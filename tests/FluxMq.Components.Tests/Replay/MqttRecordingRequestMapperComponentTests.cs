using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Replay;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Replay;

public sealed class MqttRecordingRequestMapperComponentTests
{
    [Fact]
    public async Task Input_MapsEnvelopeToRecordingRequestForConfiguredSession()
    {
        var sessionId = SessionId.New();
        var component = new MqttRecordingRequestMapperComponent(sessionId);
        var output = new BufferBlock<MqttRecordingRequest>();
        var envelope = new MqttEnvelope { Topic = "factory/line-1", Payload = [1] };

        component.Output.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(envelope);
        component.Complete();

        var request = await output.ReceiveAsync();
        await component.Completion;

        request.SessionId.ShouldBe(sessionId);
        request.Envelope.ShouldBe(envelope);
    }
}
