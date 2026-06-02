using FluxMq.Components.Assertions;
using FluxMq.Components.Control;
using FluxMq.Components.FileWriter;
using FluxMq.Components.Logging;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxFlow.Components.Assertions.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Mapping;
using MQTTnet.Protocol;
using Shouldly;
using System.Text;
using System.Threading.Tasks.Dataflow;
using AssertionErrorCodes = FluxFlow.Components.Assertions.AssertionErrorCodes;
using AssertionNodeContext = FluxFlow.Components.Assertions.Contracts.AssertionNodeContext;

namespace FluxMq.Components.Tests.Components;

public sealed class FlowAssertionComponentTests
{
    [Fact]
    public async Task Evaluate_RoutesMessagesAndPublishesResultsAndEntries()
    {
        var component = Create<MqttEnvelope>(
            "qos >= 1",
            "MqttEnvelope",
            "QoS at least once",
            "Expected QoS to be at least 1.");
        var results = new List<FlowAssertionResult>();
        var passed = new List<string>();
        var failed = new List<string>();
        var entries = new List<FlowLogEntry>();
        var events = new List<FlowEvent>();
        var resultSink = new ActionBlock<FlowAssertionResult>(results.Add);
        var passedSink = new ActionBlock<MqttEnvelope>(message => passed.Add(message.Topic));
        var failedSink = new ActionBlock<MqttEnvelope>(message => failed.Add(message.Topic));
        var entrySink = new ActionBlock<FlowLogEntry>(entries.Add);
        var eventSink = new ActionBlock<FlowEvent>(events.Add);

        component.Result.LinkTo(resultSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Passed.LinkTo(passedSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Failed.LinkTo(failedSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Entries.LinkTo(entrySink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Events.LinkTo(eventSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/qos0", MqttQualityOfServiceLevel.AtMostOnce));
        component.Input.Post(Message("factory/qos1", MqttQualityOfServiceLevel.AtLeastOnce));
        component.Complete();

        await Task.WhenAll(component.Completion, resultSink.Completion, passedSink.Completion, failedSink.Completion, entrySink.Completion, eventSink.Completion);

        results.Select(result => result.Passed).ShouldBe([false, true]);
        results[0].AssertionName.ShouldBe("QoS at least once");
        results[0].InputType.ShouldBe("MqttEnvelope");
        results[0].Message.ShouldBe("Expected QoS to be at least 1.");
        passed.ShouldBe(["factory/qos1"]);
        failed.ShouldBe(["factory/qos0"]);
        entries.Select(entry => entry.Severity).ShouldBe([FlowLogSeverity.Warning, FlowLogSeverity.Info]);
        entries.Select(entry => entry.Message).ShouldBe([
            "Assertion failed: QoS at least once.",
            "Assertion passed: QoS at least once."
        ]);
        events.Select(flowEvent => flowEvent.Type).ShouldBe([FluxMqEventTypes.AssertionEvaluated, FluxMqEventTypes.AssertionEvaluated]);
        events.Select(flowEvent => flowEvent.Status).ShouldBe(["failed", "passed"]);
        events[0].GetAttribute("assertionName").ShouldBe("QoS at least once");
        component.Id.ShouldNotBe(FlowNodeId.Empty);
    }

    [Fact]
    public async Task Evaluate_CanAssertNonMqttInput()
    {
        var component = Create<FileWriteRequest>(
            """path.EndsWith(".json") && contentText.Contains("ok")""",
            "FileWriteRequest",
            "JSON file contains ok");
        var passed = new List<string>();
        var failed = new List<string>();
        var resultSink = DataflowBlock.NullTarget<FlowAssertionResult>();
        var passedSink = new ActionBlock<FileWriteRequest>(request => passed.Add(request.Path));
        var failedSink = new ActionBlock<FileWriteRequest>(request => failed.Add(request.Path));

        component.Result.LinkTo(resultSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Passed.LinkTo(passedSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Failed.LinkTo(failedSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new FileWriteRequest { Path = "good.json", Content = Encoding.UTF8.GetBytes("""{"status":"ok"}""") });
        component.Input.Post(new FileWriteRequest { Path = "bad.txt", Content = Encoding.UTF8.GetBytes("""{"status":"ok"}""") });
        component.Complete();

        await Task.WhenAll(component.Completion, passedSink.Completion, failedSink.Completion);

        passed.ShouldBe(["good.json"]);
        failed.ShouldBe(["bad.txt"]);
    }

    [Fact]
    public async Task ExpressionFailure_PublishesErrorAndCompletes()
    {
        var component = Create<MqttEnvelope>(
            "missingVariable == true",
            "MqttEnvelope",
            "Broken assertion");
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("bad/topic", MqttQualityOfServiceLevel.AtMostOnce));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(AssertionErrorCodes.ExpressionFailed);
        error.Message.ShouldContain("flow.assert failed to evaluate input");
    }

    private static FlowAssertionComponent<TInput> Create<TInput>(
        string expression,
        string inputType,
        string name,
        string failureMessage = "Assertion failed.")
    {
        var options = new AssertionOptions
        {
            Expression = expression,
            InputType = inputType,
            Description = name,
            FailureMessage = failureMessage,
            BoundedCapacity = 1000,
            EmitPassedInput = true,
            EmitFailedInput = true
        };

        return new FlowAssertionComponent<TInput>(
            options,
            new FluxMqDynamicExpressionEngine(),
            new FluxMqControlContextFactory(),
            new AssertionNodeContext
            {
                Address = new(new("test"), new("assert")),
                Options = options,
                InputType = typeof(TInput)
            });
    }

    private static MqttEnvelope Message(string topic, MqttQualityOfServiceLevel qos) => new()
    {
        Topic = topic,
        Payload = [],
        QualityOfService = qos
    };
}
