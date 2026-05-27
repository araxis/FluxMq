using FluxMq.Components.MqttPublisher;
using FluxMq.Pipeline.Scenarios;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace FluxMq.App.Scenarios;

public sealed class MqttPublishScenarioStepRunner : IScenarioStepRunner
{
    public const string StepType = ScenarioStepTypes.MqttPublisher;

    public string Type => StepType;

    public async Task<ScenarioStepResult> RunAsync(
        ScenarioStepRunContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var configuration = context.Step.Configuration;
        var connectionName = ScenarioStepConfigurationReader.ReadRequiredString(configuration, "connection");
        var request = new MqttPublishRequest
        {
            Topic = ScenarioStepConfigurationReader.ReadRequiredString(configuration, "topic"),
            Payload = ReadPayload(configuration),
            QualityOfService = ReadQualityOfService(configuration),
            Retain = ScenarioStepConfigurationReader.ReadBoolOrDefault(configuration, "retain", false)
        };

        var publisher = context.Services.GetRequired<IMqttScenarioPublisher>();
        await publisher.PublishAsync(connectionName, request, cancellationToken).ConfigureAwait(false);

        return new ScenarioStepResult
        {
            Name = context.StepName,
            Type = context.Step.Type,
            Status = ScenarioStepRunStatus.Passed,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Message = $"Published MQTT message to '{request.Topic}'.",
            NextEventOffset = context.EventOffset
        };
    }

    private static MqttQualityOfServiceLevel ReadQualityOfService(
        IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var value = ScenarioStepConfigurationReader.ReadIntOrDefault(configuration, "qos", 0, 0);
        return value switch
        {
            0 => MqttQualityOfServiceLevel.AtMostOnce,
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            2 => MqttQualityOfServiceLevel.ExactlyOnce,
            _ => throw new InvalidOperationException("Scenario configuration value 'qos' must be 0, 1, or 2.")
        };
    }

    private static byte[] ReadPayload(IReadOnlyDictionary<string, JsonElement> configuration)
    {
        if (!configuration.TryGetValue("payload", out var payload) ||
            payload.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        var encoding = ScenarioStepConfigurationReader.ReadString(configuration, "payloadEncoding") ?? "utf8";
        return encoding.Trim().ToLowerInvariant() switch
        {
            "utf8" or "text" => ReadUtf8Payload(payload),
            "json" => JsonSerializer.SerializeToUtf8Bytes(payload),
            "base64" => Convert.FromBase64String(ReadStringPayload(payload)),
            "bytes" => ReadBytePayload(payload),
            _ => throw new InvalidOperationException(
                "Scenario configuration value 'payloadEncoding' must be utf8, text, json, base64, or bytes.")
        };
    }

    private static byte[] ReadUtf8Payload(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.String)
        {
            return Encoding.UTF8.GetBytes(payload.GetString() ?? string.Empty);
        }

        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    private static string ReadStringPayload(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Scenario payload must be a string when payloadEncoding is base64.");
        }

        return payload.GetString() ?? string.Empty;
    }

    private static byte[] ReadBytePayload(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Scenario payload must be an array when payloadEncoding is bytes.");
        }

        return payload
            .EnumerateArray()
            .Select(ReadByte)
            .ToArray();
    }

    private static byte ReadByte(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetByte(out var result))
        {
            throw new InvalidOperationException("Scenario byte payload values must be between 0 and 255.");
        }

        return result;
    }
}
