using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class ReplaySourceComponentTests
{
    [Fact]
    public async Task StartAsync_EmitsMessagesInReceivedOrder()
    {
        var start = DateTimeOffset.Parse("2026-05-08T10:00:00Z");
        var component = new ReplaySourceComponent(
            [
                Message("topic/3", start.AddSeconds(3)),
                Message("topic/1", start),
                Message("topic/2", start.AddSeconds(1))
            ],
            delayAsync: (_, _) => ValueTask.CompletedTask);
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await component.StartAsync();
        await sink.Completion;

        received.Should().Equal("topic/1", "topic/2", "topic/3");
        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ScalesRelativeDelaysBySpeed()
    {
        var start = DateTimeOffset.Parse("2026-05-08T10:00:00Z");
        var delays = new List<TimeSpan>();
        var component = new ReplaySourceComponent(
            [
                Message("topic/1", start),
                Message("topic/2", start.AddSeconds(2)),
                Message("topic/3", start.AddSeconds(3))
            ],
            speed: 2,
            delayAsync: (delay, _) =>
            {
                delays.Add(delay);
                return ValueTask.CompletedTask;
            });
        var sink = new ActionBlock<MqttEnvelope>(_ => { });

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await component.StartAsync();
        await sink.Completion;

        delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task StartAsync_PublishesErrorWhenReplayFails()
    {
        var component = new ReplaySourceComponent(
            [Message("topic/1", DateTimeOffset.UtcNow), Message("topic/2", DateTimeOffset.UtcNow.AddSeconds(1))],
            delayAsync: (_, _) => throw new InvalidOperationException("delay failed"));
        var errors = new List<FlowError>();
        var messages = new List<MqttEnvelope>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var messageSink = new ActionBlock<MqttEnvelope>(messages.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Output.LinkTo(messageSink, new DataflowLinkOptions { PropagateCompletion = true });

        await component.StartAsync();
        await Task.WhenAll(errorSink.Completion, messageSink.Completion);

        messages.Should().ContainSingle().Which.Topic.Should().Be("topic/1");
        var error = errors.Should().ContainSingle().Subject;
        error.NodeId.Should().Be(component.Id);
        error.Code.Should().Be(FlowErrorCodes.ProcessingFailed);
        error.Message.Should().Be("Replay source failed.");
    }

    [Fact]
    public async Task Complete_BeforeStart_CompletesOutput()
    {
        var component = new ReplaySourceComponent([Message("topic/1", DateTimeOffset.UtcNow)]);
        var sink = new ActionBlock<MqttEnvelope>(_ => { });

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Complete();

        await Task.WhenAll(component.Completion, sink.Completion);
        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new ReplaySourceComponent([], id: FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("replay faulted");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("replay faulted");
        await errorSink.Completion;

        errors.Should().ContainSingle().Which.Code.Should().Be(FlowErrorCodes.NodeFaulted);
    }

    private static MqttEnvelope Message(string topic, DateTimeOffset receivedAt) => new()
    {
        Topic = topic,
        Payload = [],
        ReceivedAt = receivedAt
    };
}
