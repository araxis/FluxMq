using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.Components.Replay;

public sealed record MqttRecordingRequest
{
    public required SessionId SessionId { get; init; }
    public required MqttEnvelope Envelope { get; init; }
}
