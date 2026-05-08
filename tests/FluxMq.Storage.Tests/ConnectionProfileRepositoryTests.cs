using FluentAssertions;
using FluxMq.Core.Models;
using FluxMq.Storage;
using FluxMq.Storage.Repositories;
using LiteDB;

namespace FluxMq.Storage.Tests;

public class ConnectionProfileRepositoryTests : IDisposable
{
    private readonly FluxDbContext _ctx = new(new LiteDatabase(":memory:"));
    private readonly LiteDbConnectionProfileRepository _repo;

    public ConnectionProfileRepositoryTests() =>
        _repo = new LiteDbConnectionProfileRepository(_ctx);

    [Fact]
    public void Save_And_Get_RoundTrips_Profile()
    {
        var profile = new MqttConnectionProfile { Name = "Local", Host = "localhost", Port = 1883 };

        _repo.Save(profile);
        var loaded = _repo.Get(profile.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Local");
        loaded.Host.Should().Be("localhost");
        loaded.Port.Should().Be(1883);
    }

    [Fact]
    public void Save_Upserts_ExistingProfile()
    {
        var profile = new MqttConnectionProfile { Name = "Original" };
        _repo.Save(profile);

        var updated = profile with { Name = "Updated" };
        _repo.Save(updated);

        _repo.GetAll().Should().ContainSingle()
            .Which.Name.Should().Be("Updated");
    }

    [Fact]
    public void GetAll_ReturnsAllSavedProfiles()
    {
        _repo.Save(new MqttConnectionProfile { Name = "A" });
        _repo.Save(new MqttConnectionProfile { Name = "B" });
        _repo.Save(new MqttConnectionProfile { Name = "C" });

        _repo.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownId()
    {
        _repo.Get(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void Delete_RemovesProfile_AndReturnsTrue()
    {
        var profile = new MqttConnectionProfile { Name = "ToDelete" };
        _repo.Save(profile);

        var deleted = _repo.Delete(profile.Id);

        deleted.Should().BeTrue();
        _repo.Get(profile.Id).Should().BeNull();
    }

    [Fact]
    public void Delete_ReturnsFalse_ForUnknownId()
    {
        _repo.Delete(Guid.NewGuid()).Should().BeFalse();
    }

    public void Dispose() => _ctx.Dispose();
}
