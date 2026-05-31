using FluxMq.Components.MqttPublisher;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Components;
using FluxMq.Pipeline.Scenarios;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

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
        var connectionName = ScenarioStepConfigurationReader.ReadRequiredString(
            configuration,
            ScenarioStepConfigurationKeys.Connection);
        var request = new MqttPublishRequest
        {
            Topic = ScenarioStepConfigurationReader.ReadRequiredString(
                configuration,
                ScenarioStepConfigurationKeys.Topic),
            Payload = ReadPayload(configuration),
            QualityOfService = ReadQualityOfService(configuration),
            Retain = ScenarioStepConfigurationReader.ReadBoolOrDefault(
                configuration,
                ScenarioStepConfigurationKeys.Retain,
                false)
        };

        var clientFactory = context.Services.GetRequired<IMqttScenarioClientFactory>();
        await using var client = clientFactory.CreateClient(connectionName);
        if (client.State is not MqttClientState.Connected)
        {
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        var publisher = new MqttPublisherComponent(client);
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var eventSink = new ActionBlock<FlowEvent>(context.Events.Append);
        using var errorLink = publisher.Errors.LinkTo(
            errorSink,
            new DataflowLinkOptions { PropagateCompletion = true });
        using var eventLink = publisher.Events.LinkTo(
            eventSink,
            new DataflowLinkOptions { PropagateCompletion = true });

        if (!await publisher.Input.SendAsync(request, cancellationToken).ConfigureAwait(false))
        {
            return Failed(
                context,
                startedAt,
                "MQTT publisher did not accept the scenario publish request.");
        }

        publisher.Complete();
        await publisher.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        await errorSink.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        await eventSink.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (errors.FirstOrDefault() is { } error)
        {
            var message = error.Exception is null
                ? error.Message
                : $"{error.Message} {error.Exception.Message}";
            return Failed(context, startedAt, message);
        }

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

    private static ScenarioStepResult Failed(
        ScenarioStepRunContext context,
        DateTimeOffset startedAt,
        string message)
        => new()
        {
            Name = context.StepName,
            Type = context.Step.Type,
            Status = ScenarioStepRunStatus.Failed,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Message = message,
            NextEventOffset = context.EventOffset
        };

    private static MqttQualityOfServiceLevel ReadQualityOfService(
        IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var value = ScenarioStepConfigurationReader.ReadIntOrDefault(
            configuration,
            ScenarioStepConfigurationKeys.Qos,
            0,
            0);
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
        if (!configuration.TryGetValue(ScenarioStepConfigurationKeys.Payload, out var payload) ||
            payload.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        var encoding = ScenarioStepConfigurationReader.ReadString(
            configuration,
            ScenarioStepConfigurationKeys.PayloadEncoding) ?? "utf8";
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
