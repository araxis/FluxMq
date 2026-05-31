using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Mapping;
using FluxFlow.Engine.Mapping;
using FluxMq.Components.MessageFilter;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MessageFilterComponentTests
{
    [Fact]
    public async Task Prefix_ForwardsOnlyMatchingTopics()
    {
        var component = MessageFilterComponent.TopicPrefix("factory/");
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "factory/line-1", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "system/health", Payload = [] });
        component.Input.Post(new MqttEnvelope { Topic = "factory/line-2", Payload = [] });
        component.Input.Complete();

        await sink.Completion;

        received.ShouldBe(new[] { "factory/line-1", "factory/line-2" });
        component.PassedCount.ShouldBe(2);
        component.Id.ShouldNotBe(FlowNodeId.Empty);
        component.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Fault_CompletesWithFailure()
    {
        var component = new MessageFilterComponent(_ => true);
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("filter failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);
        var act = async () => await component.Completion;

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("filter failed");
        await errorSink.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("Topic filter faulted.");
    }

    [Fact]
    public async Task ExpressionPredicate_ForwardsOnlyMatchingMessages()
    {
        var component = new MessageFilterComponent(
            new MqttEnvelopeExpressionPredicate(new DynamicExpressoFlowExpressionEngine(), "qos >= 1 && topic.StartsWith(\"factory/\")"));
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "factory/line-1", Payload = [], QualityOfService = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce });
        component.Input.Post(new MqttEnvelope { Topic = "factory/line-2", Payload = [], QualityOfService = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce });
        component.Input.Post(new MqttEnvelope { Topic = "system/line-2", Payload = [], QualityOfService = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce });
        component.Complete();

        await sink.Completion;

        received.ShouldBe(new[] { "factory/line-2" });
    }

    [Fact]
    public async Task PredicateFailure_PublishesErrorAndKeepsCompleting()
    {
        var component = new MessageFilterComponent(message =>
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

        received.ShouldBe(new[] { "good/topic", "next/topic" });
        var error = errors.ShouldHaveSingleItem();
        error.NodeId.ShouldBe(component.Id);
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Message.ShouldBe("Topic filter predicate failed.");
        error.Context.ShouldBe("bad/topic");
    }

    [Fact]
    public async Task Complete_KeepsErrorPortOpenUntilPendingMessagesDrain()
    {
        var component = new MessageFilterComponent(_ => throw new InvalidOperationException("late failure"));
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope { Topic = "late/topic", Payload = [] });
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Context.ShouldBe("late/topic");
    }
}
