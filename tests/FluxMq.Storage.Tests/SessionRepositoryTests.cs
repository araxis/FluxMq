using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Storage;
using FluxMq.Storage.Repositories;
using LiteDB;

namespace FluxMq.Storage.Tests;

public class SessionRepositoryTests : IDisposable
{
    private readonly FluxDbContext _ctx = new(new LiteDatabase(":memory:"));
    private readonly LiteDbSessionRepository _repo;

    public SessionRepositoryTests() =>
        _repo = new LiteDbSessionRepository(_ctx);

    private static MqttConnectionProfile Profile(string name = "test") =>
        new() { Name = name };

    [Fact]
    public void Start_CreatesSession_WithCorrectProfile()
    {
        var profile = Profile("broker-a");

        var session = _repo.Start(profile);

        session.Id.Should().NotBe(SessionId.Empty);
        session.ProfileId.Should().Be(profile.Id);
        session.ProfileName.Should().Be("broker-a");
        session.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        session.EndedAt.Should().BeNull();
    }

    [Fact]
    public void Start_PersistsSession()
    {
        var session = _repo.Start(Profile());

        _repo.Get(session.Id).Should().NotBeNull();
    }

    [Fact]
    public void End_SetsEndedAt()
    {
        var session = _repo.Start(Profile());

        _repo.End(session.Id);

        _repo.Get(session.Id)!.EndedAt.Should()
            .NotBeNull()
            .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void End_DoesNotThrow_ForUnknownId()
    {
        var act = () => _repo.End(SessionId.New());
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAll_ReturnsMostRecentFirst()
    {
        _repo.Start(Profile("a"));
        _repo.Start(Profile("b"));
        _repo.Start(Profile("c"));

        var all = _repo.GetAll();

        all.Should().HaveCount(3);
        all[0].StartedAt.Should().BeOnOrAfter(all[1].StartedAt);
    }

    [Fact]
    public void Delete_RemovesSession()
    {
        var session = _repo.Start(Profile());

        _repo.Delete(session.Id).Should().BeTrue();
        _repo.Get(session.Id).Should().BeNull();
    }

    public void Dispose() => _ctx.Dispose();
}
