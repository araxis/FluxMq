using FluentAssertions;
using FluxMq.Core.Models;
using FluxMq.Pipeline;
using System.Threading.Channels;

namespace FluxMq.Pipeline.Tests;

public class MessagePipelineTests
{
    [Fact]
    public async Task Pipeline_DeliversEnvelopes_ToAllProcessors()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var receivedA = new List<MqttEnvelope>();
        var receivedB = new List<MqttEnvelope>();

        var processorA = new CapturingProcessor(receivedA);
        var processorB = new CapturingProcessor(receivedB);

        await using var pipeline = new MessagePipeline(channel.Reader, [processorA, processorB]);
        pipeline.Start();

        var envelope = new MqttEnvelope { Topic = "test/topic", Payload = [1, 2, 3] };
        await channel.Writer.WriteAsync(envelope);
        channel.Writer.Complete();

        await Task.Delay(100);

        receivedA.Should().ContainSingle().Which.Topic.Should().Be("test/topic");
        receivedB.Should().ContainSingle().Which.Topic.Should().Be("test/topic");
    }

    [Fact]
    public async Task Pipeline_DeliversMultipleEnvelopes_InOrder()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var received = new List<string>();
        var processor = new TopicCapturingProcessor(received);

        await using var pipeline = new MessagePipeline(channel.Reader, [processor]);
        pipeline.Start();

        foreach (var topic in new[] { "a", "b", "c" })
            await channel.Writer.WriteAsync(new MqttEnvelope { Topic = topic, Payload = [] });

        channel.Writer.Complete();
        await Task.Delay(100);

        received.Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task Pipeline_StopsCleanly_OnDispose()
    {
        var channel = Channel.CreateUnbounded<MqttEnvelope>();
        var processor = new CapturingProcessor([]);

        var pipeline = new MessagePipeline(channel.Reader, [processor]);
        pipeline.Start();

        await pipeline.DisposeAsync();
    }

    private sealed class CapturingProcessor(List<MqttEnvelope> sink) : IMessageProcessor
    {
        public ValueTask ProcessAsync(MqttEnvelope envelope, CancellationToken ct = default)
        {
            sink.Add(envelope);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TopicCapturingProcessor(List<string> sink) : IMessageProcessor
    {
        public ValueTask ProcessAsync(MqttEnvelope envelope, CancellationToken ct = default)
        {
            sink.Add(envelope.Topic);
            return ValueTask.CompletedTask;
        }
    }
}
