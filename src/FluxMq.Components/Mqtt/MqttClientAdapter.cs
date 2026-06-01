using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.MessageSource;
using FluxFlow.Components.Mqtt.Contracts;
using FluxFlow.Components.Mqtt.Options;
using FluxFlow.Engine.Components;
using MQTTnet.Protocol;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Mqtt;

using ComponentMqttQualityOfService = FluxFlow.Components.Mqtt.Contracts.MqttQualityOfService;

internal sealed class MqttClientAdapterFactory(Func<MqttClientAdapter> adapterFactory) : IMqttClientFactory
{
    public ValueTask<MqttClientLease> CreateAsync(
        MqttClientFactoryContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(MqttClientLease.Shared(adapterFactory()));
}

internal sealed class MqttClientAdapter(
    IMqttBrokerClient client,
    ISourceBlock<MqttEnvelope>? messages = null)
    : IMqttClientAdapter
{
    private readonly IMqttBrokerClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ISourceBlock<MqttEnvelope>? _messages = messages;

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

    public async ValueTask<IMqttSubscription> SubscribeAsync(
        MqttSubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var topicFilter = options.TopicFilter ?? string.Empty;
        await ConnectIfNeededAsync(cancellationToken).ConfigureAwait(false);
        await _client.SubscribeAsync(
                topicFilter,
                ToBrokerQualityOfService(options.QualityOfService),
                options.ReceiveRetainedMessages,
                options.RetainAsPublished,
                cancellationToken)
            .ConfigureAwait(false);

        if (_messages is null)
        {
            return new ChannelMqttEnvelopeSubscription(_client.Messages, topicFilter);
        }

        var buffer = new BufferBlock<MqttEnvelope>(
            new DataflowBlockOptions { BoundedCapacity = options.BoundedCapacity });
        var link = _messages.LinkTo(
            buffer,
            new DataflowLinkOptions { PropagateCompletion = true },
            envelope => MqttTopicFilterMatcher.IsMatch(topicFilter, envelope.Topic));

        return new DataflowMqttEnvelopeSubscription(buffer, link);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

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
            PayloadPreview = FlowEventPayloadPreview.FromBytes(envelope.Payload),
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

    private sealed class ChannelMqttEnvelopeSubscription(
        ChannelReader<MqttEnvelope> messages,
        string topicFilter)
        : IMqttSubscription
    {
        public IAsyncEnumerable<MqttReceivedMessage> Messages => ReadMessagesAsync();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async IAsyncEnumerable<MqttReceivedMessage> ReadMessagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var envelope in messages.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (MqttTopicFilterMatcher.IsMatch(topicFilter, envelope.Topic))
                {
                    yield return ToReceivedMessage(envelope);
                }
            }
        }
    }

    private sealed class DataflowMqttEnvelopeSubscription(
        BufferBlock<MqttEnvelope> buffer,
        IDisposable link)
        : IMqttSubscription
    {
        public IAsyncEnumerable<MqttReceivedMessage> Messages => ReadMessagesAsync();

        public ValueTask DisposeAsync()
        {
            link.Dispose();
            buffer.Complete();
            return ValueTask.CompletedTask;
        }

        private async IAsyncEnumerable<MqttReceivedMessage> ReadMessagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (await buffer.OutputAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                while (buffer.TryReceive(out var envelope))
                {
                    yield return ToReceivedMessage(envelope);
                }
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
