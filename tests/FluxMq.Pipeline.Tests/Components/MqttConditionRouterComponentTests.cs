using Shouldly;
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

        trueTopics.ShouldBe(new[] { "factory/line-1", "factory/line-2" });
        falseTopics.ShouldBe(new[] { "system/health" });
        component.Id.ShouldNotBe(FlowNodeId.Empty);
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

        trueTopics.ShouldBe(new[] { "factory/line-1", "factory/line-2" });
        falseTopics.ShouldBe(new[] { "system/health" });

        var error = errors.ShouldHaveSingleItem();
        error.NodeId.ShouldBe(component.Id);
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Message.ShouldBe("MQTT condition router predicate failed.");
        error.Context.ShouldBe("bad/topic");
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

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Context.ShouldBe("late/topic");
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

        // Completion is Task.WhenAll(_block, _whenTrue, _whenFalse). Faulting _block propagates
        // to the two output ports via PropagateCompletion, so WhenAll faults with multiple nested
        // AggregateExceptions. Capture the task before awaiting to inspect the exception tree.
        var completionTask = component.Completion;
        await completionTask.ContinueWith(_ => { }, TaskScheduler.Default);

        completionTask.IsFaulted.ShouldBeTrue();
        completionTask.Exception!.Flatten().InnerExceptions
            .OfType<InvalidOperationException>()
            .ShouldContain(ex => ex.Message == "router failed");

        await errorSink.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("MQTT condition router faulted.");
    }

    private static MqttEnvelope Message(string topic) => new()
    {
        Topic = topic,
        Payload = []
    };
}
