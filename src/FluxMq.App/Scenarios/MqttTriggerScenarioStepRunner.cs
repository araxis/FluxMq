using FluxMq.Components.MessageSource;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Components;
using FluxMq.Scenarios;
using MQTTnet.Protocol;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Scenarios;

public sealed class MqttTriggerScenarioStepRunner : IScenarioStepRunner
{
    public const string StepType = ScenarioStepTypes.MqttTrigger;

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
        var subscriptions = ReadSubscriptions(configuration);

        var clientFactory = context.Services.GetRequired<IMqttScenarioClientFactory>();
        var client = clientFactory.CreateClient(connectionName);
        var connection = new MqttConnectionComponent(client);
        var trigger = new MqttTriggerComponent(connection, subscriptions);
        var runtime = new MqttTriggerScenarioRuntime(connection, trigger, context.Events);

        try
        {
            await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await runtime.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        context.Lifetime.Register(runtime);

        return new ScenarioStepResult
        {
            Name = context.StepName,
            Type = context.Step.Type,
            Status = ScenarioStepRunStatus.Passed,
            StartedAt = startedAt,
            FinishedAt = DateTimeOffset.UtcNow,
            Message = $"Started MQTT trigger for '{FormatSubscriptions(subscriptions)}'.",
            NextEventOffset = context.EventOffset
        };
    }

    private static IReadOnlyList<MqttSubscription> ReadSubscriptions(
        IReadOnlyDictionary<string, JsonElement> configuration)
    {
        var topicFilter = ScenarioStepConfigurationReader.ReadRequiredString(
            configuration,
            ScenarioStepConfigurationKeys.Subscriptions);
        var qualityOfService = ReadQualityOfService(configuration, defaultValue: 1);
        var receiveRetainedMessages = ScenarioStepConfigurationReader.ReadBoolOrDefault(
            configuration,
            ScenarioStepConfigurationKeys.ReceiveRetained,
            false);
        var retainAsPublished = ScenarioStepConfigurationReader.ReadBoolOrDefault(
            configuration,
            ScenarioStepConfigurationKeys.RetainAsPublished,
            true);

        return
        [
            new MqttSubscription(
                topicFilter,
                qualityOfService,
                receiveRetainedMessages,
                retainAsPublished)
        ];
    }

    private static MqttQualityOfServiceLevel ReadQualityOfService(
        IReadOnlyDictionary<string, JsonElement> configuration,
        int defaultValue)
    {
        var value = ScenarioStepConfigurationReader.ReadIntOrDefault(
            configuration,
            ScenarioStepConfigurationKeys.Qos,
            defaultValue,
            0);
        return value switch
        {
            0 => MqttQualityOfServiceLevel.AtMostOnce,
            1 => MqttQualityOfServiceLevel.AtLeastOnce,
            2 => MqttQualityOfServiceLevel.ExactlyOnce,
            _ => throw new InvalidOperationException("Scenario configuration value 'qos' must be 0, 1, or 2.")
        };
    }

    private static string FormatSubscriptions(IReadOnlyList<MqttSubscription> subscriptions)
        => string.Join(", ", subscriptions.Select(static subscription => subscription.TopicFilter));

    private sealed class MqttTriggerScenarioRuntime(
        MqttConnectionComponent connection,
        MqttTriggerComponent trigger,
        ScenarioEventJournal events)
        : IAsyncDisposable
    {
        private readonly ActionBlock<FlowEvent> _eventSink = new(events.Append);
        private readonly ActionBlock<MqttEnvelope> _outputSink = new(static _ => { });
        private readonly ActionBlock<FlowError> _errorSink = new(static _ => { });
        private IDisposable? _eventLink;
        private IDisposable? _outputLink;
        private IDisposable? _connectionErrorLink;
        private IDisposable? _triggerErrorLink;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _eventLink = trigger.Events.LinkTo(_eventSink, new DataflowLinkOptions { PropagateCompletion = true });
            _outputLink = trigger.Output.LinkTo(_outputSink, new DataflowLinkOptions { PropagateCompletion = true });
            _connectionErrorLink = connection.Errors.LinkTo(_errorSink);
            _triggerErrorLink = trigger.Errors.LinkTo(_errorSink);

            await connection.StartAsync(cancellationToken).ConfigureAwait(false);
            await trigger.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _eventLink?.Dispose();
            _outputLink?.Dispose();
            _connectionErrorLink?.Dispose();
            _triggerErrorLink?.Dispose();

            await trigger.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);

            _eventSink.Complete();
            _outputSink.Complete();
            _errorSink.Complete();

            await Task.WhenAll(
                    _eventSink.Completion,
                    _outputSink.Completion,
                    _errorSink.Completion)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
