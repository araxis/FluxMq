using FluxFlow.Components.Sessions.Contracts;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using MQTTnet.Protocol;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FluxMq.Components.Storage;

public sealed class FluxMqSessionStore(IMessageRepository messages) : ISessionStore
{
    public const string MqttRecordType = "mqtt.message";
    public const string TopicAttribute = "topic";
    public const string QosAttribute = "qos";
    public const string RetainAttribute = "retain";

    private readonly IMessageRepository _messages = messages ?? throw new ArgumentNullException(nameof(messages));
    private readonly object _sessionSync = new();
    private readonly Dictionary<string, SessionMetadata> _sessions = new(StringComparer.Ordinal);

    public Task<SessionMetadata?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var id = ParseSessionId(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sessionSync)
        {
            if (_sessions.TryGetValue(id.ToString(), out var session))
            {
                return Task.FromResult<SessionMetadata?>(CopyMetadata(session with
                {
                    MessageCount = _messages.CountBySession(id)
                }));
            }
        }

        return Task.FromResult<SessionMetadata?>(CreateMetadata(id, _messages.CountBySession(id)));
    }

    public Task<SessionMetadata> StartSessionAsync(
        SessionStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        var id = string.IsNullOrWhiteSpace(request.SessionId)
            ? SessionId.New()
            : ParseSessionId(request.SessionId);

        var session = new SessionMetadata
        {
            SessionId = id.ToString(),
            Name = request.Name,
            StartedAt = request.StartedAt,
            Notes = request.Notes,
            Tags = CopyDictionary(request.Tags),
            MessageCount = _messages.CountBySession(id)
        };

        StoreMetadata(session);
        return Task.FromResult(CopyMetadata(session));
    }

    public Task<SessionRecord> AppendMessageAsync(
        SessionAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        var id = ParseSessionId(request.Session.SessionId);
        var envelope = ToEnvelope(request.Input, request.Timestamp);

        _messages.Add(id, envelope);
        var count = _messages.CountBySession(id);
        StoreMetadata(request.Session with { MessageCount = count });
        return Task.FromResult(ToRecord(id, envelope, count));
    }

    public Task<SessionMetadata> CompleteSessionAsync(
        SessionCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        var session = request.Session with
        {
            EndedAt = request.EndedAt,
            MessageCount = request.MessageCount
        };

        StoreMetadata(session);
        return Task.FromResult(CopyMetadata(session));
    }

    public Task<IReadOnlyList<SessionMetadata>> QuerySessionsAsync(
        SessionQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SessionMetadata> sessions;
        lock (_sessionSync)
        {
            sessions = _sessions.Values.Select(CopyMetadata).ToArray();
        }

        IEnumerable<SessionMetadata> query = sessions
            .Where(session => MatchesName(session, request))
            .Where(session => MatchesTags(session, request.Tags))
            .Where(session => MatchesRange(session.StartedAt, request.StartedFrom, request.StartedTo))
            .Where(session => MatchesEndedRange(session, request.EndedFrom, request.EndedTo))
            .Where(session => MatchesCompletionState(session, request))
            .OrderByDescending(session => session.StartedAt)
            .ThenBy(session => session.SessionId, StringComparer.Ordinal);

        if (request.Limit is > 0)
        {
            query = query.Take(request.Limit.Value);
        }

        return Task.FromResult<IReadOnlyList<SessionMetadata>>(query.ToArray());
    }

    public async IAsyncEnumerable<SessionRecord> ReadMessagesAsync(
        SessionReadRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = ParseSessionId(request.SessionId);
        var emitted = 0;
        await foreach (var message in _messages.ReadBySessionAsync(id, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.StartSequence.HasValue && message.Sequence < request.StartSequence.Value)
            {
                continue;
            }

            if (request.MaxMessages.HasValue && emitted >= request.MaxMessages.Value)
            {
                yield break;
            }

            emitted++;
            yield return ToRecord(message);
        }
    }

    public static SessionRecordInput ToRecordInput(MqttEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return new SessionRecordInput
        {
            Timestamp = envelope.ReceivedAt,
            Type = MqttRecordType,
            Name = envelope.Topic,
            Payload = envelope.Payload,
            ContentType = "application/octet-stream",
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TopicAttribute] = envelope.Topic,
                [QosAttribute] = ((int)envelope.QualityOfService).ToString(CultureInfo.InvariantCulture),
                [RetainAttribute] = envelope.Retain.ToString()
            }
        };
    }

    public static MqttEnvelope ToEnvelope(SessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var topic = ReadTopic(record.Attributes, record.Name);
        return new MqttEnvelope
        {
            Topic = topic,
            Payload = ReadPayload(record.Payload),
            ReceivedAt = record.Timestamp,
            QualityOfService = ReadQos(record.Attributes),
            Retain = ReadRetain(record.Attributes)
        };
    }

    private static MqttEnvelope ToEnvelope(SessionRecordInput input, DateTimeOffset timestamp)
    {
        if (input.Payload is MqttEnvelope envelope)
        {
            return envelope with { ReceivedAt = timestamp };
        }

        var topic = ReadTopic(input.Attributes, input.Name);
        return new MqttEnvelope
        {
            Topic = topic,
            Payload = ReadPayload(input.Payload),
            ReceivedAt = timestamp,
            QualityOfService = ReadQos(input.Attributes),
            Retain = ReadRetain(input.Attributes)
        };
    }

    private static SessionRecord ToRecord(StoredMessage message)
        => new()
        {
            SessionId = message.SessionId.ToString(),
            Sequence = message.Sequence,
            Timestamp = message.ReceivedAt,
            Type = MqttRecordType,
            Name = message.Topic,
            Payload = message.Payload,
            ContentType = "application/octet-stream",
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TopicAttribute] = message.Topic,
                [QosAttribute] = ((int)message.QualityOfService).ToString(CultureInfo.InvariantCulture),
                [RetainAttribute] = message.Retain.ToString()
            }
        };

    private static SessionRecord ToRecord(SessionId sessionId, MqttEnvelope envelope, long sequence)
        => new()
        {
            SessionId = sessionId.ToString(),
            Sequence = sequence,
            Timestamp = envelope.ReceivedAt,
            Type = MqttRecordType,
            Name = envelope.Topic,
            Payload = envelope.Payload,
            ContentType = "application/octet-stream",
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TopicAttribute] = envelope.Topic,
                [QosAttribute] = ((int)envelope.QualityOfService).ToString(CultureInfo.InvariantCulture),
                [RetainAttribute] = envelope.Retain.ToString()
            }
        };

    private static SessionMetadata CreateMetadata(SessionId sessionId, long messageCount)
        => new()
        {
            SessionId = sessionId.ToString(),
            StartedAt = DateTimeOffset.UnixEpoch,
            MessageCount = messageCount
        };

    private void StoreMetadata(SessionMetadata session)
    {
        lock (_sessionSync)
        {
            _sessions[session.SessionId] = CopyMetadata(session);
        }
    }

    private static bool MatchesName(SessionMetadata session, SessionQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name) &&
            !string.Equals(session.Name, request.Name, StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(request.NamePrefix) ||
               (!string.IsNullOrWhiteSpace(session.Name) &&
                session.Name.StartsWith(request.NamePrefix, StringComparison.Ordinal));
    }

    private static bool MatchesTags(SessionMetadata session, IReadOnlyDictionary<string, string>? expectedTags)
    {
        if (expectedTags is null || expectedTags.Count == 0)
        {
            return true;
        }

        foreach (var (key, value) in expectedTags)
        {
            if (!session.Tags.TryGetValue(key, out var actual) ||
                !string.Equals(actual, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesRange(DateTimeOffset value, DateTimeOffset? from, DateTimeOffset? to)
        => (!from.HasValue || value >= from.Value) &&
           (!to.HasValue || value <= to.Value);

    private static bool MatchesEndedRange(SessionMetadata session, DateTimeOffset? from, DateTimeOffset? to)
        => (!from.HasValue || session.EndedAt >= from.Value) &&
           (!to.HasValue || session.EndedAt <= to.Value);

    private static bool MatchesCompletionState(SessionMetadata session, SessionQueryRequest request)
    {
        var isCompleted = session.EndedAt.HasValue;
        return isCompleted
            ? request.IncludeCompleted is not false
            : request.IncludeActive is not false;
    }

    private static SessionId ParseSessionId(string sessionId)
        => Guid.TryParse(sessionId, out var guid) && guid != Guid.Empty
            ? new SessionId(guid)
            : throw new InvalidOperationException("Session id must be a non-empty GUID.");

    private static string ReadTopic(IReadOnlyDictionary<string, string>? attributes, string? fallback)
    {
        if (attributes is not null &&
            attributes.TryGetValue(TopicAttribute, out var topic) &&
            !string.IsNullOrWhiteSpace(topic))
        {
            return topic;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        throw new InvalidOperationException("Session record must include an MQTT topic.");
    }

    private static byte[] ReadPayload(object? payload)
        => payload switch
        {
            null => [],
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            JsonElement { ValueKind: JsonValueKind.String } element => Encoding.UTF8.GetBytes(element.GetString() ?? string.Empty),
            JsonElement element => Encoding.UTF8.GetBytes(element.GetRawText()),
            _ => JsonSerializer.SerializeToUtf8Bytes(payload)
        };

    private static MqttQualityOfServiceLevel ReadQos(IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null || !attributes.TryGetValue(QosAttribute, out var value))
        {
            return MqttQualityOfServiceLevel.AtMostOnce;
        }

        return value.Trim() switch
        {
            "0" => MqttQualityOfServiceLevel.AtMostOnce,
            "1" => MqttQualityOfServiceLevel.AtLeastOnce,
            "2" => MqttQualityOfServiceLevel.ExactlyOnce,
            var text when Enum.TryParse<MqttQualityOfServiceLevel>(text, ignoreCase: true, out var parsed) => parsed,
            _ => MqttQualityOfServiceLevel.AtMostOnce
        };
    }

    private static bool ReadRetain(IReadOnlyDictionary<string, string>? attributes)
        => attributes is not null &&
           attributes.TryGetValue(RetainAttribute, out var value) &&
           bool.TryParse(value, out var retain) &&
           retain;

    private static Dictionary<string, string> CopyDictionary(Dictionary<string, string>? source)
        => source is null
            ? []
            : new Dictionary<string, string>(source, StringComparer.Ordinal);

    private static SessionMetadata CopyMetadata(SessionMetadata source)
        => source with { Tags = CopyDictionary(source.Tags) };
}
