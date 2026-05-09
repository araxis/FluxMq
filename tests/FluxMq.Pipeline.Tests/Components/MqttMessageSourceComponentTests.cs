using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class MqttMessageSourceComponentTests
{
    [Fact]
    public async Task StartAsync_EmitsSessionMessagesInOrder()
    {
        var session = new FakeMqttSession();
        var component = new MqttMessageSourceComponent(session);
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var producer = component.StartAsync();
        await session.WriteAsync(Message("factory/1"));
        await session.WriteAsync(Message("factory/2"));
        await session.WriteAsync(Message("factory/3"));
        session.CompleteMessages();

        await Task.WhenAll(producer, sink.Completion);

        received.Should().Equal("factory/1", "factory/2", "factory/3");
        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_CompletesWhenSessionReaderCompletes()
    {
        var session = new FakeMqttSession();
        var component = new MqttMessageSourceComponent(session);
        var sink = new ActionBlock<MqttEnvelope>(_ => { });

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var producer = component.StartAsync();
        session.CompleteMessages();

        await Task.WhenAll(producer, sink.Completion);

        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_PublishesErrorWhenSessionReaderFails()
    {
        var session = new FakeMqttSession();
        var component = new MqttMessageSourceComponent(session);
        var errors = new List<FlowError>();
        var messages = new List<MqttEnvelope>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var messageSink = new ActionBlock<MqttEnvelope>(messages.Add);
        var failure = new InvalidOperationException("session reader failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Output.LinkTo(messageSink, new DataflowLinkOptions { PropagateCompletion = true });

        var producer = component.StartAsync();
        await session.WriteAsync(Message("factory/1"));
        session.CompleteMessages(failure);

        await Task.WhenAll(producer, errorSink.Completion, messageSink.Completion);

        messages.Should().ContainSingle().Which.Topic.Should().Be("factory/1");

        var error = errors.Should().ContainSingle().Subject;
        error.NodeId.Should().Be(component.Id);
        error.Code.Should().Be(FlowErrorCodes.ProcessingFailed);
        error.Message.Should().Be("MQTT message source failed.");
        error.Context.Should().Be("test");
    }

    [Fact]
    public async Task Complete_CancelsSourceAndCompletesOutput()
    {
        var component = new MqttMessageSourceComponent(new FakeMqttSession());
        var sink = new ActionBlock<MqttEnvelope>(_ => { });

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var producer = component.StartAsync();
        component.Complete();

        await Task.WhenAll(producer, sink.Completion);
        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttMessageSourceComponent(new FakeMqttSession(), id: FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("message source faulted");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("message source faulted");
        await errorSink.Completion;

        errors.Should().ContainSingle().Which.Code.Should().Be(FlowErrorCodes.NodeFaulted);
    }

    [Fact]
    public async Task Fault_AfterStart_StopsProducer()
    {
        var component = new MqttMessageSourceComponent(new FakeMqttSession());
        var failure = new InvalidOperationException("source failed");

        var producer = component.StartAsync();
        component.Fault(failure);

        await producer.WaitAsync(TimeSpan.FromSeconds(5));

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("source failed");
    }

    [Fact]
    public async Task StartAsync_ConnectsAndSubscribesWhenConfigured()
    {
        var session = new FakeMqttSession();
        var component = new MqttMessageSourceComponent(
            session,
            subscriptions:
            [
                new MqttSubscription("factory/#", MqttQualityOfServiceLevel.AtLeastOnce)
            ],
            connectOnStart: true);

        var producer = component.StartAsync();
        session.CompleteMessages();
        await producer;

        session.ConnectCalls.Should().Be(1);
        session.Subscriptions.Should().ContainSingle(subscription =>
            subscription.TopicFilter == "factory/#" &&
            subscription.QualityOfService == MqttQualityOfServiceLevel.AtLeastOnce);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedSession()
    {
        var session = new FakeMqttSession();
        var component = new MqttMessageSourceComponent(
            session,
            disposeSessionOnDispose: true);

        await component.DisposeAsync();

        session.DisposeCalls.Should().Be(1);
    }

    private static MqttEnvelope Message(string topic) => new()
    {
        Topic = topic,
        Payload = []
    };

    private sealed class FakeMqttSession : IMqttSession
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
        private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];

        public MqttConnectionProfile Profile { get; } = new() { Name = "test" };
        public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;

        public event EventHandler<MqttSessionState>? StateChanged
        {
            add { }
            remove { }
        }

        public async Task WriteAsync(MqttEnvelope message)
        {
            await _messages.Writer.WriteAsync(message);
        }

        public void CompleteMessages(Exception? exception = null)
        {
            _messages.Writer.Complete(exception);
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            State = MqttSessionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttSessionState.Disconnected;
            _messages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
        {
            _subscriptions.Add((topicFilter, qos));
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;
        public Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
