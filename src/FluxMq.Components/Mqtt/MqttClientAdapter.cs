using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.MessageSource;
using FluxFlow.Components.Mqtt.Contracts;
using FluxFlow.Components.Mqtt.Options;
using MQTTnet.Protocol;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Mqtt;

using ComponentConnectionProfile = FluxFlow.Components.Mqtt.Options.MqttConnectionProfile;
using ComponentMqttQualityOfService = FluxFlow.Components.Mqtt.Contracts.MqttQualityOfService;

internal sealed class MqttClientAdapterFactory(Func<MqttClientAdapter> adapterFactory) : IMqttClientFactory
{
    public ValueTask<IMqttClientAdapter> CreateAsync(
        ComponentConnectionProfile connection,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IMqttClientAdapter>(adapterFactory());
}

internal sealed class MqttClientAdapter(
    IMqttBrokerClient client,
    ISourceBlock<MqttEnvelope>? messages = null,
    IReadOnlyList<MqttSubscription>? subscriptions = null,
    bool disposeClientOnDispose = false)
    : IMqttClientAdapter
{
    private readonly IMqttBrokerClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ISourceBlock<MqttEnvelope>? _messages = messages;
    private readonly IReadOnlyList<MqttSubscription> _subscriptions = subscriptions ?? [];
    private readonly bool _disposeClientOnDispose = disposeClientOnDispose;

    public async ValueTask PublishAsync(
        MqttPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await ConnectIfNeededAsync(cancellationToken).ConfigureAwait(false);
            await _client.PublishAsync(
                    request.Topic ?? string.Empty,
                    request.Payload,
                    ToBrokerQualityOfService(request.QualityOfService ?? ComponentMqttQualityOfService.AtMostOnce),
                    request.Retain ?? false,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new MqttClientAdapterException(
                exception.Message,
                request.Topic,
                request.CorrelationId,
                exception);
        }
    }

    public async IAsyncEnumerable<MqttReceivedMessage> SubscribeAsync(
        MqttSubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var topicFilter = options.TopicFilter ?? string.Empty;
        var subscription = FindSubscription(topicFilter);
        await ConnectIfNeededAsync(cancellationToken).ConfigureAwait(false);
        await _client.SubscribeAsync(
                topicFilter,
                subscription?.QualityOfService ?? ToBrokerQualityOfService(options.QualityOfService),
                subscription?.ReceiveRetainedMessages ?? true,
                subscription?.RetainAsPublished ?? true,
                cancellationToken)
            .ConfigureAwait(false);

        if (_messages is null)
        {
            await foreach (var envelope in ReadFromChannelAsync(_client.Messages, topicFilter, cancellationToken)
                .ConfigureAwait(false))
            {
                yield return ToReceivedMessage(envelope);
            }

            yield break;
        }

        var buffer = new BufferBlock<MqttEnvelope>();
        using var link = _messages.LinkTo(
            buffer,
            new DataflowLinkOptions { PropagateCompletion = true },
            envelope => MqttTopicFilterMatcher.IsMatch(topicFilter, envelope.Topic));

        try
        {
            while (await buffer.OutputAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                while (buffer.TryReceive(out var envelope))
                {
                    yield return ToReceivedMessage(envelope);
                }
            }
        }
        finally
        {
            buffer.Complete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposeClientOnDispose)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static ComponentMqttQualityOfService ToComponentQualityOfService(MqttQualityOfServiceLevel qualityOfService)
        => qualityOfService switch
        {
            MqttQualityOfServiceLevel.AtMostOnce => ComponentMqttQualityOfService.AtMostOnce,
            MqttQualityOfServiceLevel.AtLeastOnce => ComponentMqttQualityOfService.AtLeastOnce,
            MqttQualityOfServiceLevel.ExactlyOnce => ComponentMqttQualityOfService.ExactlyOnce,
            _ => ComponentMqttQualityOfService.AtMostOnce
        };

    internal static MqttQualityOfServiceLevel ToBrokerQualityOfService(ComponentMqttQualityOfService qualityOfService)
        => qualityOfService switch
        {
            ComponentMqttQualityOfService.AtMostOnce => MqttQualityOfServiceLevel.AtMostOnce,
            ComponentMqttQualityOfService.AtLeastOnce => MqttQualityOfServiceLevel.AtLeastOnce,
            ComponentMqttQualityOfService.ExactlyOnce => MqttQualityOfServiceLevel.ExactlyOnce,
            _ => MqttQualityOfServiceLevel.AtMostOnce
        };

    internal static MqttReceivedMessage ToReceivedMessage(MqttEnvelope envelope)
        => new()
        {
            Timestamp = envelope.ReceivedAt,
            Topic = envelope.Topic,
            Payload = envelope.Payload,
            QualityOfService = ToComponentQualityOfService(envelope.QualityOfService),
            Retain = envelope.Retain
        };

    internal static MqttEnvelope ToEnvelope(MqttReceivedMessage message)
        => new()
        {
            Topic = message.Topic,
            Payload = message.Payload,
            ReceivedAt = message.Timestamp,
            QualityOfService = ToBrokerQualityOfService(message.QualityOfService),
            Retain = message.Retain
        };

    private async Task ConnectIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_client.State is not MqttClientState.Connected)
        {
            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private MqttSubscription? FindSubscription(string topicFilter)
        => _subscriptions.FirstOrDefault(subscription =>
            string.Equals(subscription.TopicFilter, topicFilter, StringComparison.Ordinal));

    private static async IAsyncEnumerable<MqttEnvelope> ReadFromChannelAsync(
        ChannelReader<MqttEnvelope> messages,
        string topicFilter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var envelope in messages.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (MqttTopicFilterMatcher.IsMatch(topicFilter, envelope.Topic))
            {
                yield return envelope;
            }
        }
    }
}

internal sealed class MqttClientAdapterException(
    string message,
    string? topic,
    string? correlationId,
    Exception innerException)
    : Exception(message, innerException)
{
    public string? Topic { get; } = topic;
    public string? CorrelationId { get; } = correlationId;
}
