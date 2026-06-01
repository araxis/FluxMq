using FluxMq.Components.Control;
using FluxMq.Core.Models;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class FlowFilterComponentTests
{
    [Fact]
    public async Task Input_ForwardsOnlyMatchingMessagesAndCountsPasses()
    {
        var component = Create("qos >= 1 && topic.StartsWith(\"factory/\")");
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/line-1", MqttQualityOfServiceLevel.AtMostOnce));
        component.Input.Post(Message("factory/line-2", MqttQualityOfServiceLevel.AtLeastOnce));
        component.Input.Post(Message("system/line-2", MqttQualityOfServiceLevel.AtLeastOnce));
        component.Complete();

        await Task.WhenAll(component.Completion, sink.Completion);

        received.ShouldBe(["factory/line-2"]);
        component.PassedCount.ShouldBe(1);
        component.Id.ShouldNotBe(FlowNodeId.Empty);
    }

    [Fact]
    public async Task ExpressionFailure_PublishesErrorAndKeepsCompleting()
    {
        var component = Create("missingVariable == true");
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/line-1", MqttQualityOfServiceLevel.AtLeastOnce));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(4000);
        error.Message.ShouldContain("flow.filter failed to evaluate input");
    }

    private static FlowFilterComponent<MqttEnvelope> Create(string expression)
    {
        var options = Options(expression);
        return new FlowFilterComponent<MqttEnvelope>(
            options,
            new DynamicExpressoFlowExpressionEngine(),
            new FluxMqControlContextFactory(),
            Context<MqttEnvelope>(options));
    }

    private static ControlExpressionOptions Options(string expression)
        => new()
        {
            Expression = expression,
            InputType = "MqttEnvelope",
            BoundedCapacity = 1000
        };

    private static ControlNodeContext Context<TInput>(ControlExpressionOptions options)
        => new()
        {
            Address = new(new("test"), new("filter")),
            Options = options,
            InputType = typeof(TInput)
        };

    private static MqttEnvelope Message(string topic, MqttQualityOfServiceLevel qos)
        => new()
        {
            Topic = topic,
            Payload = [],
            QualityOfService = qos
        };
}
