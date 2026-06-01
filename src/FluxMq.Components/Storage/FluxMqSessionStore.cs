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

    public Task<SessionMetadata?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var id = ParseSessionId(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
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

        return Task.FromResult(new SessionMetadata
        {
            SessionId = id.ToString(),
            Name = request.Name,
            StartedAt = request.StartedAt,
            Notes = request.Notes,
            Tags = CopyDictionary(request.Tags),
            MessageCount = _messages.CountBySession(id)
        });
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
        return Task.FromResult(ToRecord(id, envelope, _messages.CountBySession(id)));
    }

    public Task<SessionMetadata> CompleteSessionAsync(
        SessionCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(request.Session with
        {
            EndedAt = request.EndedAt,
            MessageCount = request.MessageCount
        });
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
}
