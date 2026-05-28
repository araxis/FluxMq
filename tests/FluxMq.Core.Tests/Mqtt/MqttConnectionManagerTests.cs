using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using MQTTnet.Protocol;
using Polly;
using Polly.Retry;
using System.Threading.Channels;

namespace FluxMq.Core.Tests.Mqtt;

public class MqttConnectionManagerTests
{
    // Instant-retry pipeline — no delays in tests
    private static readonly ResiliencePipeline InstantRetry =
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 5, Delay = TimeSpan.Zero })
            .Build();

    private static (MqttConnectionManager manager, FakeFluxMqttClient client) BuildManager(
        MqttConnectionProfile? profile = null,
        Func<Task>? onConnect = null)
    {
        profile ??= new MqttConnectionProfile { Name = "test" };
        var client = new FakeFluxMqttClient(profile, onConnect);
        var manager = new MqttConnectionManager(_ => client, InstantRetry);
        return (manager, client);
    }

    [Fact]
    public async Task ConnectAsync_RegistersClient_AndReturnsIt()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, client) = BuildManager(profile);
        await using var _ = manager;

        var result = await manager.ConnectAsync(profile);

        result.ShouldBeSameAs(client);
        manager.Clients.ContainsKey(profile.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectAsync_DuplicateProfileId_Throws()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, _) = BuildManager(profile);
        await using var _ = manager;

        await manager.ConnectAsync(profile);

        var act = async () => await manager.ConnectAsync(profile);
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("already active");
    }

    [Fact]
    public async Task DisconnectAsync_CallsDisconnectOnClient()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, client) = BuildManager(profile);
        await using var _ = manager;

        await manager.ConnectAsync(profile);
        await manager.DisconnectAsync(profile.Id);

        client.DisconnectCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesClient_AndDisposesIt()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, client) = BuildManager(profile);
        await using var _ = manager;

        await manager.ConnectAsync(profile);
        await manager.RemoveAsync(profile.Id);

        manager.Clients.ContainsKey(profile.Id).ShouldBeFalse();
        client.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task StateChanged_Fires_WhenClientRaisesStateChange()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var (manager, client) = BuildManager(profile);
        await using var _ = manager;
        var received = new List<MqttClientStateChangedEventArgs>();

        manager.StateChanged += (_, args) => received.Add(args);

        await manager.ConnectAsync(profile);
        client.SimulateStateChange(MqttClientState.Faulted);

        // Give reconnect task a moment to fire Reconnecting state
        await Task.Delay(50);

        received.ShouldContain(a => a.State == MqttClientState.Faulted);
    }

    [Fact]
    public async Task Reconnect_TriggersReconnecting_State_OnFault()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var connectCount = 0;
        var reconnectedTcs = new TaskCompletionSource();

        // Fail once, then succeed — reconnect loop should complete after 1 retry
        var client = new FakeFluxMqttClient(profile, onConnect: () =>
        {
            connectCount++;
            if (connectCount == 1) return Task.CompletedTask; // initial connect
            reconnectedTcs.TrySetResult();                    // reconnect succeeded
            return Task.CompletedTask;
        });

        var reconnectingStates = new List<MqttClientState>();
        await using var manager = new MqttConnectionManager(_ => client, InstantRetry);
        manager.StateChanged += (_, a) => reconnectingStates.Add(a.State);

        await manager.ConnectAsync(profile);
        client.SimulateStateChange(MqttClientState.Faulted);

        await reconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        reconnectingStates.ShouldContain(MqttClientState.Reconnecting);
        reconnectingStates.ShouldContain(MqttClientState.Connected);
    }

    [Fact]
    public async Task RemoveAsync_CancelsInProgressReconnect()
    {
        var profile = new MqttConnectionProfile { Name = "test" };
        var connectGate = new SemaphoreSlim(0);
        var connectCount = 0;

        var client = new FakeFluxMqttClient(profile, onConnect: async () =>
        {
            connectCount++;
            if (connectCount > 1)
                await connectGate.WaitAsync(); // block reconnect indefinitely
        });

        await using var manager = new MqttConnectionManager(_ => client, InstantRetry);

        await manager.ConnectAsync(profile);
        client.SimulateStateChange(MqttClientState.Faulted);

        // Give the reconnect task time to start
        await Task.Delay(50);
        await manager.RemoveAsync(profile.Id);

        manager.Clients.ContainsKey(profile.Id).ShouldBeFalse();
        client.Disposed.ShouldBeTrue();

        connectGate.Release(); // unblock so test teardown is clean
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllClients()
    {
        var profiles = Enumerable.Range(0, 3)
            .Select(_ => new MqttConnectionProfile())
            .ToList();

        var clients = new List<FakeFluxMqttClient>();
        var manager = new MqttConnectionManager(p =>
        {
            var s = new FakeFluxMqttClient(p);
            clients.Add(s);
            return s;
        }, InstantRetry);

        foreach (var p in profiles)
            await manager.ConnectAsync(p);

        await manager.DisposeAsync();

        foreach (var item in clients) { item.Disposed.ShouldBeTrue(); }
    }
}

sealed class FakeFluxMqttClient(
    MqttConnectionProfile profile,
    Func<Task>? onConnect = null,
    MqttClientState initialState = MqttClientState.Disconnected) : IFluxMqttClient
{
    private readonly Channel<MqttEnvelope> _channel = Channel.CreateUnbounded<MqttEnvelope>();

    public MqttConnectionProfile Profile { get; } = profile;
    public MqttClientState State { get; private set; } = initialState;
    public ChannelReader<MqttEnvelope> Messages => _channel.Reader;
    public bool DisconnectCalled { get; private set; }
    public bool Disposed { get; private set; }

    public event EventHandler<MqttClientState>? StateChanged;

    public void SimulateStateChange(MqttClientState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (onConnect is not null)
            await onConnect();
        State = MqttClientState.Connected;
        StateChanged?.Invoke(this, State);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        DisconnectCalled = true;
        State = MqttClientState.Disconnected;
        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default) => Task.CompletedTask;
    public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos, bool receiveRetainedMessages, bool retainAsPublished = true, CancellationToken ct = default) => Task.CompletedTask;
    public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
