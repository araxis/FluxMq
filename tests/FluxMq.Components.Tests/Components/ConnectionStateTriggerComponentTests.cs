using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.ConnectionStateTrigger;
using FluxFlow.Engine.Components;
using MQTTnet.Protocol;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class ConnectionStateTriggerComponentTests
{
    [Fact]
    public async Task Output_BroadcastsConnectionStateChanges()
    {
        var manager = new FakeConnectionManager();
        var nodeId = FlowNodeId.New();
        using var component = new ConnectionStateTriggerComponent(manager, nodeId);
        var received = new List<MqttClientStateChangedEventArgs>();
        var sink = new ActionBlock<MqttClientStateChangedEventArgs>(received.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var profile = new MqttConnectionProfile
        {
            Id = ConnectionProfileId.New(),
            Name = "Local",
            Host = "localhost",
            Port = 1883
        };

        manager.Emit(profile, MqttClientState.Connected);
        component.Dispose();
        await sink.Completion;

        received.ShouldHaveSingleItem().State.ShouldBe(MqttClientState.Connected);
        component.Id.ShouldBe(nodeId);
        component.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Output_BroadcastsClientStateChanges()
    {
        var profile = new MqttConnectionProfile
        {
            Id = ConnectionProfileId.New(),
            Name = "Local",
            Host = "localhost",
            Port = 1883
        };
        var client = new FakeMqttBrokerClient(profile);
        using var component = new ConnectionStateTriggerComponent(client);
        var received = new List<MqttClientStateChangedEventArgs>();
        var sink = new ActionBlock<MqttClientStateChangedEventArgs>(received.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        client.Emit(MqttClientState.Connected);
        component.Dispose();
        await sink.Completion;

        var state = received.ShouldHaveSingleItem();
        state.Profile.ShouldBeSameAs(profile);
        state.State.ShouldBe(MqttClientState.Connected);
    }

    [Fact]
    public async Task Fault_CompletesWithFailure()
    {
        var manager = new FakeConnectionManager();
        using var component = new ConnectionStateTriggerComponent(manager);
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("state stream failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);
        var act = async () => await component.Completion;

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("state stream failed");
        await errorSink.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("Connection state trigger faulted.");
    }

    private sealed class FakeConnectionManager : IMqttConnectionManager
    {
        public IReadOnlyDictionary<ConnectionProfileId, IMqttBrokerClient> Clients { get; } =
            new Dictionary<ConnectionProfileId, IMqttBrokerClient>();

        public event EventHandler<MqttClientStateChangedEventArgs>? StateChanged;

        public Task<IMqttBrokerClient> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DisconnectAsync(ConnectionProfileId profileId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveAsync(ConnectionProfileId profileId, CancellationToken ct = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Emit(MqttConnectionProfile profile, MqttClientState state)
        {
            StateChanged?.Invoke(this, new MqttClientStateChangedEventArgs(profile.Id, profile, state));
        }
    }

    private sealed class FakeMqttBrokerClient(MqttConnectionProfile profile) : IMqttBrokerClient
    {
        public MqttConnectionProfile Profile { get; } = profile;
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => throw new NotSupportedException();

        public event EventHandler<MqttClientState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos,
            bool receiveRetainedMessages,
            bool retainAsPublished = true,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;

        public Task PublishAsync(
            string topic,
            byte[] payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Emit(MqttClientState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }
    }
}
