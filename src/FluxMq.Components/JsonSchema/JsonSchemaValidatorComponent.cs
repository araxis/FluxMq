using FluxMq.Core.Models;
using FluxFlow.Components.Validation;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using PackageValidationIssue = FluxFlow.Components.Validation.Contracts.JsonSchemaValidationIssue;
using PackageValidationResult = FluxFlow.Components.Validation.Contracts.JsonSchemaValidationResult<FluxMq.Core.Models.MqttEnvelope>;

namespace FluxMq.Components.JsonSchema;

public sealed class JsonSchemaValidatorComponent : EventFlowNodeBase
{
    private const string InputTypeName = "MqttEnvelope";
    private const string PayloadTextSelector = "payloadText";

    private static readonly PortName InputPort = new("Input");

    private readonly RuntimeNode _innerNode;
    private readonly TransformBlock<PackageValidationResult, JsonSchemaValidationResult> _result;
    private readonly BufferBlock<MqttEnvelope> _valid;
    private readonly BufferBlock<MqttEnvelope> _invalid;
    private readonly ActionBlock<FlowError> _errors;
    private readonly List<IDisposable> _links = [];
    private int _started;

    public JsonSchemaValidatorComponent(
        JsonSchemaValidatorDefinition definition,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
        : base(id ?? FlowNodeId.New())
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (boundedCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundedCapacity),
                "JSON Schema validator bounded capacity must be greater than zero.");
        }

        var blockOptions = new DataflowBlockOptions { BoundedCapacity = boundedCapacity };
        var executionOptions = new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity,
            EnsureOrdered = true
        };

        _innerNode = CreateInnerNode(definition, boundedCapacity);
        Input = GetInput<MqttEnvelope>(_innerNode, ValidationComponentPorts.Input);
        _result = new TransformBlock<PackageValidationResult, JsonSchemaValidationResult>(
            MapResult,
            executionOptions);
        _valid = new BufferBlock<MqttEnvelope>(blockOptions);
        _invalid = new BufferBlock<MqttEnvelope>(blockOptions);
        _errors = new ActionBlock<FlowError>(
            error => TryReportError(error.Code, error.Message, error.Exception, error.Context),
            executionOptions);

        _links.Add(LinkOutput(_innerNode, ValidationComponentPorts.Result, _result));
        _links.Add(LinkOutput(_innerNode, ValidationComponentPorts.Valid, _valid));
        _links.Add(LinkOutput(_innerNode, ValidationComponentPorts.Invalid, _invalid));
        _links.Add(_innerNode.Node.Errors.LinkTo(
            _errors,
            new DataflowLinkOptions { PropagateCompletion = true }));

        CompleteWhen(Task.WhenAll(
            _innerNode.Node.Completion,
            _result.Completion,
            _errors.Completion));
    }

    public ITargetBlock<MqttEnvelope> Input { get; }
    public ISourceBlock<JsonSchemaValidationResult> Result => _result;
    public ISourceBlock<MqttEnvelope> Valid => _valid;
    public ISourceBlock<MqttEnvelope> Invalid => _invalid;

    public override Task StartAsync(CancellationToken cancellationToken = default)
        => Interlocked.Exchange(ref _started, 1) == 0
            ? _innerNode.Node.StartAsync(cancellationToken)
            : Task.CompletedTask;

    public override void Complete()
        => _innerNode.Node.Complete();

    public override void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            _innerNode.Node.Fault(exception);
            ((IDataflowBlock)_result).Fault(exception);
            ((IDataflowBlock)_valid).Fault(exception);
            ((IDataflowBlock)_invalid).Fault(exception);
            ((IDataflowBlock)_errors).Fault(exception);
        }
        finally
        {
            FaultNode(exception);
        }
    }

    protected override void OnNodeCompleted()
    {
        _valid.Complete();
        _invalid.Complete();
        DisposeLinks();
        base.OnNodeCompleted();
    }

    protected override void OnNodeFaulted(Exception exception)
    {
        ((IDataflowBlock)_valid).Fault(exception);
        ((IDataflowBlock)_invalid).Fault(exception);
        DisposeLinks();
        base.OnNodeFaulted(exception);
    }

    private static RuntimeNode CreateInnerNode(
        JsonSchemaValidatorDefinition definition,
        int boundedCapacity)
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterValidationComponents(options => options
                .RegisterType<MqttEnvelope>(InputTypeName)
                .UseValueSelector<MqttEnvelope>(
                    PayloadTextSelector,
                    static (envelope, _) => Encoding.UTF8.GetString(envelope.Payload)));

        if (!registry.TryGetFactory(ValidationComponentTypes.JsonSchemaValidator, out var factory))
        {
            throw new InvalidOperationException("JSON Schema validator package factory was not registered.");
        }

        return factory(new RuntimeNodeFactoryContext(
            new NodeName("jsonSchemaValidator"),
            CreateInnerDefinition(definition, boundedCapacity),
            "jsonSchemaValidator",
            new Dictionary<NodeName, RuntimeNode>()));
    }

    private static NodeDefinition CreateInnerDefinition(
        JsonSchemaValidatorDefinition definition,
        int boundedCapacity)
    {
        var innerDefinition = new NodeDefinition
        {
            Type = ValidationComponentTypes.JsonSchemaValidator
        };

        if (!string.IsNullOrWhiteSpace(definition.SchemaJson))
        {
            innerDefinition.Configuration["schema"] = ToJsonString(definition.SchemaJson);
        }

        if (!string.IsNullOrWhiteSpace(definition.SchemaPath))
        {
            innerDefinition.Configuration["schemaPath"] = ToJsonString(definition.SchemaPath);
        }

        innerDefinition.Configuration["schemaId"] = ToJsonString(definition.SchemaId);
        innerDefinition.Configuration["inputType"] = ToJsonString(InputTypeName);
        innerDefinition.Configuration["valueSelector"] = ToJsonString(PayloadTextSelector);
        innerDefinition.Configuration["boundedCapacity"] =
            JsonSerializer.SerializeToElement(boundedCapacity);

        return innerDefinition;
    }

    private static JsonElement ToJsonString(string value)
        => JsonSerializer.SerializeToElement(value);

    private static ITargetBlock<T> GetInput<T>(RuntimeNode node, string portName)
    {
        var input = node.FindInput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner validator input '{portName}' was not found.");

        return input is InputPort<T> typedInput
            ? typedInput.Target
            : throw new InvalidOperationException(
                $"Inner validator input '{portName}' has unsupported type '{input.ValueType.Name}'.");
    }

    private static IDisposable LinkOutput<T>(
        RuntimeNode node,
        string portName,
        ITargetBlock<T> target)
    {
        var output = node.FindOutput(new PortName(portName))
            ?? throw new InvalidOperationException($"Inner validator output '{portName}' was not found.");

        var link = output.TryLinkTo(
            new InputPort<T>(
                new PortAddress(
                    "jsonSchemaValidator",
                    new NodeName(portName),
                    InputPort),
                target),
            propagateCompletion: true,
            out var error);

        if (error is not null || link is null)
        {
            throw new InvalidOperationException(
                error?.Message ?? $"Could not link inner validator output '{portName}'.");
        }

        return link;
    }

    private JsonSchemaValidationResult MapResult(PackageValidationResult result)
    {
        var mapped = new JsonSchemaValidationResult
        {
            SchemaId = string.IsNullOrWhiteSpace(result.SchemaId) ? "inline" : result.SchemaId,
            IsValid = result.IsValid,
            Envelope = result.Input,
            Issues = CreateIssues(result)
        };

        PublishEvent(mapped);
        return mapped;
    }

    private static IReadOnlyList<JsonSchemaValidationIssue> CreateIssues(PackageValidationResult result)
    {
        if (result.IsValid)
        {
            return [];
        }

        if (!IsPayloadValidJson(result.Input))
        {
            return [new JsonSchemaValidationIssue("$", "Payload is not valid JSON.")];
        }

        var issues = result.Issues
            .Select(MapIssue)
            .Where(issue => !string.IsNullOrWhiteSpace(issue.Message))
            .ToArray();

        return issues.Length > 0
            ? issues
            : [new JsonSchemaValidationIssue("$", "JSON value does not match schema.")];
    }

    private static JsonSchemaValidationIssue MapIssue(PackageValidationIssue issue)
        => new(
            Path: FirstNonEmpty(
                issue.InstanceLocation,
                issue.EvaluationPath,
                issue.SchemaLocation) ?? "$",
            Message: issue.Message ?? "JSON value does not match schema.");

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsPayloadValidJson(MqttEnvelope envelope)
    {
        try
        {
            using var _ = JsonDocument.Parse(envelope.Payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void PublishEvent(JsonSchemaValidationResult result)
    {
        EmitEvent(new FlowEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FluxMqEventTypes.JsonSchemaValidated,
            Source = "JsonSchemaValidator",
            SourceNodeId = Id,
            Subject = result.Envelope.Topic,
            Status = result.IsValid ? "valid" : "invalid",
            Channel = result.Envelope.Topic,
            PayloadBytes = result.Envelope.Payload.Length,
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["schemaId"] = result.SchemaId,
                ["isValid"] = result.IsValid.ToString(),
                ["issueCount"] = result.Issues.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        });
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
