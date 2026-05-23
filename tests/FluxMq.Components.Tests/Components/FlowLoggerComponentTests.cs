using FluxMq.Components.Logging;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class FlowLoggerComponentTests
{
    [Fact]
    public async Task Input_PublishesStructuredMessageEntry()
    {
        var component = new FlowLoggerComponent(includePayloadPreview: true);
        var entries = new List<FlowLogEntry>();
        var target = new ActionBlock<FlowLogEntry>(entries.Add);

        component.Entries.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(new MqttEnvelope
        {
            Topic = "factory/line-a",
            Payload = """{"status":"ok"}"""u8.ToArray(),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        component.Complete();

        await Task.WhenAll(component.Completion, target.Completion);

        var entry = entries.ShouldHaveSingleItem();
        entry.Severity.ShouldBe(FlowLogSeverity.Info);
        entry.Source.ShouldBe("MqttEnvelope");
        entry.Topic.ShouldBe("factory/line-a");
        entry.PayloadBytes.ShouldBe(15);
        entry.PayloadPreview.ShouldBe("""{"status":"ok"}""");
        entry.Context.ShouldBe("qos=1; retain=True");
    }

    [Fact]
    public async Task FlowErrors_PublishesErrorEntry()
    {
        var nodeId = FlowNodeId.New();
        var timestamp = DateTimeOffset.Parse("2026-05-23T10:15:00Z");
        var component = new FlowLoggerComponent();
        var entries = new List<FlowLogEntry>();
        var target = new ActionBlock<FlowLogEntry>(entries.Add);

        component.Entries.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true });

        component.FlowErrors.Post(new FlowError
        {
            NodeId = nodeId,
            Code = FlowErrorCodes.ProcessingFailed,
            Message = "Mapper failed.",
            OccurredAt = timestamp,
            Context = "mapper.Input"
        });
        component.Complete();

        await Task.WhenAll(component.Completion, target.Completion);

        var entry = entries.ShouldHaveSingleItem();
        entry.Timestamp.ShouldBe(timestamp);
        entry.Severity.ShouldBe(FlowLogSeverity.Error);
        entry.Source.ShouldBe("FlowError");
        entry.RelatedNodeId.ShouldBe(nodeId);
        entry.ErrorCode.ShouldBe(FlowErrorCodes.ProcessingFailed);
        entry.Message.ShouldBe("Mapper failed.");
        entry.Context.ShouldBe("mapper.Input");
    }

    [Fact]
    public async Task RecentEntries_KeepsLatestConfiguredEntries()
    {
        var component = new FlowLoggerComponent(maxEntries: 2);

        component.Input.Post(Message("factory/one"));
        component.Input.Post(Message("factory/two"));
        component.Input.Post(Message("factory/three"));
        component.Complete();

        await component.Completion;

        component.RecentEntries.Select(entry => entry.Topic)
            .ShouldBe(["factory/two", "factory/three"]);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsInputs()
    {
        var component = new FlowLoggerComponent(FlowNodeId.New());
        var errors = new List<FlowError>();
        var target = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("logger failed");

        component.Errors.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true });

        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("logger failed");
        await target.Completion;

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.NodeFaulted);
        error.Message.ShouldBe("Flow logger faulted.");
    }

    private static MqttEnvelope Message(string topic) => new()
    {
        Topic = topic,
        Payload = [1]
    };
}
