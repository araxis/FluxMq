using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage.Models;

namespace FluxMq.Components.Storage.Repositories;

public interface IMessageRepository
{
    void Add(SessionId sessionId, MqttEnvelope envelope);
    void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes);
    IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId);
    IReadOnlyList<StoredMessage> GetByTopic(string topic);
    long CountBySession(SessionId sessionId);
}
