using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxFlow.Components.Payloads;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using PackagePayloadInspectionRequest = FluxFlow.Components.Payloads.Contracts.PayloadInspectionRequest;
using PackagePayloadInspectionResult = FluxFlow.Components.Payloads.Contracts.PayloadInspectionResult;
using PackagePayloadKind = FluxFlow.Components.Payloads.Contracts.PayloadKind;

namespace FluxMq.Components.MqttPayloadInspector;

public sealed class PayloadInspectorMapperComponent : FlowNodeBase
{
    private static readonly PortName InputPort = new("Input");

    private readonly Lock _sync = new();
    private readonly Queue<MqttEnvelope> _pendingEnvelopes = new();
    private readonly RuntimeNode _innerNode;
    private readonly ITargetBlock<PackagePayloadInspectionRequest> _innerInput;
    private readonly ActionBlock<MqttEnvelope> _input;
    private readonly ActionBlock<PackagePayloadInspectionResult> _resultRelay;
    private readonly ActionBlock<FlowError> _packageErrors;
    private readonly BufferBlock<InspectedMqttMessage> _output;
    private readonly List<IDisposable> _links = [];
    private int _started;

    public PayloadInspectorMapperComponent(FlowNodeId? id = null, int boundedCapacity = 1000)
        : base(id ?? FlowNodeId.New())
    {
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Bounded capacity must be positive.");
        }

        _innerNode = CreateInnerNode(boundedCapacity);
        _innerInput = GetInput<PackagePayloadInspectionRequest>(_innerNode, PayloadComponentPorts.Input);
        _input = new ActionBlock<MqttEnvelope>(
            InspectAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _resultRelay = new ActionBlock<PackagePayloadInspectionResult>(
            PublishResult,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _packageErrors = new ActionBlock<FlowError>(
            error => TryReportError(error.Code, error.Message, error.Exception, error.Context),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _output = new BufferBlock<InspectedMqttMessage>(
            new DataflowBlockOptions { BoundedCapacity = boundedCapacity });

        _links.Add(LinkOutput(_innerNode, PayloadComponentPorts.Output, _resultRelay));
        _links.Add(_innerNode.Node.Errors.LinkTo(
            _packageErrors,
            new DataflowLinkOptions { PropagateCompletion = true }));

        CompleteWhen(CompleteAsync());
    }

    public ITargetBlock<MqttEnvelope> Input => _input;
    public ISourceBlock<InspectedMqttMessage> Output => _output;

    public override Task StartAsync(CancellationToken cancellationToken = default)
        => Interlocked.Exchange(ref _started, 1) == 0
            ? _innerNode.Node.StartAsync(cancellationToken)
            : Task.CompletedTask;

    public override void Complete()
        => _input.Complete();

    public override void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        TryReportError(FlowErrorCodes.NodeFaulted, "Payload inspector mapper faulted.", exception);

        try
        {
            ((IDataflowBlock)_input).Fault(exception);
            _innerNode.Node.Fault(exception);
            ((IDataflowBlock)_resultRelay).Fault(exception);
            ((IDataflowBlock)_packageErrors).Fault(exception);
            ((IDataflowBlock)_output).Fault(exception);
        }
        finally
        {
            FaultNode(exception);
        }
    }

    protected override void OnNodeCompleted()
    {
        DisposeLinks();
        _output.Complete();
        base.OnNodeCompleted();
    }

    protected override void OnNodeFaulted(Exception exception)
    {
        DisposeLinks();
        ((IDataflowBlock)_output).Fault(exception);
        base.OnNodeFaulted(exception);
    }

    private async Task InspectAsync(MqttEnvelope envelope)
    {
        try
        {
            var request = CreateRequest(envelope);
            lock (_sync)
            {
                _pendingEnvelopes.Enqueue(envelope);
            }

            var accepted = await _innerInput.SendAsync(request).ConfigureAwait(false);
            if (!accepted)
            {
                RemovePendingEnvelope();
                TryReportError(
                    FlowErrorCodes.ProcessingFailed,
                    "Payload inspection failed.",
                    new InvalidOperationException("Payload inspector input is closed."),
                    envelope.Topic);
                await _output.SendAsync(new InspectedMqttMessage(envelope, CreateEmptyInspection()))
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            TryReportError(FlowErrorCodes.ProcessingFailed, "Payload inspection failed.", exception, envelope.Topic);
            await _output.SendAsync(new InspectedMqttMessage(envelope, CreateEmptyInspection()))
                .ConfigureAwait(false);
        }
    }

    private void PublishResult(PackagePayloadInspectionResult result)
    {
        MqttEnvelope? envelope;
        lock (_sync)
        {
            envelope = _pendingEnvelopes.Count == 0
                ? null
                : _pendingEnvelopes.Dequeue();
        }

        if (envelope is null)
        {
            TryReportError(
                FlowErrorCodes.ProcessingFailed,
                "Payload inspection failed.",
                new InvalidOperationException("Payload inspection result did not have a matching envelope."));
            return;
        }

        _output.Post(new InspectedMqttMessage(envelope, MapResult(result, envelope.Payload)));
    }

    private async Task CompleteAsync()
    {
        await _input.Completion.ConfigureAwait(false);
        _innerNode.Node.Complete();
        await Task.WhenAll(
                _innerNode.Node.Completion,
                _resultRelay.Completion,
                _packageErrors.Completion)
            .ConfigureAwait(false);
    }

    private static PackagePayloadInspectionRequest CreateRequest(MqttEnvelope envelope)
        => new()
        {
            Bytes = envelope.Payload
        };

    private static PayloadInspectionResult MapResult(
        PackagePayloadInspectionResult result,
        byte[] payload)
    {
        var format = MapFormat(result, out var jsonKind);
        var label = CreateLabel(format);
        var rawText = result.TextPreview ?? string.Empty;
        var formatted = CreateFormattedText(result, format, rawText);

        return new PayloadInspectionResult
        {
            Format = format,
            SizeBytes = result.ByteCount,
            IsText = format is not PayloadFormat.Binary,
            ContentTypeLabel = label,
            JsonValueKind = jsonKind,
            RawText = rawText,
            FormattedText = formatted,
            HexDump = BuildHexDump(payload),
            Metadata = $"{result.ByteCount} bytes | {label}"
        };
    }

    private static PayloadInspectionResult CreateEmptyInspection()
        => new()
        {
            Format = PayloadFormat.Empty,
            SizeBytes = 0,
            IsText = true,
            ContentTypeLabel = "Empty",
            RawText = string.Empty,
            FormattedText = string.Empty,
            HexDump = string.Empty,
            Metadata = "0 bytes | Empty"
        };

    private static PayloadFormat MapFormat(
        PackagePayloadInspectionResult result,
        out JsonValueKind jsonKind)
    {
        jsonKind = JsonValueKind.Undefined;
        return result.Kind switch
        {
            PackagePayloadKind.Empty => PayloadFormat.Empty,
            PackagePayloadKind.JsonObject => SetJsonKind(PayloadFormat.Json, JsonValueKind.Object, out jsonKind),
            PackagePayloadKind.JsonArray => SetJsonKind(PayloadFormat.Array, JsonValueKind.Array, out jsonKind),
            PackagePayloadKind.JsonScalar => MapJsonScalar(result.TextPreview, out jsonKind),
            PackagePayloadKind.Xml => PayloadFormat.Xml,
            PackagePayloadKind.Base64 => PayloadFormat.Base64,
            PackagePayloadKind.Text => PayloadFormat.Text,
            PackagePayloadKind.Binary => PayloadFormat.Binary,
            _ => PayloadFormat.Text
        };
    }

    private static PayloadFormat MapJsonScalar(string? rawText, out JsonValueKind jsonKind)
    {
        jsonKind = JsonValueKind.Undefined;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return PayloadFormat.Text;
        }

        try
        {
            using var document = JsonDocument.Parse(rawText);
            jsonKind = document.RootElement.ValueKind;
            return jsonKind switch
            {
                JsonValueKind.String => PayloadFormat.String,
                JsonValueKind.Number => PayloadFormat.Number,
                JsonValueKind.True or JsonValueKind.False => PayloadFormat.Boolean,
                JsonValueKind.Null => PayloadFormat.Null,
                JsonValueKind.Array => PayloadFormat.Array,
                JsonValueKind.Object => PayloadFormat.Json,
                _ => PayloadFormat.Text
            };
        }
        catch (JsonException)
        {
            return PayloadFormat.Text;
        }
    }

    private static PayloadFormat SetJsonKind(
        PayloadFormat format,
        JsonValueKind valueKind,
        out JsonValueKind jsonKind)
    {
        jsonKind = valueKind;
        return format;
    }

    private static string CreateFormattedText(
        PackagePayloadInspectionResult result,
        PayloadFormat format,
        string rawText)
    {
        if (format == PayloadFormat.Base64)
        {
            return result.Base64DecodedByteCount.HasValue
                ? $"Base64 payload{Environment.NewLine}Decoded bytes: {result.Base64DecodedByteCount.Value}"
                : "Base64 payload";
        }

        if (!string.IsNullOrEmpty(result.FormattedPreview))
        {
            return result.FormattedPreview;
        }

        return format is PayloadFormat.Text or PayloadFormat.String or PayloadFormat.Number or PayloadFormat.Boolean or PayloadFormat.Null
            ? rawText
            : string.Empty;
    }

    private static string CreateLabel(PayloadFormat format)
        => format switch
        {
            PayloadFormat.Empty => "Empty",
            PayloadFormat.Json => "JSON",
            PayloadFormat.Array => "Array",
            PayloadFormat.String => "String",
            PayloadFormat.Number => "Number",
            PayloadFormat.Boolean => "Boolean",
            PayloadFormat.Null => "Null",
            PayloadFormat.Xml => "XML",
            PayloadFormat.Base64 => "Base64",
            PayloadFormat.Text => "Text",
            PayloadFormat.Binary => "Binary",
            _ => "Unknown"
        };

    private static string BuildHexDump(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var offset = 0; offset < payload.Length; offset += 8)
        {
            var line = payload.AsSpan(offset, Math.Min(8, payload.Length - offset));
            builder.Append(offset.ToString("X8"));
            builder.Append("  ");

            for (var index = 0; index < 8; index++)
            {
                builder.Append(index < line.Length ? line[index].ToString("X2") : "  ");
                builder.Append(index == 3 ? "  " : " ");
            }

            if (offset + 8 < payload.Length)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static RuntimeNode CreateInnerNode(int boundedCapacity)
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterPayloadComponents();

        if (!registry.TryGetFactory(PayloadComponentTypes.Inspect, out var factory))
        {
            throw new InvalidOperationException("Payload inspect package factory was not registered.");
        }

        return factory(new RuntimeNodeFactoryContext(
            new NodeName("payloadInspect"),
            CreateInnerDefinition(boundedCapacity),
            "payloadInspect",
            new Dictionary<NodeName, RuntimeNode>()));
    }

    private static NodeDefinition CreateInnerDefinition(int boundedCapacity)
    {
        var definition = new NodeDefinition
        {
            Type = PayloadComponentTypes.Inspect
        };

        definition.Configuration["boundedCapacity"] = JsonSerializer.SerializeToElement(boundedCapacity);
        definition.Configuration["maxPreviewBytes"] = JsonSerializer.SerializeToElement(1024 * 1024);
        definition.Configuration["maxFormattedChars"] = JsonSerializer.SerializeToElement(1024 * 1024);
        definition.Configuration["detectBase64"] = JsonSerializer.SerializeToElement(true);
        definition.Configuration["formatJson"] = JsonSerializer.SerializeToElement(true);
        definition.Configuration["formatXml"] = JsonSerializer.SerializeToElement(true);

        return definition;
    }

    private static ITargetBlock<T> GetInput<T>(RuntimeNode node, string portName)
    {
        var input = node.FindInput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner payload inspector input '{portName}' was not found.");

        return input is InputPort<T> typedInput
            ? typedInput.Target
            : throw new InvalidOperationException(
                $"Inner payload inspector input '{portName}' has unsupported type '{input.ValueType.Name}'.");
    }

    private static IDisposable LinkOutput<T>(
        RuntimeNode node,
        string portName,
        ITargetBlock<T> target)
    {
        var output = node.FindOutput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner payload inspector output '{portName}' was not found.");

        var link = output.TryLinkTo(
            new InputPort<T>(
                new PortAddress(
                    "payloadInspect",
                    new NodeName(portName),
                    InputPort),
                target),
            propagateCompletion: true,
            out var error);

        return link ?? throw new InvalidOperationException(
            error?.Message ?? $"Could not link inner payload inspector output '{portName}'.");
    }

    private void RemovePendingEnvelope()
    {
        lock (_sync)
        {
            if (_pendingEnvelopes.Count > 0)
            {
                _pendingEnvelopes.Dequeue();
            }
        }
    }

    private void DisposeLinks()
    {
        foreach (var link in _links)
        {
            link.Dispose();
        }

        _links.Clear();
    }
}
