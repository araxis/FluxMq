using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class TopicFilterComponentTests
{
    [Fact]
    public async Task Prefix_ForwardsOnlyMatchingTopics()
    {
        var component = TopicFilterComponent.Prefix("factory/");
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "factory/line-1", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "system/health", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "factory/line-2", Payload = [] });
        component.Input.Complete();

        await sink.Completion;

        received.Should().Equal("factory/line-1", "factory/line-2");
        component.Id.Should().NotBe(FlowNodeId.Empty);
        component.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Fault_CompletesWithFailure()
    {
        var component = new TopicFilterComponent(_ => true);
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("filter failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);
        var act = async () => await component.Completion;

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("filter failed");
        await errorSink.Completion;

        errors.Should().ContainSingle().Which.Message.Should().Be("Topic filter faulted.");
    }

    [Fact]
    public async Task PredicateFailure_PublishesErrorAndKeepsCompleting()
    {
        var component = new TopicFilterComponent(message =>
        {
            if (message.Topic == "bad/topic")
            {
                throw new InvalidOperationException("bad predicate");
            }

            return true;
        });

        var received = new List<string>();
        var errors = new List<FlowError>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "good/topic", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "bad/topic", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "next/topic", Payload = [] });
        component.Input.Complete();

        await component.Completion;
        component.Complete();
        await Task.WhenAll(sink.Completion, errorSink.Completion);

        received.Should().Equal("good/topic", "next/topic");
        var error = errors.Should().ContainSingle().Subject;
        error.NodeId.Should().Be(component.Id);
        error.Message.Should().Be("Topic filter predicate failed.");
        error.Context.Should().Be("bad/topic");
    }

    [Fact]
    public async Task Complete_KeepsErrorPortOpenUntilPendingMessagesDrain()
    {
        var component = new TopicFilterComponent(_ => throw new InvalidOperationException("late failure"));
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "late/topic", Payload = [] });
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        errors.Should().ContainSingle().Which.Context.Should().Be("late/topic");
    }
}
