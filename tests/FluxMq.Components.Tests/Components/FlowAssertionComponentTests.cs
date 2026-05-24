using Shouldly;
using FluxMq.Components.Assertions;
using FluxMq.Components.FileWriter;
using FluxMq.Components.Logging;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Mapping;
using MQTTnet.Protocol;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class FlowAssertionComponentTests
{
    [Fact]
    public async Task Evaluate_RoutesMessagesAndPublishesResultsAndEntries()
    {
        var component = new FlowAssertionComponent<MqttEnvelope>(
            new DelegateFlowPredicate<MqttEnvelope>(message => message.QualityOfService >= MqttQualityOfServiceLevel.AtLeastOnce),
            "QoS at least once",
            "qos >= 1",
            "Expected QoS to be at least 1.");
        var results = new List<FlowAssertionResult>();
        var passed = new List<string>();
        var failed = new List<string>();
        var entries = new List<FlowLogEntry>();
        var resultSink = new ActionBlock<FlowAssertionResult>(results.Add);
        var passedSink = new ActionBlock<MqttEnvelope>(message => passed.Add(message.Topic));
        var failedSink = new ActionBlock<MqttEnvelope>(message => failed.Add(message.Topic));
        var entrySink = new ActionBlock<FlowLogEntry>(entries.Add);

        component.Result.LinkTo(resultSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Passed.LinkTo(passedSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Failed.LinkTo(failedSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Entries.LinkTo(entrySink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/qos0", MqttQualityOfServiceLevel.AtMostOnce));
        component.Input.Post(Message("factory/qos1", MqttQualityOfServiceLevel.AtLeastOnce));
        component.Complete();

        await Task.WhenAll(component.Completion, resultSink.Completion, passedSink.Completion, failedSink.Completion, entrySink.Completion);

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
        component.Id.ShouldNotBe(FlowNodeId.Empty);
    }

    [Fact]
    public async Task Evaluate_CanAssertNonMqttInput()
    {
        var component = new FlowAssertionComponent<FileWriteRequest>(
            new FlowAssertionExpressionPredicate<FileWriteRequest>(
                new DynamicExpressoFlowExpressionEngine(),
                """path.EndsWith(".json") && contentText.Contains("ok")"""),
            "JSON file contains ok",
            """path.EndsWith(".json") && contentText.Contains("ok")""");
        var passed = new List<string>();
        var failed = new List<string>();
        var passedSink = new ActionBlock<FileWriteRequest>(request => passed.Add(request.Path));
        var failedSink = new ActionBlock<FileWriteRequest>(request => failed.Add(request.Path));

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
    public async Task PredicateFailure_PublishesErrorAndContinuesLaterMessages()
    {
        var component = new FlowAssertionComponent<MqttEnvelope>(
            new DelegateFlowPredicate<MqttEnvelope>(message =>
            {
                if (message.Topic == "bad/topic")
                {
                    throw new InvalidOperationException("expression failed");
                }

                return true;
            }),
            "Always true",
            "true");
        var results = new List<FlowAssertionResult>();
        var errors = new List<FlowError>();
        var resultSink = new ActionBlock<FlowAssertionResult>(results.Add);
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Result.LinkTo(resultSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("bad/topic", MqttQualityOfServiceLevel.AtMostOnce));
        component.Input.Post(Message("factory/good", MqttQualityOfServiceLevel.AtMostOnce));
        component.Complete();

        await Task.WhenAll(component.Completion, resultSink.Completion, errorSink.Completion);

        ((MqttEnvelope)results.ShouldHaveSingleItem().Value!).Topic.ShouldBe("factory/good");

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Message.ShouldBe("Flow assertion expression failed.");
        error.Context.ShouldBe("bad/topic");
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new FlowAssertionComponent<MqttEnvelope>(
            new DelegateFlowPredicate<MqttEnvelope>(_ => true),
            "Always true",
            "true",
            id: FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("assertion failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var completionTask = component.Completion;
        await completionTask.ContinueWith(_ => { }, TaskScheduler.Default);

        completionTask.IsFaulted.ShouldBeTrue();
        completionTask.Exception!.Flatten().InnerExceptions
            .OfType<InvalidOperationException>()
            .ShouldContain(ex => ex.Message == "assertion failed");

        await errorSink.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("Flow assertion faulted.");
    }

    private static MqttEnvelope Message(string topic, MqttQualityOfServiceLevel qos) => new()
    {
        Topic = topic,
        Payload = [],
        QualityOfService = qos
    };
}
