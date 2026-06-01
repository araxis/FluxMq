using FluxMq.Components.Control;
using FluxMq.Components.Logging;
using FluxMq.Core.Models;
using FluxFlow.Components.Control.Contracts;
using FluxFlow.Components.Control.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class FlowWhenComponentTests
{
    [Fact]
    public async Task Input_RoutesMessagesToTrueAndFalsePortsAndWritesEntries()
    {
        var component = Create("topic.StartsWith(\"factory/\")");
        var trueTopics = new List<string>();
        var falseTopics = new List<string>();
        var entries = new List<FlowLogEntry>();
        var trueSink = new ActionBlock<MqttEnvelope>(message => trueTopics.Add(message.Topic));
        var falseSink = new ActionBlock<MqttEnvelope>(message => falseTopics.Add(message.Topic));
        var entrySink = new ActionBlock<FlowLogEntry>(entries.Add);

        component.WhenTrue.LinkTo(trueSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.WhenFalse.LinkTo(falseSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Entries.LinkTo(entrySink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/line-1"));
        component.Input.Post(Message("system/health"));
        component.Input.Post(Message("factory/line-2"));
        component.Complete();

        await Task.WhenAll(component.Completion, trueSink.Completion, falseSink.Completion, entrySink.Completion);

        trueTopics.ShouldBe(["factory/line-1", "factory/line-2"]);
        falseTopics.ShouldBe(["system/health"]);
        entries.Select(entry => entry.Message).ShouldBe([
            "Routed input to WhenFalse.",
            "Routed input to WhenTrue.",
            "Routed input to WhenTrue."
        ], ignoreOrder: true);
        component.Id.ShouldNotBe(FlowNodeId.Empty);
    }

    [Fact]
    public async Task ExpressionFailure_PublishesErrorAndContinuesCompletion()
    {
        var component = Create("missingVariable == true");
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/line-1"));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(4100);
        error.Message.ShouldContain("flow.when failed to evaluate input");
    }

    private static FlowWhenComponent<MqttEnvelope> Create(string expression)
    {
        var options = Options(expression);
        return new FlowWhenComponent<MqttEnvelope>(
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
            Address = new(new("test"), new("when")),
            Options = options,
            InputType = typeof(TInput)
        };

    private static MqttEnvelope Message(string topic)
        => new()
        {
            Topic = topic,
            Payload = []
        };
}
