using FluentAssertions;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using MQTTnet.Protocol;
using System.Threading.Channels;

namespace FluxMq.Core.Tests.Session;

public class MqttConnectionManagerTests
{
    private static (MqttConnectionManager manager, FakeMqttSession session) BuildManager(
        MqttConnectionProfile? profile = null,
        MqttSessionState initialState = MqttSessionState.Disconnected)
    {
        profile ??= new MqttConnectionProfile { Name = "test" };
        var session = new FakeMqttSession(profile, initialState);
        var manager = new MqttConnectionManager(_ => session);
        return (manager, session);
    }

    [Fact]
    public async Task ConnectAsync_RegistersSession_AndReturnsIt()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, session) = BuildManager(profile);
        await using var _ = manager;

        var result = await manager.ConnectAsync(profile);

        result.Should().BeSameAs(session);
        manager.Sessions.Should().ContainKey(profile.Id);
    }

    [Fact]
    public async Task ConnectAsync_DuplicateProfileId_Throws()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, _) = BuildManager(profile);
        await using var _ = manager;

        await manager.ConnectAsync(profile);

        var act = async () => await manager.ConnectAsync(profile);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already active*");
    }

    [Fact]
    public async Task DisconnectAsync_CallsDisconnectOnSession()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, session) = BuildManager(profile, MqttSessionState.Connected);
        await using var _ = manager;

        await manager.ConnectAsync(profile);
        await manager.DisconnectAsync(profile.Id);

        session.DisconnectCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesSession_AndDisposesIt()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, session) = BuildManager(profile);
        await using var _ = manager;

        await manager.ConnectAsync(profile);
        await manager.RemoveAsync(profile.Id);

        manager.Sessions.Should().NotContainKey(profile.Id);
        session.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task StateChanged_Fires_WhenSessionRaisesStateChange()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, session) = BuildManager(profile);
        await using var _ = manager;
        var received = new List<SessionStateChangedEventArgs>();

        manager.StateChanged += (_, args) => received.Add(args);

        await manager.ConnectAsync(profile);
        session.SimulateStateChange(MqttSessionState.Faulted);

        received.Should().Contain(a =>
            a.SessionId == profile.Id &&
            a.State == MqttSessionState.Faulted);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllSessions()
    {
        var profiles = Enumerable.Range(0, 3)
            .Select(_ => new MqttConnectionProfile())
            .ToList();

        var sessions = new List<FakeMqttSession>();
        var manager = new MqttConnectionManager(p =>
        {
            var s = new FakeMqttSession(p);
            sessions.Add(s);
            return s;
        });

        foreach (var p in profiles)
            await manager.ConnectAsync(p);

        await manager.DisposeAsync();

        sessions.Should().AllSatisfy(s => s.Disposed.Should().BeTrue());
    }
}

sealed class FakeMqttSession(MqttConnectionProfile profile, MqttSessionState initialState = MqttSessionState.Disconnected) : IMqttSession
{
    private readonly Channel<MqttEnvelope> _channel = Channel.CreateUnbounded<MqttEnvelope>();

    public MqttConnectionProfile Profile { get; } = profile;
    public MqttSessionState State { get; private set; } = initialState;
    public ChannelReader<MqttEnvelope> Messages => _channel.Reader;
    public bool DisconnectCalled { get; private set; }
    public bool Disposed { get; private set; }

    public event EventHandler<MqttSessionState>? StateChanged;

    public void SimulateStateChange(MqttSessionState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        State = MqttSessionState.Connected;
        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        DisconnectCalled = true;
        State = MqttSessionState.Disconnected;
        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
