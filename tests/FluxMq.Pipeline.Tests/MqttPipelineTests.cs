using Shouldly;
using FluxMq.Core.Models;
using FluxMq.Pipeline;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests;

public class MqttPipelineTests
{
    [Fact]
    public async Task Pipeline_FansOut_ToAllLinkedSinks()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var receivedA = new List<MqttEnvelope>();
        var receivedB = new List<MqttEnvelope>();

        await using var pipeline = new MqttPipeline(channel.Reader);

        var sinkA = new ActionBlock<MqttEnvelope>(e => receivedA.Add(e));
        var sinkB = new ActionBlock<MqttEnvelope>(e => receivedB.Add(e));
        pipeline.LinkTo(sinkA);
        pipeline.LinkTo(sinkB);

        await channel.Writer.WriteAsync(new MqttEnvelope { Topic = "test/topic", Payload = [] });
        channel.Writer.Complete();

        await Task.WhenAll(sinkA.Completion, sinkB.Completion);

        receivedA.ShouldHaveSingleItem().Topic.ShouldBe("test/topic");
        receivedB.ShouldHaveSingleItem().Topic.ShouldBe("test/topic");
    }

    [Fact]
    public async Task Pipeline_Delivers_MultipleEnvelopes_ToEachSink()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var received = new List<string>();

        await using var pipeline = new MqttPipeline(channel.Reader);
        var sink = new ActionBlock<MqttEnvelope>(e => received.Add(e.Topic));
        pipeline.LinkTo(sink);

        foreach (var topic in new[] { "a", "b", "c" })
            await channel.Writer.WriteAsync(new MqttEnvelope { Topic = topic, Payload = [] });

        channel.Writer.Complete();
        await sink.Completion;

        received.ShouldBe(new[] { "a", "b", "c" });
    }

    [Fact]
    public async Task Pipeline_Exposes_Output_ForDirectLinking()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var received = new List<MqttEnvelope>();

        await using var pipeline = new MqttPipeline(channel.Reader);

        // Consumer links directly to Output (e.g. with a filter predicate)
        var sink = new ActionBlock<MqttEnvelope>(e => received.Add(e));
        pipeline.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true },
            predicate: e => e.Topic.StartsWith("sensors/"));

        await channel.Writer.WriteAsync(new MqttEnvelope { Topic = "sensors/temp", Payload = [] });
        await channel.Writer.WriteAsync(new MqttEnvelope { Topic = "commands/reboot", Payload = [] });
        channel.Writer.Complete();

        await sink.Completion;

        received.ShouldHaveSingleItem().Topic.ShouldBe("sensors/temp");
    }

    [Fact]
    public async Task Pipeline_DisposesCleanly_WithoutMessagesInFlight()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var pipeline = new MqttPipeline(channel.Reader);

        await pipeline.DisposeAsync();
    }

    [Fact]
    public async Task Pipeline_DisposesCleanly_WithPendingMessages()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();

        await using var pipeline = new MqttPipeline(channel.Reader);

        // Write without completing channel — dispose should still return
        for (var i = 0; i < 10; i++)
            await channel.Writer.WriteAsync(new MqttEnvelope { Topic = $"t/{i}", Payload = [] });

        // No assertion beyond "does not hang"
    }
}
