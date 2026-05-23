using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.MessageSource;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using Shouldly;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class MqttSourceComponentTests
{
    [Fact]
    public async Task StoredSessionSource_StreamsRepositoryMessages()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository(
            Stored(sessionId, "factory/two", DateTimeOffset.Parse("2026-05-15T10:00:01Z"), 2),
            Stored(sessionId, "factory/one", DateTimeOffset.Parse("2026-05-15T10:00:00Z"), 1));
        var source = new StoredSessionSourceComponent(repository, sessionId);
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        source.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await source.StartAsync();
        await sink.Completion;

        received.ShouldBe(["factory/one", "factory/two"]);
        repository.StreamedSessionIds.ShouldBe([sessionId]);
    }

    [Fact]
    public async Task StoredSessionSource_PublishesErrorsAndCompletesWhenRepositoryFails()
    {
        var source = new StoredSessionSourceComponent(new FailingMessageRepository(), SessionId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var messages = new List<MqttEnvelope>();
        var messageSink = new ActionBlock<MqttEnvelope>(messages.Add);

        source.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        source.Output.LinkTo(messageSink, new DataflowLinkOptions { PropagateCompletion = true });

        await source.StartAsync();
        await Task.WhenAll(errorSink.Completion, messageSink.Completion);

        messages.ShouldBeEmpty();
        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
    }

    [Fact]
    public async Task GeneratedSource_EmitsConfiguredMessages()
    {
        var source = new GeneratedMqttSourceComponent(
        [
            TestMqttSession.Message("factory/one"),
            TestMqttSession.Message("factory/two")
        ]);
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        source.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await source.StartAsync();
        await sink.Completion;

        received.ShouldBe(["factory/one", "factory/two"]);
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
        private readonly IReadOnlyList<StoredMessage> _messages = messages;

        public List<SessionId> StreamedSessionIds { get; } = [];

        public void Add(SessionId sessionId, MqttEnvelope envelope)
            => throw new NotSupportedException();

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
            => throw new NotSupportedException();

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
            StreamedSessionIds.Add(sessionId);
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

    private sealed class FailingMessageRepository : IMessageRepository
    {
        public void Add(SessionId sessionId, MqttEnvelope envelope)
            => throw new NotSupportedException();

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
            => throw new NotSupportedException();

        public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId)
            => throw new InvalidOperationException("read failed");

        public IReadOnlyList<StoredMessage> GetByTopic(string topic)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<StoredMessage> ReadBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (DateTimeOffset.UtcNow == DateTimeOffset.MinValue)
            {
                yield return null!;
            }

            await Task.Yield();
            throw new InvalidOperationException("read failed");
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

        public long CountBySession(SessionId sessionId) => 0;
    }
}
