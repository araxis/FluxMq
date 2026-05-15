using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage;
using FluxMq.Components.Storage.Repositories;
using LiteDB;
using MQTTnet.Protocol;

namespace FluxMq.Components.Tests.Storage;

public class MessageRepositoryTests : IDisposable
{
    private readonly FluxDbContext _ctx = new(new LiteDatabase(":memory:"));
    private readonly LiteDbMessageRepository _repo;
    private readonly SessionId _sessionId = SessionId.New();

    public MessageRepositoryTests() =>
        _repo = new LiteDbMessageRepository(_ctx);

    private static MqttEnvelope Envelope(string topic, string payload = "{}") =>
        new()
        {
            Topic = topic,
            Payload = System.Text.Encoding.UTF8.GetBytes(payload),
            ReceivedAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public void Add_StoresMessage_RetrievableBySession()
    {
        _repo.Add(_sessionId, Envelope("sensors/temp", "{\"v\":21}"));

        var messages = _repo.GetBySession(_sessionId);

        messages.ShouldHaveSingleItem().Topic.ShouldBe("sensors/temp");
    }

    [Fact]
    public void AddBatch_StoresAllMessages()
    {
        var envelopes = Enumerable.Range(1, 5)
            .Select(i => Envelope($"topic/{i}"));

        _repo.AddBatch(_sessionId, envelopes);

        _repo.GetBySession(_sessionId).Count.ShouldBe(5);
    }

    [Fact]
    public void GetBySession_ReturnsOnlyThatSessionsMessages()
    {
        var otherId = SessionId.New();
        _repo.Add(_sessionId, Envelope("a/b"));
        _repo.Add(otherId, Envelope("c/d"));

        _repo.GetBySession(_sessionId).ShouldHaveSingleItem().Topic.ShouldBe("a/b");
    }

    [Fact]
    public void GetByTopic_ReturnsMatchingMessages_AcrossSessions()
    {
        _repo.Add(_sessionId, Envelope("sensors/temp"));
        _repo.Add(SessionId.New(), Envelope("sensors/temp"));
        _repo.Add(_sessionId, Envelope("sensors/humidity"));

        _repo.GetByTopic("sensors/temp").Count.ShouldBe(2);
    }

    [Fact]
    public void GetBySession_OrderedByReceivedAt()
    {
        // Insert in reverse order; i=5 → newest (-1s), i=1 → oldest (-5s)
        // so ascending ReceivedAt produces ascending topic names t/1..t/5
        for (var i = 5; i >= 1; i--)
            _repo.Add(_sessionId, Envelope($"t/{i}") with
            {
                ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(i - 6)
            });

        var topics = _repo.GetBySession(_sessionId).Select(m => m.Topic).ToList();

        topics.ShouldBeInOrder();
    }

    [Fact]
    public void CountBySession_ReturnsCorrectCount()
    {
        _repo.Add(_sessionId, Envelope("a"));
        _repo.Add(_sessionId, Envelope("b"));
        _repo.Add(SessionId.New(), Envelope("c"));

        _repo.CountBySession(_sessionId).ShouldBe(2);
    }

    [Fact]
    public void ToEnvelope_RoundTrips_Payload()
    {
        var original = new MqttEnvelope
        {
            Topic = "test/topic",
            Payload = [1, 2, 3],
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _repo.Add(_sessionId, original);
        var restored = _repo.GetBySession(_sessionId).Single().ToEnvelope();

        restored.Topic.ShouldBe(original.Topic);
        restored.Payload.ShouldBe(original.Payload);
        restored.QualityOfService.ShouldBe(original.QualityOfService);
        restored.Retain.ShouldBe(original.Retain);
    }

    public void Dispose() => _ctx.Dispose();
}
