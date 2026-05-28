using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttPublisher;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttPublisherComponentTests
{
    [Fact]
    public async Task Input_PublishesMessagesToSession()
    {
        var session = new FakeFluxMqttClient();
        var component = new MqttPublisherComponent(session);

        component.Input.Post(new MqttPublishRequest
        {
            Topic = "factory/command",
            Payload = [1, 2, 3],
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        component.Complete();

        await component.Completion;

        var publish = session.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("factory/command");
        publish.Payload.ShouldBe(new byte[] { 1, 2, 3 });
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        publish.Retain.ShouldBeTrue();
    }

    [Fact]
    public async Task Input_EmitsPublishLogEntry()
    {
        var session = new FakeFluxMqttClient();
        var component = new MqttPublisherComponent(session);
        var entries = new BufferBlock<FlowLogEntry>();

        component.Entries.LinkTo(entries, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttPublishRequest
        {
            Topic = "factory/logged",
            Payload = "hello"u8.ToArray()
        });
        component.Complete();

        var entry = await entries.ReceiveAsync();
        await component.Completion;

        entry.Severity.ShouldBe(FlowLogSeverity.Info);
        entry.Source.ShouldBe("MqttPublisher");
        entry.Topic.ShouldBe("factory/logged");
        entry.PayloadBytes.ShouldBe(5);
        component.PublishedCount.ShouldBe(1);
        component.LastPublishedTopic.ShouldBe("factory/logged");
    }

    [Fact]
    public async Task Input_EmitsPublishEvent()
    {
        var session = new FakeFluxMqttClient();
        var component = new MqttPublisherComponent(session);
        var events = new BufferBlock<FlowEvent>();

        component.Events.LinkTo(events, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttPublishRequest
        {
            Topic = "factory/event",
            Payload = "12"u8.ToArray(),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        component.Complete();

        var flowEvent = await events.ReceiveAsync();
        await component.Completion;

        flowEvent.Type.ShouldBe(FlowEventTypes.MqttMessagePublished);
        flowEvent.Source.ShouldBe("MqttPublisher");
        flowEvent.SourceNodeId.ShouldBe(component.Id);
        flowEvent.Topic.ShouldBe("factory/event");
        flowEvent.PayloadBytes.ShouldBe(2);
        flowEvent.PayloadPreview.ShouldBe("12");
        flowEvent.GetAttribute("qos").ShouldBe("1");
        flowEvent.GetAttribute("retain").ShouldBe("True");
    }

    [Fact]
    public async Task Input_EmitsPublishEventWithoutPreviewForBinaryPayload()
    {
        var session = new FakeFluxMqttClient();
        var component = new MqttPublisherComponent(session);
        var events = new BufferBlock<FlowEvent>();

        component.Events.LinkTo(events, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(new MqttPublishRequest
        {
            Topic = "factory/binary",
            Payload = [0xff, 0xfe, 0xfd]
        });
        component.Complete();

        var flowEvent = await events.ReceiveAsync();
        await component.Completion;

        flowEvent.PayloadBytes.ShouldBe(3);
        flowEvent.PayloadPreview.ShouldBeNull();
    }

    [Fact]
    public async Task Input_PreservesPublishOrder()
    {
        var session = new FakeFluxMqttClient();
        var component = new MqttPublisherComponent(session);

        component.Input.Post(new MqttPublishRequest { Topic = "factory/1", Payload = [] });
        component.Input.Post(new MqttPublishRequest { Topic = "factory/2", Payload = [] });
        component.Input.Post(new MqttPublishRequest { Topic = "factory/3", Payload = [] });
        component.Complete();

        await component.Completion;

        session.Published.Select(message => message.Topic)
            .ShouldBe(new[] { "factory/1", "factory/2", "factory/3" });
    }

    [Fact]
    public async Task PublishFailure_PublishesErrorAndKeepsProcessing()
    {
        var session = new FakeFluxMqttClient(topicToFail: "factory/fail");
        var component = new MqttPublisherComponent(session);
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttPublishRequest { Topic = "factory/ok-1", Payload = [] });
        component.Input.Post(new MqttPublishRequest { Topic = "factory/fail", Payload = [] });
        component.Input.Post(new MqttPublishRequest { Topic = "factory/ok-2", Payload = [] });
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        session.Published.Select(message => message.Topic)
            .ShouldBe(new[] { "factory/ok-1", "factory/ok-2" });

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Context.ShouldBe("factory/fail");
        error.NodeId.ShouldBe(component.Id);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttPublisherComponent(new FakeFluxMqttClient(), FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("publisher failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("publisher failed");
        await errorSink.Completion;

        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.NodeFaulted);
    }

    private sealed class FakeFluxMqttClient(string? topicToFail = null) : IFluxMqttClient
    {
        public MqttConnectionProfile Profile { get; } = new() { Name = "test" };
        public MqttClientState State { get; private set; } = MqttClientState.Connected;
        public ChannelReader<MqttEnvelope> Messages { get; } = Channel.CreateUnbounded<MqttEnvelope>().Reader;
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttClientState>? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default) => Task.CompletedTask;
        public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos, bool receiveRetainedMessages, bool retainAsPublished = true, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;

        public Task PublishAsync(
            string topic,
            byte[] payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken ct = default)
        {
            if (topic == topicToFail)
            {
                throw new InvalidOperationException("publish failed");
            }

            Published.Add(new PublishedMessage(topic, payload, qos, retain));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public sealed record PublishedMessage(
            string Topic,
            byte[] Payload,
            MqttQualityOfServiceLevel QualityOfService,
            bool Retain);
    }
}
