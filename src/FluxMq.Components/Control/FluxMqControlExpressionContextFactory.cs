using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.Mapping;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Components.Timers.Contracts;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using System.Text;

namespace FluxMq.Components.Control;

public static class FluxMqControlExpressionContextFactory
{
    public static FlowMapContext Create<TInput>(TInput input)
    {
        var variables = new Dictionary<string, object?>
        {
            ["input"] = input,
            ["value"] = input,
            ["inputType"] = typeof(TInput).Name,
            ["Encoding"] = typeof(Encoding),
            ["Guid"] = typeof(Guid),
            ["SessionId"] = typeof(SessionId),
            ["MqttEnvelope"] = typeof(MqttEnvelope),
            ["MqttQualityOfServiceLevel"] = typeof(MqttQualityOfServiceLevel),
            ["MqttPublishRequest"] = typeof(MqttPublishRequest),
            ["MqttRecordingRequest"] = typeof(MqttRecordingRequest),
            ["FileWriteRequest"] = typeof(FileWriteRequest),
            ["FileWriteMode"] = typeof(FileWriteMode),
            ["PayloadInspectionRequest"] = typeof(PayloadInspectionRequest),
            ["PayloadInspectionResult"] = typeof(PayloadInspectionResult),
            ["PayloadKind"] = typeof(PayloadKind),
            ["HttpRequestInput"] = typeof(HttpRequestInput),
            ["HttpResponseOutput"] = typeof(HttpResponseOutput),
            ["HttpErrorOutput"] = typeof(HttpErrorOutput),
            ["HttpErrorKind"] = typeof(HttpErrorKind),
            ["StateReducerInput"] = typeof(StateReducerInput),
            ["StateReducerResult"] = typeof(StateReducerResult),
            ["StateReducerOperation"] = typeof(StateReducerOperation),
            ["TimerTick"] = typeof(TimerTick),
            ["ScheduleTick"] = typeof(ScheduleTick)
        };

        AddTypeSpecificVariables(variables, input);
        return new FlowMapContext { Variables = variables };
    }

    private static void AddTypeSpecificVariables<TInput>(Dictionary<string, object?> variables, TInput input)
    {
        switch (input)
        {
            case MqttEnvelope envelope:
                Merge(variables, MqttEnvelopeExpressionContextFactory.Create(envelope));
                variables["input"] = envelope;
                variables["value"] = envelope;
                break;
            case MqttPublishRequest request:
                AddPublishRequest(variables, request);
                break;
            case MqttRecordingRequest request:
                variables["recordingRequest"] = request;
                variables["sessionId"] = request.SessionId;
                Merge(variables, MqttEnvelopeExpressionContextFactory.Create(request.Envelope));
                variables["input"] = request;
                variables["value"] = request;
                break;
            case FileWriteRequest request:
                variables["fileWriteRequest"] = request;
                variables["path"] = request.Path;
                variables["content"] = request.Content;
                variables["contentText"] = Encoding.UTF8.GetString(request.Content);
                variables["mode"] = request.Mode;
                variables["createDirectory"] = request.CreateDirectory;
                break;
            case JsonSchemaValidationResult result:
                variables["validationResult"] = result;
                variables["schemaId"] = result.SchemaId;
                variables["isValid"] = result.IsValid;
                variables["issues"] = result.Issues;
                variables["issueCount"] = result.Issues.Count;
                Merge(variables, MqttEnvelopeExpressionContextFactory.Create(result.Envelope));
                variables["input"] = result;
                variables["value"] = result;
                break;
            case InspectedMqttMessage inspected:
                variables["inspected"] = inspected;
                variables["inspection"] = inspected.Payload;
                variables["payloadFormat"] = inspected.Payload.Format;
                variables["payloadKind"] = inspected.Payload.ContentTypeLabel;
                variables["payloadSize"] = inspected.Payload.SizeBytes;
                Merge(variables, MqttEnvelopeExpressionContextFactory.Create(inspected.Envelope));
                variables["input"] = inspected;
                variables["value"] = inspected;
                break;
            case PayloadInspectionRequest request:
                variables["inspectionRequest"] = request;
                variables["bytes"] = request.Bytes;
                variables["text"] = request.Text;
                variables["contentType"] = request.ContentType;
                variables["encodingHint"] = request.EncodingHint;
                break;
            case PayloadInspectionResult result:
                Merge(variables, HttpPayloadExpressionContextFactory.Create(result));
                break;
            case HttpRequestInput request:
                AddHttpRequest(variables, request);
                break;
            case HttpResponseOutput response:
                Merge(variables, HttpPayloadExpressionContextFactory.Create(response));
                break;
            case HttpErrorOutput error:
                Merge(variables, HttpPayloadExpressionContextFactory.Create(error));
                break;
            case StateReducerResult result:
                variables["stateResult"] = result;
                variables["key"] = result.Key;
                variables["previousState"] = result.PreviousState;
                variables["stateInput"] = result.Input;
                variables["newState"] = result.NewState;
                variables["version"] = result.Version;
                variables["updatedAt"] = result.UpdatedAt;
                break;
            case TimerTick tick:
                Merge(variables, TimerTickExpressionContextFactory.Create(tick));
                break;
            case ScheduleTick tick:
                Merge(variables, ScheduleTickExpressionContextFactory.Create(tick));
                break;
        }
    }

    private static void AddPublishRequest(Dictionary<string, object?> variables, MqttPublishRequest request)
    {
        variables["request"] = request;
        variables["topic"] = request.Topic;
        variables["payload"] = request.Payload;
        variables["payloadText"] = Encoding.UTF8.GetString(request.Payload);
        variables["qos"] = (int)request.QualityOfService;
        variables["qualityOfService"] = request.QualityOfService;
        variables["retain"] = request.Retain;
    }

    private static void AddHttpRequest(Dictionary<string, object?> variables, HttpRequestInput request)
    {
        variables["request"] = request;
        variables["method"] = request.Method;
        variables["url"] = request.Url;
        variables["headers"] = request.Headers;
        variables["body"] = request.Body;
        variables["bytes"] = request.Bytes;
        variables["contentType"] = request.ContentType;
        variables["timeoutMilliseconds"] = request.TimeoutMilliseconds;
    }

    private static void Merge(Dictionary<string, object?> variables, FlowMapContext context)
    {
        foreach (var (key, value) in context.Variables)
        {
            variables[key] = value;
        }
    }
}
