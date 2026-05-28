using Shouldly;
using FluxMq.Core.Models;
using FluxMq.Components.MessageSource;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttTriggerComponentTests
{
    [Fact]
    public async Task StartAsync_InstallsSubscriptionsAndForwardsMatchingMessages()
    {
        var session = new TestFluxMqttClient();
        var connection = new MqttConnectionComponent(session, disposeClientOnDispose: false);
        var trigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription(
                "sensors/+",
                MqttQualityOfServiceLevel.AtLeastOnce,
                ReceiveRetainedMessages: false,
                RetainAsPublished: true)
        ]);

        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(envelope => received.Add(envelope.Topic));
        trigger.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await connection.StartAsync();
        await trigger.StartAsync();

        await session.WriteAsync(TestFluxMqttClient.Message("sensors/temp"));
        await session.WriteAsync(TestFluxMqttClient.Message("lights/kitchen"));    // filtered out
        await session.WriteAsync(TestFluxMqttClient.Message("sensors/humidity"));
        session.CompleteMessages();

        await sink.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        received.ShouldBe(new[] { "sensors/temp", "sensors/humidity" });
        var sub = session.Subscriptions.ShouldHaveSingleItem();
        sub.ShouldBe(("sensors/+", MqttQualityOfServiceLevel.AtLeastOnce));
        var options = session.SubscriptionOptions.ShouldHaveSingleItem();
        options.ReceiveRetainedMessages.ShouldBeFalse();
        options.RetainAsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_EmitsReceiveEventsForForwardedMessages()
    {
        var session = new TestFluxMqttClient();
        var connection = new MqttConnectionComponent(session, disposeClientOnDispose: false);
        var trigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription("sensors/#", MqttQualityOfServiceLevel.AtMostOnce)
        ]);
        var events = new List<FlowEvent>();
        var eventSink = new ActionBlock<FlowEvent>(events.Add);
        var outputSink = new ActionBlock<MqttEnvelope>(_ => { });

        trigger.Events.LinkTo(eventSink, new DataflowLinkOptions { PropagateCompletion = true });
        trigger.Output.LinkTo(outputSink, new DataflowLinkOptions { PropagateCompletion = true });

        await connection.StartAsync();
        await trigger.StartAsync();

        await session.WriteAsync(new MqttEnvelope
        {
            Topic = "sensors/temp",
            Payload = Encoding.UTF8.GetBytes("12"),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        await session.WriteAsync(TestFluxMqttClient.Message("lights/kitchen"));
        session.CompleteMessages();

        await Task.WhenAll(eventSink.Completion, outputSink.Completion).WaitAsync(TimeSpan.FromSeconds(5));

        var flowEvent = events.ShouldHaveSingleItem();
        flowEvent.Type.ShouldBe(FlowEventTypes.MqttMessageReceived);
        flowEvent.Source.ShouldBe("MqttTrigger");
        flowEvent.SourceNodeId.ShouldBe(trigger.Id);
        flowEvent.Topic.ShouldBe("sensors/temp");
        flowEvent.PayloadBytes.ShouldBe(2);
        flowEvent.PayloadPreview.ShouldBe("12");
        flowEvent.GetAttribute("qos").ShouldBe("1");
        flowEvent.GetAttribute("retain").ShouldBe("True");
    }

    [Fact]
    public async Task TwoTriggersSharingAConnection_EachReceiveOnlyTheirTopics()
    {
        var session = new TestFluxMqttClient();
        var connection = new MqttConnectionComponent(session, disposeClientOnDispose: false);
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

        await session.WriteAsync(TestFluxMqttClient.Message("sensors/temp"));
        await session.WriteAsync(TestFluxMqttClient.Message("$SYS/broker/uptime"));
        await session.WriteAsync(TestFluxMqttClient.Message("lights/kitchen"));
        session.CompleteMessages();

        await Task.WhenAll(sensorSink.Completion, sysSink.Completion).WaitAsync(TimeSpan.FromSeconds(5));

        sensorMessages.ShouldHaveSingleItem().ShouldBe("sensors/temp");
        sysMessages.ShouldHaveSingleItem().ShouldBe("$SYS/broker/uptime");

        session.Subscriptions.Select(s => s.TopicFilter)
            .ShouldBe(new[] { "sensors/#", "$SYS/#" }, ignoreOrder: true);
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneSubscription()
    {
        var session = new TestFluxMqttClient();
        var connection = new MqttConnectionComponent(session, disposeClientOnDispose: false);
        var act = () => new MqttTriggerComponent(connection, []);
        var ex = Should.Throw<ArgumentException>(act);
        ex.Message.ShouldContain("at least one subscription");
    }

    [Fact]
    public async Task DisposeAsync_StopsTheTrigger()
    {
        var session = new TestFluxMqttClient();
        var connection = new MqttConnectionComponent(session, disposeClientOnDispose: false);
        var trigger = new MqttTriggerComponent(connection,
        [
            new MqttSubscription("#", MqttQualityOfServiceLevel.AtMostOnce)
        ]);
        var sink = new ActionBlock<MqttEnvelope>(_ => { });
        trigger.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await connection.StartAsync();
        await trigger.StartAsync();
        await trigger.DisposeAsync();

        trigger.Completion.IsCompleted.ShouldBeTrue();
    }
}
