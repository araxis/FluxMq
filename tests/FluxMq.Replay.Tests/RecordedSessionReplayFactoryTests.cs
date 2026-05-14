using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using FluxMq.Replay;
using FluxMq.Storage.Models;
using FluxMq.Storage.Repositories;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Replay.Tests;

public sealed class RecordedSessionReplayFactoryTests
{
    [Fact]
    public async Task Create_LoadsStoredMessagesAndReplaysAsEnvelopes()
    {
        var sessionId = SessionId.New();
        var startedAt = DateTimeOffset.Parse("2026-05-08T10:00:00Z");
        var repository = new FakeMessageRepository(
            Stored(sessionId, "factory/2", startedAt.AddSeconds(2)),
            Stored(sessionId, "factory/1", startedAt));
        var factory = new RecordedSessionReplayFactory(repository, delayAsync: (_, _) => ValueTask.CompletedTask);
        var component = factory.Create(sessionId);
        var received = new List<string>();
        var sink = new ActionBlock<MqttEnvelope>(message => received.Add(message.Topic));

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await component.StartAsync();
        await sink.Completion;

        received.ShouldBe(new[] { "factory/1", "factory/2" });
        repository.RequestedSessionIds.ShouldBe(new[] { sessionId });
    }

    [Fact]
    public void Create_AppliesReplayOptions()
    {
        var sessionId = SessionId.New();
        var nodeId = FlowNodeId.New();
        var repository = new FakeMessageRepository();
        var factory = new RecordedSessionReplayFactory(repository);

        var component = factory.Create(sessionId, new RecordedSessionReplayOptions
        {
            NodeId = nodeId,
            Speed = 4,
            BoundedCapacity = 32
        });

        component.Id.ShouldBe(nodeId);
        component.Speed.ShouldBe(4);
    }

    [Fact]
    public async Task Create_AllowsEmptyRecordedSession()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository();
        var factory = new RecordedSessionReplayFactory(repository);
        var component = factory.Create(sessionId);
        var received = new List<MqttEnvelope>();
        var sink = new ActionBlock<MqttEnvelope>(received.Add);

        component.Output.LinkTo(sink, new DataflowLinkOptions { PropagateCompletion = true });

        await component.StartAsync();
        await sink.Completion;

        received.ShouldBeEmpty();
        component.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    private static StoredMessage Stored(SessionId sessionId, string topic, DateTimeOffset receivedAt) => new()
    {
        SessionId = sessionId,
        Topic = topic,
        Payload = [],
        ReceivedAt = receivedAt
    };

    private sealed class FakeMessageRepository(params StoredMessage[] messages) : IMessageRepository
    {
        private readonly IReadOnlyList<StoredMessage> _messages = messages;

        public List<SessionId> RequestedSessionIds { get; } = [];

        public void Add(SessionId sessionId, MqttEnvelope envelope)
            => throw new NotSupportedException();

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
            => throw new NotSupportedException();

        public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId)
        {
            RequestedSessionIds.Add(sessionId);
            return _messages
                .Where(message => message.SessionId == sessionId)
                .OrderBy(message => message.ReceivedAt)
                .ToArray();
        }

        public IReadOnlyList<StoredMessage> GetByTopic(string topic)
            => throw new NotSupportedException();

        public long CountBySession(SessionId sessionId)
            => _messages.Count(message => message.SessionId == sessionId);
    }
}
