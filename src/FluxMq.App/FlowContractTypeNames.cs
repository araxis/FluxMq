using FluxMq.Components.Assertions;
using FluxMq.Components.FileWriter;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Core.Models;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Components.Timers.Contracts;
using FluxFlow.Engine.Components;

namespace FluxMq.App;

public static class FlowContractTypeNames
{
    public const string MqttEnvelope = nameof(MqttEnvelope);
    public const string MqttPublishRequest = nameof(MqttPublishRequest);
    public const string MqttRecordingRequest = nameof(MqttRecordingRequest);
    public const string FileWriteRequest = nameof(FileWriteRequest);
    public const string PayloadInspectionRequest = nameof(PayloadInspectionRequest);
    public const string PayloadInspectionResult = nameof(PayloadInspectionResult);
    public const string HttpRequestInput = nameof(HttpRequestInput);
    public const string HttpResponseOutput = nameof(HttpResponseOutput);
    public const string HttpErrorOutput = nameof(HttpErrorOutput);
    public const string JsonSchemaValidationResult = nameof(JsonSchemaValidationResult);
    public const string InspectedMqttMessage = nameof(InspectedMqttMessage);
    public const string MqttMetricsSnapshot = nameof(MqttMetricsSnapshot);
    public const string TimerTick = nameof(TimerTick);
    public const string ScheduleTick = nameof(ScheduleTick);
    public const string StateReducerResult = nameof(StateReducerResult);
    public const string FlowLogEntry = nameof(FlowLogEntry);
    public const string FlowError = nameof(FlowError);

    public static readonly IReadOnlyList<string> AssertionInputTypes =
    [
        MqttEnvelope,
        MqttPublishRequest,
        MqttRecordingRequest,
        FileWriteRequest,
        PayloadInspectionRequest,
        PayloadInspectionResult,
        HttpRequestInput,
        HttpResponseOutput,
        HttpErrorOutput,
        JsonSchemaValidationResult,
        InspectedMqttMessage,
        MqttMetricsSnapshot,
        TimerTick,
        ScheduleTick,
        StateReducerResult,
        FlowLogEntry,
        FlowError
    ];

    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Contains('.')
            ? trimmed[(trimmed.LastIndexOf('.') + 1)..]
            : trimmed;
    }

    public static bool IsAssertionInputType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return AssertionInputTypes.Contains(Normalize(value), StringComparer.Ordinal);
    }

    public static string FormatAssertionInputTypes()
        => string.Join(", ", AssertionInputTypes);
}
