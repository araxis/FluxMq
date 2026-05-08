using FluxMq.Core.Models;
using FluxMq.Storage.Models;

namespace FluxMq.Storage.Repositories;

public interface IMessageRepository
{
    void Add(Guid sessionId, MqttEnvelope envelope);
    void AddBatch(Guid sessionId, IEnumerable<MqttEnvelope> envelopes);
    IReadOnlyList<StoredMessage> GetBySession(Guid sessionId);
    IReadOnlyList<StoredMessage> GetByTopic(string topic);
    long CountBySession(Guid sessionId);
}
