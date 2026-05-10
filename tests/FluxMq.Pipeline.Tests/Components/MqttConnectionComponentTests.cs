using FluentAssertions;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Components.MessageSource;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class MqttConnectionComponentTests
{
    [Fact]
    public async Task StartAsync_ConnectsTheUnderlyingSession()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session);

        await component.StartAsync();

        session.ConnectCalls.Should().Be(1);
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session);

        await component.StartAsync();
        await component.StartAsync();

        session.ConnectCalls.Should().Be(1);
    }

    [Fact]
    public void Session_ExposesUnderlyingSession()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session);

        component.Session.Should().BeSameAs(session);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedSession()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session, disposeSessionOnDispose: true);

        await component.DisposeAsync();

        session.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_LeavesSessionAlone_WhenNotOwned()
    {
        var session = new TestMqttSession();
        var component = new MqttConnectionComponent(session, disposeSessionOnDispose: false);

        await component.DisposeAsync();

        session.DisposeCalls.Should().Be(0);
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
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        await sink.Completion;

        errors.Should().ContainSingle().Which.Code.Should().Be(FlowErrorCodes.NodeFaulted);
    }
}
