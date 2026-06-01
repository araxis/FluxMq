using FluxMq.Core.Models;
using FluxFlow.Components.Observability;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using PackageFlowLogEntry = FluxFlow.Components.Observability.Contracts.FlowLogEntry;
using PackageFlowLogLevel = FluxFlow.Components.Observability.Contracts.FlowLogLevel;

namespace FluxMq.Components.Logging;

public sealed class FlowLoggerComponent : FlowNodeBase
{
    private static readonly PortName InputPort = new("Input");

    private readonly Lock _sync = new();
    private readonly Queue<FlowLogEntry> _recentEntries = new();
    private readonly RuntimeNode _messageLogger;
    private readonly RuntimeNode _errorLogger;
    private readonly ActionBlock<PackageFlowLogEntry> _messageEntries;
    private readonly ActionBlock<PackageFlowLogEntry> _errorEntries;
    private readonly ActionBlock<FlowError> _packageErrors;
    private readonly BufferBlock<FlowLogEntry> _entries;
    private readonly List<IDisposable> _links = [];
    private readonly int _maxEntries;

    public FlowLoggerComponent(
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxEntries = 500,
        bool includePayloadPreview = false,
        int maxPayloadPreviewChars = 512,
        bool waitForMessageInputCompletion = true,
        bool waitForFlowErrorInputCompletion = true)
        : base(id ?? FlowNodeId.New())
    {
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity), boundedCapacity, "Bounded capacity must be positive.");
        }

        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), maxEntries, "Maximum entries must be positive.");
        }

        if (maxPayloadPreviewChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadPreviewChars), maxPayloadPreviewChars, "Payload preview length must be positive.");
        }

        _maxEntries = maxEntries;
        _entries = new BufferBlock<FlowLogEntry>();
        _messageLogger = CreateMessageLogger(
            boundedCapacity,
            includePayloadPreview,
            maxPayloadPreviewChars);
        _errorLogger = CreateErrorLogger(boundedCapacity);
        _messageEntries = new ActionBlock<PackageFlowLogEntry>(
            entry => PublishEntry(MapMessageEntry(entry)),
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });
        _errorEntries = new ActionBlock<PackageFlowLogEntry>(
            entry => PublishEntry(MapErrorEntry(entry)),
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

        Input = GetInput<MqttEnvelope>(_messageLogger, ObservabilityComponentPorts.Input);
        FlowErrors = GetInput<FlowError>(_errorLogger, ObservabilityComponentPorts.Input);
        _links.Add(LinkOutput(_messageLogger, ObservabilityComponentPorts.Entries, _messageEntries));
        _links.Add(LinkOutput(_errorLogger, ObservabilityComponentPorts.Entries, _errorEntries));
        _links.Add(_messageLogger.Node.Errors.LinkTo(_packageErrors));
        _links.Add(_errorLogger.Node.Errors.LinkTo(_packageErrors));

        var completions = new List<Task>(2);
        if (waitForMessageInputCompletion)
        {
            completions.Add(_messageLogger.Node.Completion);
            completions.Add(_messageEntries.Completion);
        }
        else
        {
            _messageLogger.Node.Complete();
        }

        if (waitForFlowErrorInputCompletion)
        {
            completions.Add(_errorLogger.Node.Completion);
            completions.Add(_errorEntries.Completion);
        }
        else
        {
            _errorLogger.Node.Complete();
        }

        CompleteWhen(CompleteRelaysAsync(completions.Count == 0
            ? Task.CompletedTask
            : Task.WhenAll(completions)));
    }

    public ITargetBlock<MqttEnvelope> Input { get; }
    public ITargetBlock<FlowError> FlowErrors { get; }
    public ISourceBlock<FlowLogEntry> Entries => _entries;

    public IReadOnlyList<FlowLogEntry> RecentEntries
    {
        get
        {
            lock (_sync)
            {
                return _recentEntries.ToArray();
            }
        }
    }

    public override void Complete()
    {
        _messageLogger.Node.Complete();
        _errorLogger.Node.Complete();
    }

    public override void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        TryReportError(FlowErrorCodes.NodeFaulted, "Flow logger faulted.", exception);

        try
        {
            _messageLogger.Node.Fault(exception);
            _errorLogger.Node.Fault(exception);
            ((IDataflowBlock)_messageEntries).Fault(exception);
            ((IDataflowBlock)_errorEntries).Fault(exception);
            ((IDataflowBlock)_packageErrors).Fault(exception);
        }
        finally
        {
            FaultNode(exception);
        }
    }

    protected override void OnNodeCompleted()
    {
        DisposeLinks();
        _entries.Complete();
        base.OnNodeCompleted();
    }

    protected override void OnNodeFaulted(Exception exception)
    {
        DisposeLinks();
        ((IDataflowBlock)_entries).Fault(exception);
        base.OnNodeFaulted(exception);
    }

    private static RuntimeNode CreateMessageLogger(
        int boundedCapacity,
        bool includePayloadPreview,
        int maxPayloadPreviewChars)
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterObservabilityComponents(options => options
                .RegisterType<MqttEnvelope>("MqttEnvelope")
                .UseValueSelector<MqttEnvelope>("topic", static (envelope, _) => envelope.Topic)
                .UseValueSelector<MqttEnvelope>("payloadBytes", static (envelope, _) => envelope.Payload.Length)
                .UseValueSelector<MqttEnvelope>("qos", static (envelope, _) => (int)envelope.QualityOfService)
                .UseValueSelector<MqttEnvelope>("retain", static (envelope, _) => envelope.Retain)
                .UseValueSelector<MqttEnvelope>(
                    "payloadPreview",
                    (envelope, _) => BuildPayloadPreview(
                        envelope.Payload,
                        includePayloadPreview,
                        maxPayloadPreviewChars)));

        return CreateInnerLogger(
            registry,
            "messageLogger",
            "MqttEnvelope",
            "Information",
            "MqttEnvelope",
            "Received MQTT message on '{topic}'.",
            ["topic", "payloadBytes", "payloadPreview", "qos", "retain"],
            boundedCapacity);
    }

    private static RuntimeNode CreateErrorLogger(int boundedCapacity)
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterObservabilityComponents(options => options
                .RegisterType<FlowError>("FlowError")
                .UseValueSelector<FlowError>("message", static (error, _) => error.Message)
                .UseValueSelector<FlowError>("occurredAt", static (error, _) => error.OccurredAt)
                .UseValueSelector<FlowError>("relatedNodeId", static (error, _) => error.NodeId)
                .UseValueSelector<FlowError>("errorCode", static (error, _) => error.Code)
                .UseValueSelector<FlowError>("context", static (error, _) => error.Context));

        return CreateInnerLogger(
            registry,
            "errorLogger",
            "FlowError",
            "Error",
            "FlowError",
            "{message}",
            ["message", "occurredAt", "relatedNodeId", "errorCode", "context"],
            boundedCapacity);
    }

    private static RuntimeNode CreateInnerLogger(
        RuntimeNodeFactoryRegistry registry,
        string nodeName,
        string inputType,
        string level,
        string category,
        string messageTemplate,
        string[] attributeSelectors,
        int boundedCapacity)
    {
        if (!registry.TryGetFactory(ObservabilityComponentTypes.Logger, out var factory))
        {
            throw new InvalidOperationException("Flow logger package factory was not registered.");
        }

        return factory(new RuntimeNodeFactoryContext(
            new NodeName(nodeName),
            CreateLoggerDefinition(inputType, level, category, messageTemplate, attributeSelectors, boundedCapacity),
            nodeName,
            new Dictionary<NodeName, RuntimeNode>()));
    }

    private static NodeDefinition CreateLoggerDefinition(
        string inputType,
        string level,
        string category,
        string messageTemplate,
        string[] attributeSelectors,
        int boundedCapacity)
    {
        var definition = new NodeDefinition
        {
            Type = ObservabilityComponentTypes.Logger
        };

        definition.Configuration["inputType"] = ToJsonString(inputType);
        definition.Configuration["level"] = ToJsonString(level);
        definition.Configuration["category"] = ToJsonString(category);
        definition.Configuration["messageTemplate"] = ToJsonString(messageTemplate);
        definition.Configuration["attributeSelectors"] =
            JsonSerializer.SerializeToElement(attributeSelectors);
        definition.Configuration["boundedCapacity"] =
            JsonSerializer.SerializeToElement(boundedCapacity);

        return definition;
    }

    private static JsonElement ToJsonString(string value)
        => JsonSerializer.SerializeToElement(value);

    private static ITargetBlock<T> GetInput<T>(RuntimeNode node, string portName)
    {
        var input = node.FindInput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner flow logger input '{portName}' was not found.");

        return input is InputPort<T> typedInput
            ? typedInput.Target
            : throw new InvalidOperationException(
                $"Inner flow logger input '{portName}' has unsupported type '{input.ValueType.Name}'.");
    }

    private static IDisposable LinkOutput<T>(
        RuntimeNode node,
        string portName,
        ITargetBlock<T> target)
    {
        var output = node.FindOutput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner flow logger output '{portName}' was not found.");

        var link = output.TryLinkTo(
            new InputPort<T>(
                new PortAddress(
                    node.Address.Scope,
                    node.Address.Node,
                    InputPort),
                target),
            propagateCompletion: true,
            out var error);

        if (error is not null || link is null)
        {
            throw new InvalidOperationException(
                error?.Message ?? $"Could not link inner flow logger output '{portName}'.");
        }

        return link;
    }

    private static string? BuildPayloadPreview(
        byte[] payload,
        bool includePayloadPreview,
        int maxPayloadPreviewChars)
    {
        if (!includePayloadPreview || payload.Length == 0)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(payload);
        text = text.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

        return text.Length <= maxPayloadPreviewChars
            ? text
            : text[..maxPayloadPreviewChars] + "...";
    }

    private static FlowLogEntry MapMessageEntry(PackageFlowLogEntry entry)
    {
        var qos = GetAttributeString(entry, "qos") ?? "0";
        var retain = GetAttributeBool(entry, "retain")?.ToString() ?? bool.FalseString;

        return new FlowLogEntry
        {
            Timestamp = entry.Timestamp,
            Severity = MapSeverity(entry.Level),
            Source = entry.Category,
            Message = entry.Message,
            Topic = GetAttributeString(entry, "topic"),
            PayloadBytes = GetAttributeInt(entry, "payloadBytes"),
            PayloadPreview = GetAttributeString(entry, "payloadPreview"),
            Context = $"qos={qos}; retain={retain}"
        };
    }

    private static FlowLogEntry MapErrorEntry(PackageFlowLogEntry entry)
        => new()
        {
            Timestamp = GetAttributeDateTimeOffset(entry, "occurredAt") ?? entry.Timestamp,
            Severity = MapSeverity(entry.Level),
            Source = entry.Category,
            Message = entry.Message,
            RelatedNodeId = GetAttributeNodeId(entry, "relatedNodeId"),
            ErrorCode = GetAttributeInt(entry, "errorCode"),
            Context = GetAttributeString(entry, "context")
        };

    private static FlowLogSeverity MapSeverity(PackageFlowLogLevel level)
        => level switch
        {
            PackageFlowLogLevel.Trace or PackageFlowLogLevel.Debug or PackageFlowLogLevel.Information
                => FlowLogSeverity.Info,
            PackageFlowLogLevel.Warning => FlowLogSeverity.Warning,
            PackageFlowLogLevel.Error or PackageFlowLogLevel.Critical => FlowLogSeverity.Error,
            _ => FlowLogSeverity.Info
        };

    private static string? GetAttributeString(PackageFlowLogEntry entry, string name)
        => entry.Attributes.TryGetValue(name, out var value)
            ? value switch
            {
                null => null,
                string text => text,
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            }
            : null;

    private static int? GetAttributeInt(PackageFlowLogEntry entry, string name)
        => entry.Attributes.TryGetValue(name, out var value)
            ? value switch
            {
                int number => number,
                long number => number > int.MaxValue ? int.MaxValue : (int)number,
                double number => number > int.MaxValue ? int.MaxValue : (int)number,
                _ when int.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => null
            }
            : null;

    private static bool? GetAttributeBool(PackageFlowLogEntry entry, string name)
        => entry.Attributes.TryGetValue(name, out var value)
            ? value switch
            {
                bool flag => flag,
                _ when bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) => parsed,
                _ => null
            }
            : null;

    private static DateTimeOffset? GetAttributeDateTimeOffset(PackageFlowLogEntry entry, string name)
        => entry.Attributes.TryGetValue(name, out var value)
            ? value switch
            {
                DateTimeOffset timestamp => timestamp,
                DateTime timestamp => timestamp,
                _ when DateTimeOffset.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsed) => parsed,
                _ => null
            }
            : null;

    private static FlowNodeId? GetAttributeNodeId(PackageFlowLogEntry entry, string name)
        => entry.Attributes.TryGetValue(name, out var value)
            ? value switch
            {
                FlowNodeId id => id,
                _ => null
            }
            : null;

    private void PublishEntry(FlowLogEntry entry)
    {
        lock (_sync)
        {
            _recentEntries.Enqueue(entry);
            while (_recentEntries.Count > _maxEntries)
            {
                _recentEntries.Dequeue();
            }
        }

        _entries.Post(entry);
    }

    private async Task CompleteRelaysAsync(Task innerCompletion)
    {
        try
        {
            await innerCompletion.ConfigureAwait(false);
            _packageErrors.Complete();
            await _packageErrors.Completion.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            ((IDataflowBlock)_packageErrors).Fault(exception);
            throw;
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
