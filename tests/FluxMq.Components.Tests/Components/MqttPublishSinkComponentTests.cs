using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Components.MqttPublishSink;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttPublishSinkComponentTests
{
    [Fact]
    public async Task Input_PublishesMessagesToSession()
    {
        var session = new FakeMqttSession();
        var component = new MqttPublishSinkComponent(session);

        component.Input.Post(new MqttEnvelope
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
    public async Task Input_PreservesPublishOrder()
    {
        var session = new FakeMqttSession();
        var component = new MqttPublishSinkComponent(session);

        component.Input.Post(new MqttEnvelope { Topic = "factory/1", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "factory/2", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "factory/3", Payload = [] });
        component.Complete();

        await component.Completion;

        session.Published.Select(message => message.Topic)
            .ShouldBe(new[] { "factory/1", "factory/2", "factory/3" });
    }

    [Fact]
    public async Task PublishFailure_PublishesErrorAndKeepsProcessing()
    {
        var session = new FakeMqttSession(topicToFail: "factory/fail");
        var component = new MqttPublishSinkComponent(session);
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "factory/ok-1", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "factory/fail", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "factory/ok-2", Payload = [] });
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
        var component = new MqttPublishSinkComponent(new FakeMqttSession(), FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("publish sink failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("publish sink failed");
        await errorSink.Completion;

        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.NodeFaulted);
    }

    private sealed class FakeMqttSession(string? topicToFail = null) : IMqttSession
    {
        public MqttConnectionProfile Profile { get; } = new() { Name = "test" };
        public MqttSessionState State { get; private set; } = MqttSessionState.Connected;
        public ChannelReader<MqttEnvelope> Messages { get; } = Channel.CreateUnbounded<MqttEnvelope>().Reader;
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttSessionState>? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default) => Task.CompletedTask;
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
