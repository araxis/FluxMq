using FluentAssertions;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components.MessageSource;
using MQTTnet.Protocol;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class MqttTriggerComponentTests
{
    [Fact]
    public async Task StartAsync_InstallsSubscriptionsAndForwardsMatchingMessages()
    {
        var session = new TestMqttSession();
        var connection = new MqttConnectionComponent(session, disposeSessionOnDispose: false);
        var trigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription("sensors/+", MqttQualityOfServiceLevel.AtLeastOnce)
        ]);

        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(envelope => received.Add(envelope.Topic));
        trigger.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await connection.StartAsync();
        await trigger.StartAsync();

        await session.WriteAsync(TestMqttSession.Message("sensors/temp"));
        await session.WriteAsync(TestMqttSession.Message("lights/kitchen"));    // filtered out
        await session.WriteAsync(TestMqttSession.Message("sensors/humidity"));
        session.CompleteMessages();

        await sink.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        received.Should().Equal("sensors/temp", "sensors/humidity");
        session.Subscriptions.Should().ContainSingle()
            .Which.Should().Be(("sensors/+", MqttQualityOfServiceLevel.AtLeastOnce));
    }

    [Fact]
    public async Task TwoTriggersSharingAConnection_EachReceiveOnlyTheirTopics()
    {
        var session = new TestMqttSession();
        var connection = new MqttConnectionComponent(session, disposeSessionOnDispose: false);
        var sensorsTrigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription("sensors/#", MqttQualityOfServiceLevel.AtMostOnce)
        ]);
        var sysTrigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription("$SYS/#", MqttQualityOfServiceLevel.AtMostOnce)
        ]);

        var sensorMessages = new List<string>();
        var sysMessages = new List<string>();
        var sensorSink = new ActionBlock<MqttEnvelope>(e => sensorMessages.Add(e.Topic));
        var sysSink = new ActionBlock<MqttEnvelope>(e => sysMessages.Add(e.Topic));
        sensorsTrigger.Output.LinkTo(sensorSink, new DataflowLinkOptions { PropagateCompletion = true });
        sysTrigger.Output.LinkTo(sysSink, new DataflowLinkOptions { PropagateCompletion = true });

        await connection.StartAsync();
        await sensorsTrigger.StartAsync();
        await sysTrigger.StartAsync();

        await session.WriteAsync(TestMqttSession.Message("sensors/temp"));
        await session.WriteAsync(TestMqttSession.Message("$SYS/broker/uptime"));
        await session.WriteAsync(TestMqttSession.Message("lights/kitchen"));
        session.CompleteMessages();

        await Task.WhenAll(sensorSink.Completion, sysSink.Completion).WaitAsync(TimeSpan.FromSeconds(5));

        sensorMessages.Should().ContainSingle().Which.Should().Be("sensors/temp");
        sysMessages.Should().ContainSingle().Which.Should().Be("$SYS/broker/uptime");

        // Each trigger installed its own subscription on the broker.
        session.Subscriptions.Select(s => s.TopicFilter)
            .Should().BeEquivalentTo(["sensors/#", "$SYS/#"]);
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneSubscription()
    {
        var session = new TestMqttSession();
        var connection = new MqttConnectionComponent(session, disposeSessionOnDispose: false);
        var act = () => new MqttTriggerComponent(connection, []);
        act.Should().Throw<ArgumentException>().WithMessage("*at least one subscription*");
    }

    [Fact]
    public async Task DisposeAsync_StopsTheTrigger()
    {
        var session = new TestMqttSession();
        var connection = new MqttConnectionComponent(session, disposeSessionOnDispose: false);
        var trigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription("#", MqttQualityOfServiceLevel.AtMostOnce)
        ]);
        var sink = new ActionBlock<MqttEnvelope>(_ => { });
        trigger.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await connection.StartAsync();
        await trigger.StartAsync();
        await trigger.DisposeAsync();

        trigger.Completion.IsCompleted.Should().BeTrue();
    }
}
