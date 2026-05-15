using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage;
using FluxMq.Components.Storage.Repositories;
using LiteDB;

namespace FluxMq.Components.Tests.Storage;

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

        loaded.ShouldNotBeNull();
        loaded!.Name.ShouldBe("Local");
        loaded.Host.ShouldBe("localhost");
        loaded.Port.ShouldBe(1883);
    }

    [Fact]
    public void Save_Upserts_ExistingProfile()
    {
        var profile = new MqttConnectionProfile { Name = "Original" };
        _repo.Save(profile);

        var updated = profile with { Name = "Updated" };
        _repo.Save(updated);

        _repo.GetAll().ShouldHaveSingleItem().Name.ShouldBe("Updated");
    }

    [Fact]
    public void GetAll_ReturnsAllSavedProfiles()
    {
        _repo.Save(new MqttConnectionProfile { Name = "A" });
        _repo.Save(new MqttConnectionProfile { Name = "B" });
        _repo.Save(new MqttConnectionProfile { Name = "C" });

        _repo.GetAll().Count.ShouldBe(3);
    }

    [Fact]
    public void Get_ReturnsNull_ForUnknownId()
    {
        _repo.Get(ConnectionProfileId.New()).ShouldBeNull();
    }

    [Fact]
    public void Delete_RemovesProfile_AndReturnsTrue()
    {
        var profile = new MqttConnectionProfile { Name = "ToDelete" };
        _repo.Save(profile);

        var deleted = _repo.Delete(profile.Id);

        deleted.ShouldBeTrue();
        _repo.Get(profile.Id).ShouldBeNull();
    }

    [Fact]
    public void Delete_ReturnsFalse_ForUnknownId()
    {
        _repo.Delete(ConnectionProfileId.New()).ShouldBeFalse();
    }

    public void Dispose() => _ctx.Dispose();
}
