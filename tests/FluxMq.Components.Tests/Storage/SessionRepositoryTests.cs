using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage;
using FluxMq.Components.Storage.Repositories;
using LiteDB;

namespace FluxMq.Components.Tests.Storage;

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

        session.Id.ShouldNotBe(SessionId.Empty);
        session.ProfileId.ShouldBe(profile.Id);
        session.ProfileName.ShouldBe("broker-a");
        session.StartedAt.ShouldBe(DateTimeOffset.UtcNow, tolerance: TimeSpan.FromSeconds(2));
        session.EndedAt.ShouldBeNull();
    }

    [Fact]
    public void Start_CreatesNamedProjectSession()
    {
        var session = _repo.Start(Profile("broker-a"), "morning capture", "factory floor");

        session.Name.ShouldBe("morning capture");
        session.ProjectName.ShouldBe("factory floor");
    }

    [Fact]
    public void Start_PersistsSession()
    {
        var session = _repo.Start(Profile());

        _repo.Get(session.Id).ShouldNotBeNull();
    }

    [Fact]
    public void End_SetsEndedAt()
    {
        var session = _repo.Start(Profile());

        _repo.End(session.Id);

        var endedAt = _repo.Get(session.Id)!.EndedAt;
        endedAt.ShouldNotBeNull();
        endedAt!.Value.ShouldBe(DateTimeOffset.UtcNow, tolerance: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void End_DoesNotThrow_ForUnknownId()
    {
        var act = () => _repo.End(SessionId.New());
        Should.NotThrow(act);
    }

    [Fact]
    public void GetAll_ReturnsMostRecentFirst()
    {
        _repo.Start(Profile("a"));
        _repo.Start(Profile("b"));
        _repo.Start(Profile("c"));

        var all = _repo.GetAll();

        all.Count.ShouldBe(3);
        all[0].StartedAt.ShouldBeGreaterThanOrEqualTo(all[1].StartedAt);
    }

    [Fact]
    public void Delete_RemovesSession()
    {
        var session = _repo.Start(Profile());

        _repo.Delete(session.Id).ShouldBeTrue();
        _repo.Get(session.Id).ShouldBeNull();
    }

    public void Dispose() => _ctx.Dispose();
}
