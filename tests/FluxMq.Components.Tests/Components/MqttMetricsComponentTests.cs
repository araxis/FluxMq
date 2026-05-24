using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.MqttMetrics;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttMetricsComponentTests
{
    [Fact]
    public async Task Input_UpdatesMetricsAndPublishesSnapshots()
    {
        var start = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        var component = new MqttMetricsComponent();
        var snapshots = new List<MqttMetricsSnapshot>();
        var sink = new ActionBlock<MqttMetricsSnapshot>(snapshots.Add);

        component.Snapshots.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/1", [1, 2], start, retain: true));
        component.Input.Post(Message("factory/2", [1, 2, 3, 4], start.AddSeconds(1)));
        component.Input.Post(Message("factory/1", [], start.AddSeconds(2)));
        component.Complete();

        await Task.WhenAll(component.Completion, sink.Completion);

        snapshots.Count.ShouldBe(3);
        var current = component.Current;
        current.MessageCount.ShouldBe(3);
        current.TotalPayloadBytes.ShouldBe(6);
        current.MinPayloadBytes.ShouldBe(0);
        current.MaxPayloadBytes.ShouldBe(4);
        current.AveragePayloadBytes.ShouldBe(2);
        current.RetainedMessageCount.ShouldBe(1);
        current.UniqueTopicCount.ShouldBe(2);
        current.LastTopic.ShouldBe("factory/1");
        current.LastReceivedAt.ShouldBe(start.AddSeconds(2));
        current.TopicCounts.ShouldBe(
        [
            new MqttTopicMetric("factory/1", 2),
            new MqttTopicMetric("factory/2", 1)
        ]);
    }

    [Fact]
    public async Task Current_BeforeMessages_ReturnsEmptySnapshot()
    {
        var component = new MqttMetricsComponent();

        component.Complete();
        await component.Completion;

        component.Current.ShouldBe(new MqttMetricsSnapshot());
    }

    [Fact]
    public async Task ProcessingFailure_PublishesErrorAndKeepsProcessing()
    {
        var component = new MqttMetricsComponent();
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/ok", [1]));
        component.Input.Post(new MqttEnvelope { Topic = "factory/bad", Payload = null! });
        component.Input.Post(Message("factory/next", [1, 2]));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        component.Current.MessageCount.ShouldBe(2);
        component.Current.TotalPayloadBytes.ShouldBe(3);

        var error = errors.ShouldHaveSingleItem();
        error.NodeId.ShouldBe(component.Id);
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Message.ShouldBe("MQTT metrics update failed.");
        error.Context.ShouldBe("factory/bad");
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttMetricsComponent(FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("metrics failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("metrics failed");
        await errorSink.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("MQTT metrics observer faulted.");
    }

    private static MqttEnvelope Message(
        string topic,
        byte[] payload,
        DateTimeOffset? receivedAt = null,
        bool retain = false) => new()
        {
            Topic = topic,
            Payload = payload,
            ReceivedAt = receivedAt ?? DateTimeOffset.UtcNow,
            Retain = retain
        };
}
