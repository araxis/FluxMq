using Shouldly;
using FluxMq.Components.MessageSource;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttConnectionComponentTests
{
    [Fact]
    public async Task StartAsync_ConnectsTheUnderlyingSession()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session);

        await component.StartAsync();

        session.ConnectCalls.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session);

        await component.StartAsync();
        await component.StartAsync();

        session.ConnectCalls.ShouldBe(1);
    }

    [Fact]
    public void Session_ExposesUnderlyingSession()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session);

        component.Session.ShouldBeSameAs(session);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedSession()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session, disposeSessionOnDispose: true);

        await component.DisposeAsync();

        session.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeAsync_LeavesSessionAlone_WhenNotOwned()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session, disposeSessionOnDispose: false);

        await component.DisposeAsync();

        session.DisposeCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttConnectionComponent(new TestMqttSession());
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
