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
        var failure = new InvalidOperationException("filter failed");

        component.Fault(failure);
        var act = async () => await component.Completion;

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("filter failed");
    }
}
