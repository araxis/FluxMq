using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Components;

public sealed class MqttConditionRouterComponentTests
{
    [Fact]
    public async Task TopicPrefix_RoutesMatchingMessagesToTruePortAndOthersToFalsePort()
    {
        var component = MqttConditionRouterComponent.TopicPrefix("factory/");
        var trueTopics = new List<string>();
        var falseTopics = new List<string>();
        var trueSink = new ActionBlock<MqttEnvelope>(message => trueTopics.Add(message.Topic));
        var falseSink = new ActionBlock<MqttEnvelope>(message => falseTopics.Add(message.Topic));

        component.WhenTrue.LinkTo(trueSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.WhenFalse.LinkTo(falseSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/line-1"));
        component.Input.Post(Message("system/health"));
        component.Input.Post(Message("factory/line-2"));
        component.Complete();

        await Task.WhenAll(component.Completion, trueSink.Completion, falseSink.Completion);

        trueTopics.Should().Equal("factory/line-1", "factory/line-2");
        falseTopics.Should().Equal("system/health");
        component.Id.Should().NotBe(FlowNodeId.Empty);
    }

    [Fact]
    public async Task PredicateFailure_PublishesErrorAndContinuesRoutingLaterMessages()
    {
        var component = new MqttConditionRouterComponent(message =>
        {
            if (message.Topic == "bad/topic")
            {
                throw new InvalidOperationException("predicate failed");
            }

            return message.Topic.StartsWith("factory/", StringComparison.Ordinal);
        });
        var trueTopics = new List<string>();
        var falseTopics = new List<string>();
        var errors = new List<FlowError>();
        var trueSink = new ActionBlock<MqttEnvelope>(message => trueTopics.Add(message.Topic));
        var falseSink = new ActionBlock<MqttEnvelope>(message => falseTopics.Add(message.Topic));
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.WhenTrue.LinkTo(trueSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.WhenFalse.LinkTo(falseSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/line-1"));
        component.Input.Post(Message("bad/topic"));
        component.Input.Post(Message("system/health"));
        component.Input.Post(Message("factory/line-2"));
        component.Complete();

        await Task.WhenAll(component.Completion, trueSink.Completion, falseSink.Completion, errorSink.Completion);

        trueTopics.Should().Equal("factory/line-1", "factory/line-2");
        falseTopics.Should().Equal("system/health");

        var error = errors.Should().ContainSingle().Subject;
        error.NodeId.Should().Be(component.Id);
        error.Code.Should().Be(FlowErrorCodes.ProcessingFailed);
        error.Message.Should().Be("MQTT condition router predicate failed.");
        error.Context.Should().Be("bad/topic");
    }

    [Fact]
    public async Task Complete_KeepsErrorPortOpenUntilPendingMessagesDrain()
    {
        var component = new MqttConditionRouterComponent(_ => throw new InvalidOperationException("late failure"));
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("late/topic"));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        var error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(FlowErrorCodes.ProcessingFailed);
        error.Context.Should().Be("late/topic");
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttConditionRouterComponent(_ => true, FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("router failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("router failed");
        await errorSink.Completion;

        var error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(FlowErrorCodes.NodeFaulted);
        error.Message.Should().Be("MQTT condition router faulted.");
    }

    private static MqttEnvelope Message(string topic) => new()
    {
        Topic = topic,
        Payload = []
    };
}
