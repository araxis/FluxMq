using FluxFlow.Components.Sessions.Contracts;
using FluxMq.Components.Storage;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using MQTTnet.Protocol;
using Shouldly;
using System.Runtime.CompilerServices;
using System.Text;

namespace FluxMq.Components.Tests.Storage;

public sealed class FluxMqSessionStoreTests
{
    [Fact]
    public async Task AppendMessageAsync_StoresMqttEnvelopeRecord()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository();
        var store = new FluxMqSessionStore(repository);
        var envelope = new MqttEnvelope
        {
            Topic = "factory/line/1",
            Payload = [1, 2, 3],
            ReceivedAt = DateTimeOffset.Parse("2026-06-01T10:00:00Z"),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        };

        var record = await store.AppendMessageAsync(new SessionAppendRequest
        {
            Session = new SessionMetadata
            {
                SessionId = sessionId.ToString(),
                StartedAt = envelope.ReceivedAt
            },
            Input = FluxMqSessionStore.ToRecordInput(envelope),
            Sequence = 1,
            Timestamp = envelope.ReceivedAt
        });

        record.SessionId.ShouldBe(sessionId.ToString());
        record.Sequence.ShouldBe(1);
        record.Attributes[FluxMqSessionStore.TopicAttribute].ShouldBe("factory/line/1");
        repository.Recorded.ShouldHaveSingleItem().Envelope.ShouldBe(envelope);
    }

    [Fact]
    public async Task ReadMessagesAsync_StreamsStoredMessagesAsSessionRecords()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository(
            Stored(sessionId, "factory/one", DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 1),
            Stored(sessionId, "factory/two", DateTimeOffset.Parse("2026-06-01T10:00:01Z"), 2));
        var store = new FluxMqSessionStore(repository);
        var records = new List<SessionRecord>();

        await foreach (var record in store.ReadMessagesAsync(new SessionReadRequest { SessionId = sessionId.ToString() }))
        {
            records.Add(record);
        }

        records.Select(record => record.Name).ShouldBe(["factory/one", "factory/two"]);
        records.Select(record => FluxMqSessionStore.ToEnvelope(record).Topic).ShouldBe(["factory/one", "factory/two"]);
    }

    [Fact]
    public async Task StartSessionAsync_ReturnsExistingMessageCount()
    {
        var sessionId = SessionId.New();
        var store = new FluxMqSessionStore(new FakeMessageRepository(
            Stored(sessionId, "factory/one", DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 1),
            Stored(sessionId, "factory/two", DateTimeOffset.Parse("2026-06-01T10:00:01Z"), 2)));

        var session = await store.StartSessionAsync(new SessionStartRequest
        {
            SessionId = sessionId.ToString(),
            StartedAt = DateTimeOffset.Parse("2026-06-01T10:00:00Z")
        });

        session.MessageCount.ShouldBe(2);
    }

    [Fact]
    public async Task QuerySessionsAsync_ReturnsFilteredSessionMetadata()
    {
        var repository = new FakeMessageRepository();
        var store = new FluxMqSessionStore(repository);

        var first = await store.StartSessionAsync(new SessionStartRequest
        {
            SessionId = SessionId.New().ToString(),
            Name = "factory-a",
            StartedAt = DateTimeOffset.Parse("2026-06-01T10:00:00Z"),
            Tags = new Dictionary<string, string> { ["project"] = "app1" }
        });
        await store.StartSessionAsync(new SessionStartRequest
        {
            SessionId = SessionId.New().ToString(),
            Name = "debug",
            StartedAt = DateTimeOffset.Parse("2026-06-01T11:00:00Z"),
            Tags = new Dictionary<string, string> { ["project"] = "app2" }
        });
        await store.CompleteSessionAsync(new SessionCompleteRequest
        {
            Session = first,
            EndedAt = DateTimeOffset.Parse("2026-06-01T10:05:00Z"),
            MessageCount = 3
        });

        var result = await store.QuerySessionsAsync(new SessionQueryRequest
        {
            NamePrefix = "factory",
            Tags = new Dictionary<string, string> { ["project"] = "app1" },
            IncludeActive = false,
            IncludeCompleted = true,
            Limit = 10
        });

        var session = result.ShouldHaveSingleItem();
        session.SessionId.ShouldBe(first.SessionId);
        session.Name.ShouldBe("factory-a");
        session.EndedAt.ShouldBe(DateTimeOffset.Parse("2026-06-01T10:05:00Z"));
        session.MessageCount.ShouldBe(3);
        session.Tags["project"].ShouldBe("app1");
    }

    [Fact]
    public void ToEnvelope_ReadsMqttAttributes()
    {
        var record = new SessionRecord
        {
            SessionId = SessionId.New().ToString(),
            Sequence = 1,
            Timestamp = DateTimeOffset.Parse("2026-06-01T10:00:00Z"),
            Payload = "hello",
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FluxMqSessionStore.TopicAttribute] = "factory/text",
                [FluxMqSessionStore.QosAttribute] = "2",
                [FluxMqSessionStore.RetainAttribute] = "true"
            }
        };

        var envelope = FluxMqSessionStore.ToEnvelope(record);

        envelope.Topic.ShouldBe("factory/text");
        envelope.Payload.ShouldBe(Encoding.UTF8.GetBytes("hello"));
        envelope.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.ExactlyOnce);
        envelope.Retain.ShouldBeTrue();
    }

    private static StoredMessage Stored(SessionId sessionId, string topic, DateTimeOffset receivedAt, long sequence) => new()
    {
        SessionId = sessionId,
        Sequence = sequence,
        Topic = topic,
        Payload = [],
        ReceivedAt = receivedAt
    };

    private sealed class FakeMessageRepository(params StoredMessage[] messages) : IMessageRepository
    {
        private readonly List<StoredMessage> _messages = [.. messages];

        public List<RecordedMessage> Recorded { get; } = [];

        public void Add(SessionId sessionId, MqttEnvelope envelope)
        {
            Recorded.Add(new RecordedMessage(sessionId, envelope));
            _messages.Add(StoredMessage.From(sessionId, envelope, _messages.Count(message => message.SessionId == sessionId) + 1));
        }

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
        {
            foreach (var envelope in envelopes)
            {
                Add(sessionId, envelope);
            }
        }

        public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId)
            => _messages
                .Where(message => message.SessionId == sessionId)
                .OrderBy(message => message.ReceivedAt)
                .ThenBy(message => message.Sequence)
                .ToArray();

        public IReadOnlyList<StoredMessage> GetByTopic(string topic)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<StoredMessage> ReadBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var message in GetBySession(sessionId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<MqttEnvelope> ReadEnvelopesBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var message in ReadBySessionAsync(sessionId, cancellationToken))
            {
                yield return message.ToEnvelope();
            }
        }

        public long CountBySession(SessionId sessionId)
            => _messages.Count(message => message.SessionId == sessionId);
    }

    private sealed record RecordedMessage(SessionId SessionId, MqttEnvelope Envelope);
}
