using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class MqttMetricsSinkComponentTests
{
    [Fact]
    public async Task Input_UpdatesMetricsAndPublishesSnapshots()
    {
        var start = DateTimeOffset.Parse("2026-05-09T10:00:00Z");
        var component = new MqttMetricsSinkComponent();
        var snapshots = new List<MqttMetricsSnapshot>();
        var sink = new ActionBlock<MqttMetricsSnapshot>(snapshots.Add);

        component.Snapshots.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/1", [1, 2], start, retain: true));
        component.Input.Post(Message("factory/2", [1, 2, 3, 4], start.AddSeconds(1)));
        component.Input.Post(Message("factory/1", [], start.AddSeconds(2)));
        component.Complete();

        await Task.WhenAll(component.Completion, sink.Completion);

        snapshots.Should().HaveCount(3);
        var current = component.Current;
        current.MessageCount.Should().Be(3);
        current.TotalPayloadBytes.Should().Be(6);
        current.MinPayloadBytes.Should().Be(0);
        current.MaxPayloadBytes.Should().Be(4);
        current.AveragePayloadBytes.Should().Be(2);
        current.RetainedMessageCount.Should().Be(1);
        current.UniqueTopicCount.Should().Be(2);
        current.LastTopic.Should().Be("factory/1");
        current.LastReceivedAt.Should().Be(start.AddSeconds(2));
    }

    [Fact]
    public async Task Current_BeforeMessages_ReturnsEmptySnapshot()
    {
        var component = new MqttMetricsSinkComponent();

        component.Complete();
        await component.Completion;

        component.Current.Should().BeEquivalentTo(new MqttMetricsSnapshot());
    }

    [Fact]
    public async Task ProcessingFailure_PublishesErrorAndKeepsProcessing()
    {
        var component = new MqttMetricsSinkComponent();
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/ok", [1]));
        component.Input.Post(new MqttEnvelope { Topic = "factory/bad", Payload = null! });
        component.Input.Post(Message("factory/next", [1, 2]));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        component.Current.MessageCount.Should().Be(2);
        component.Current.TotalPayloadBytes.Should().Be(3);

        var error = errors.Should().ContainSingle().Subject;
        error.NodeId.Should().Be(component.Id);
        error.Code.Should().Be(FlowErrorCodes.ProcessingFailed);
        error.Message.Should().Be("MQTT metrics update failed.");
        error.Context.Should().Be("factory/bad");
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttMetricsSinkComponent(FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("metrics failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("metrics failed");
        await errorSink.Completion;

        var error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(FlowErrorCodes.NodeFaulted);
        error.Message.Should().Be("MQTT metrics sink faulted.");
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
