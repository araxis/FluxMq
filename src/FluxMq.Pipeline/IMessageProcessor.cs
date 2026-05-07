using FluxMq.Core.Models;

namespace FluxMq.Pipeline;

public interface IMessageProcessor
{
    ValueTask ProcessAsync(MqttEnvelope envelope, CancellationToken ct = default);
}
