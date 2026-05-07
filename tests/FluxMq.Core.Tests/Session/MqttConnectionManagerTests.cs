using FluentAssertions;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using MQTTnet.Protocol;
using Polly;
using Polly.Retry;
using System.Threading.Channels;

namespace FluxMq.Core.Tests.Session;

public class MqttConnectionManagerTests
{
    // Instant-retry pipeline — no delays in tests
    private static readonly ResiliencePipeline InstantRetry =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 5, Delay = TimeSpan.Zero })
            .Build();

    private static (MqttConnectionManager manager, FakeMqttSession session) BuildManager(
        MqttConnectionProfile? profile = null,
        Func<Task>? onConnect = null)
    {
        profile ??= new MqttConnectionProfile { Name = "test" };
        var session = new FakeMqttSession(profile, onConnect);
        var manager = new MqttConnectionManager(_ => session, InstantRetry);
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
        var (manager, session) = BuildManager(profile);
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

        // Give reconnect task a moment to fire Reconnecting state
        await Task.Delay(50);

        received.Should().Contain(a => a.State == MqttSessionState.Faulted);
    }

    [Fact]
    public async Task Reconnect_TriggersReconnecting_State_OnFault()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var connectCount = 0;
        var reconnectedTcs = new TaskCompletionSource();

        // Fail once, then succeed — reconnect loop should complete after 1 retry
        var session = new FakeMqttSession(profile, onConnect: () =>
        {
            connectCount++;
            if (connectCount == 1) return Task.CompletedTask; // initial connect
            reconnectedTcs.TrySetResult();                    // reconnect succeeded
            return Task.CompletedTask;
        });

        var reconnectingStates = new List<MqttSessionState>();
        await using var manager = new MqttConnectionManager(_ => session, InstantRetry);
        manager.StateChanged += (_, a) => reconnectingStates.Add(a.State);

        await manager.ConnectAsync(profile);
        session.SimulateStateChange(MqttSessionState.Faulted);

        await reconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        reconnectingStates.Should().Contain(MqttSessionState.Reconnecting);
        reconnectingStates.Should().Contain(MqttSessionState.Connected);
    }

    [Fact]
    public async Task RemoveAsync_CancelsInProgressReconnect()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var connectGate = new SemaphoreSlim(0);
        var connectCount = 0;

        var session = new FakeMqttSession(profile, onConnect: async () =>
        {
            connectCount++;
            if (connectCount > 1)
                await connectGate.WaitAsync(); // block reconnect indefinitely
        });

        await using var manager = new MqttConnectionManager(_ => session, InstantRetry);

        await manager.ConnectAsync(profile);
        session.SimulateStateChange(MqttSessionState.Faulted);

        // Give the reconnect task time to start
        await Task.Delay(50);
        await manager.RemoveAsync(profile.Id);

        manager.Sessions.Should().NotContainKey(profile.Id);
        session.Disposed.Should().BeTrue();

        connectGate.Release(); // unblock so test teardown is clean
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
        }, InstantRetry);

        foreach (var p in profiles)
            await manager.ConnectAsync(p);

        await manager.DisposeAsync();

        sessions.Should().AllSatisfy(s => s.Disposed.Should().BeTrue());
    }
}

sealed class FakeMqttSession(
    MqttConnectionProfile profile,
    Func<Task>? onConnect = null,
    MqttSessionState initialState = MqttSessionState.Disconnected) : IMqttSession
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

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (onConnect is not null)
            await onConnect();
        State = MqttSessionState.Connected;
        StateChanged?.Invoke(this, State);
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
