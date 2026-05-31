using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Engine.Components;
using Json.Schema;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using SchemaDocument = Json.Schema.JsonSchema;

namespace FluxMq.Components.JsonSchema;

public sealed class JsonSchemaValidatorComponent : IFlowNode, IFlowEventSource
{
    private readonly ActionBlock<MqttEnvelope> _block;
    private readonly BufferBlock<JsonSchemaValidationResult> _result;
    private readonly BufferBlock<MqttEnvelope> _valid;
    private readonly BufferBlock<MqttEnvelope> _invalid;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowEvent> _events;
    private readonly SchemaDocument _schema;
    private readonly string _schemaId;

    public JsonSchemaValidatorComponent(
        JsonSchemaValidatorDefinition definition,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.SchemaJson);

        Id = id ?? FlowNodeId.New();
        _schema = SchemaDocument.FromText(definition.SchemaJson);
        _schemaId = string.IsNullOrWhiteSpace(definition.SchemaId) ? "inline" : definition.SchemaId.Trim();
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _events = new BufferBlock<FlowEvent>();
        _result = new BufferBlock<JsonSchemaValidationResult>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _valid = new BufferBlock<MqttEnvelope>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _invalid = new BufferBlock<MqttEnvelope>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _block = new ActionBlock<MqttEnvelope>(
            ValidateAndRouteAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

        _block.Completion.ContinueWith(
            CompleteOutputs,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public ISourceBlock<FlowEvent> Events => _events;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<JsonSchemaValidationResult> Result => _result;
    public ISourceBlock<MqttEnvelope> Valid => _valid;
    public ISourceBlock<MqttEnvelope> Invalid => _invalid;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "JSON Schema validator faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private async Task ValidateAndRouteAsync(MqttEnvelope envelope)
    {
        var result = Validate(envelope);
        await _result.SendAsync(result).ConfigureAwait(false);
        PublishEvent(result);

        var branch = result.IsValid ? _valid : _invalid;
        await branch.SendAsync(result.Envelope).ConfigureAwait(false);
    }

    private JsonSchemaValidationResult Validate(MqttEnvelope envelope)
    {
        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(envelope.Payload));
            payload = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            return new JsonSchemaValidationResult
            {
                SchemaId = _schemaId,
                IsValid = false,
                Envelope = envelope,
                Issues =
                [
                    new JsonSchemaValidationIssue("$", $"Payload is not valid JSON: {exception.Message}")
                ]
            };
        }

        try
        {
            var results = _schema.Evaluate(payload, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });

            return new JsonSchemaValidationResult
            {
                SchemaId = _schemaId,
                IsValid = results.IsValid,
                Envelope = envelope,
                Issues = results.IsValid ? [] : CollectIssues(results)
            };
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "JSON Schema validation failed.", exception, envelope.Topic);
            return new JsonSchemaValidationResult
            {
                SchemaId = _schemaId,
                IsValid = false,
                Envelope = envelope,
                Issues =
                [
                    new JsonSchemaValidationIssue("$", "JSON Schema validation failed.")
                ]
            };
        }
    }

    private void CompleteOutputs(Task completion)
    {
        if (completion.IsFaulted && completion.Exception is { } exception)
        {
            ((IDataflowBlock)_result).Fault(exception);
            ((IDataflowBlock)_valid).Fault(exception);
            ((IDataflowBlock)_invalid).Fault(exception);
            _errors.Complete();
            _events.Complete();
            return;
        }

        _result.Complete();
        _valid.Complete();
        _invalid.Complete();
        _errors.Complete();
        _events.Complete();
    }

    private static IReadOnlyList<JsonSchemaValidationIssue> CollectIssues(EvaluationResults results)
    {
        var issues = new List<JsonSchemaValidationIssue>();
        foreach (var result in Flatten(results))
        {
            if (result.IsValid)
            {
                continue;
            }

            if (result.Errors is { Count: > 0 } errors)
            {
                foreach (var error in errors)
                {
                    issues.Add(new JsonSchemaValidationIssue(FormatPath(result), error.Value));
                }
            }
            else if (result.Details is null || result.Details.Count == 0)
            {
                issues.Add(new JsonSchemaValidationIssue(FormatPath(result), "JSON value does not match schema."));
            }
        }

        return issues.Count > 0
            ? issues
            :
            [
                new JsonSchemaValidationIssue("$", "JSON value does not match schema.")
            ];
    }

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults results)
    {
        yield return results;
        foreach (var detail in results.Details ?? [])
        {
            foreach (var result in Flatten(detail))
            {
                yield return result;
            }
        }
    }

    private static string FormatPath(EvaluationResults result)
        => result.InstanceLocation.ToString();

    private void PublishError(int code, string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = context
        });
    }

    private void PublishEvent(JsonSchemaValidationResult result)
    {
        _events.Post(new FlowEvent
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
}
