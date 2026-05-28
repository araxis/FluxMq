using Shouldly;
using FluxMq.Components.MessageSource;
using FluxMq.Core.Mqtt;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttConnectionComponentTests
{
    [Fact]
    public async Task StartAsync_ConnectsTheUnderlyingSession()
    {
        var session = new TestFluxMqttClient();
        var component = new MqttConnectionComponent(session);

        await component.StartAsync();

        session.ConnectCalls.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_WaitsForConnectionBeforeReturning()
    {
        var connectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new TestFluxMqttClient(connectDelay: connectGate.Task);
        var component = new MqttConnectionComponent(session);

        var startTask = component.StartAsync();
        await Task.Delay(50);

        startTask.IsCompleted.ShouldBeFalse();
        session.State.ShouldBe(MqttClientState.Connecting);

        connectGate.SetResult();

        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        session.State.ShouldBe(MqttClientState.Connected);
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        var session = new TestFluxMqttClient();
        var component = new MqttConnectionComponent(session);

        await component.StartAsync();
        await component.StartAsync();

        session.ConnectCalls.ShouldBe(1);
    }

    [Fact]
    public void Client_ExposesUnderlyingClient()
    {
        var session = new TestFluxMqttClient();
        var component = new MqttConnectionComponent(session);

        component.Client.ShouldBeSameAs(session);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedSession()
    {
        var session = new TestFluxMqttClient();
        var component = new MqttConnectionComponent(session, disposeClientOnDispose: true);

        await component.DisposeAsync();

        session.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_LeavesSessionAlone_WhenNotOwned()
    {
        var session = new TestFluxMqttClient();
        var component = new MqttConnectionComponent(session, disposeClientOnDispose: false);

        await component.DisposeAsync();

        session.DisposeCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttConnectionComponent(new TestFluxMqttClient());
        var errors = new List<FlowError>();
        var sink = new ActionBlock<FlowError>(errors.Add);
        component.Errors.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        var failure = new InvalidOperationException("boom");
        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("boom");
        await sink.Completion;

        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.NodeFaulted);
    }
}
