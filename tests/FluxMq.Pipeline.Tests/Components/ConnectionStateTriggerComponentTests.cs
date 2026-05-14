using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class ConnectionStateTriggerComponentTests
{
    [Fact]
    public async Task Output_BroadcastsConnectionStateChanges()
    {
        var manager = new FakeConnectionManager();
        var nodeId = FlowNodeId.New();
        using var component = new ConnectionStateTriggerComponent(manager, nodeId);
        var received = new List<SessionStateChangedEventArgs>();
        var sink = new ActionBlock<SessionStateChangedEventArgs>(received.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var profile = new MqttConnectionProfile
        {
            Id = ConnectionProfileId.New(),
            Name = "Local",
            Host = "localhost",
            Port = 1883
        };

        manager.Emit(profile, MqttSessionState.Connected);
        component.Dispose();
        await sink.Completion;

        received.ShouldHaveSingleItem().State.ShouldBe(MqttSessionState.Connected);
        component.Id.ShouldBe(nodeId);
        component.Completion.IsCompletedSuccessfully.ShouldBeTrue();
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
        public IReadOnlyDictionary<ConnectionProfileId, IMqttSession> Sessions { get; } =
            new Dictionary<ConnectionProfileId, IMqttSession>();

        public event EventHandler<SessionStateChangedEventArgs>? StateChanged;

        public Task<IMqttSession> ConnectAsync(MqttConnectionProfile profile, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DisconnectAsync(ConnectionProfileId profileId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveAsync(ConnectionProfileId profileId, CancellationToken ct = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Emit(MqttConnectionProfile profile, MqttSessionState state)
        {
            StateChanged?.Invoke(this, new SessionStateChangedEventArgs(profile.Id, profile, state));
        }
    }
}
