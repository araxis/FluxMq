using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Core.TopicIndex;
using FluxMq.UI.Services;
using MQTTnet.Protocol;
using Shouldly;
using System.Threading.Channels;

namespace FluxMq.UI.Tests;

public sealed class LiveMqttWorkspaceServiceTests
{
    [Fact]
    public async Task EnsureConnectionsAsync_AddsConnectsAndSubscribesProjectBroker()
    {
        var createdProfiles = new List<MqttConnectionProfile>();
        var createdSessions = new List<FakeMqttSession>();
        var service = CreateService(profile =>
        {
            createdProfiles.Add(profile);
            var session = new FakeMqttSession(profile);
            createdSessions.Add(session);
            return session;
        });
        var profile = new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        var ready = await service.EnsureConnectionsAsync([(profile, "factory/#")]);

        ready.ShouldBeTrue();
        service.State.ShouldBe(MqttSessionState.Connected);
        service.Connections.ShouldHaveSingleItem().Profile.ClientId.ShouldBe("runtime-client");
        createdProfiles.ShouldHaveSingleItem().ClientId.ShouldStartWith("runtime-client-workspace-");
        createdProfiles.Single().ClientId.ShouldNotBe("runtime-client");
        createdSessions.ShouldHaveSingleItem().ConnectCalls.ShouldBe(1);
        createdSessions.Single().Subscriptions.ShouldContain(("factory/#", MqttQualityOfServiceLevel.AtMostOnce));
        await service.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionsAsync_DoesNotReconnectAlreadyConnectedBroker()
    {
        var createdSessions = new List<FakeMqttSession>();
        var service = CreateService(profile =>
        {
            var session = new FakeMqttSession(profile);
            createdSessions.Add(session);
            return session;
        });
        var profile = new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        await service.EnsureConnectionsAsync([(profile, "#")]);
        var ready = await service.EnsureConnectionsAsync([(profile, "#")]);

        ready.ShouldBeTrue();
        createdSessions.ShouldHaveSingleItem().ConnectCalls.ShouldBe(1);
        service.Connections.ShouldHaveSingleItem().State.ShouldBe(MqttSessionState.Connected);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionsAsync_TreatsDifferentResourceNamesAsDifferentBrokers()
    {
        var createdSessions = new List<FakeMqttSession>();
        var service = CreateService(profile =>
        {
            var session = new FakeMqttSession(profile);
            createdSessions.Add(session);
            return session;
        });
        var profile = new MqttConnectionProfile
        {
            Name = "shared-endpoint",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        var ready = await service.EnsureConnectionsAsync(
        [
            ("broker1", profile, "factory/one/#"),
            ("broker2", profile, "factory/two/#")
        ]);

        ready.ShouldBeTrue();
        service.Connections.Select(connection => connection.ResourceName)
            .ShouldBe(["broker1", "broker2"], ignoreOrder: true);
        createdSessions.Count.ShouldBe(2);
        createdSessions.SelectMany(session => session.Subscriptions.Select(subscription => subscription.TopicFilter))
            .ShouldBe(["factory/one/#", "factory/two/#"], ignoreOrder: true);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionsAsync_ReturnsFalseWhenBrokerConnectionFails()
    {
        var service = CreateService(profile => new FakeMqttSession(profile, failConnect: true));
        var profile = new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        var ready = await service.EnsureConnectionsAsync([(profile, "#")]);

        ready.ShouldBeFalse();
        service.State.ShouldBe(MqttSessionState.Faulted);
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "ConnectFailed");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task DisconnectAutoStartedConnectionsAsync_DisconnectsOnlyConnectionsStartedByEnsure()
    {
        var service = CreateService(profile => new FakeMqttSession(profile));
        var profile = new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        var manual = service.AddConnectionIfAbsent(profile, "#", "manual-broker");
        await service.ConnectAsync(manual.Id);

        await service.EnsureConnectionsAsync(
        [
            ("manual-broker", profile, "#"),
            ("app-broker", profile, "#")
        ]);
        await service.DisconnectAutoStartedConnectionsAsync();

        var connections = service.Connections.ToDictionary(connection => connection.ResourceName);
        connections["manual-broker"].State.ShouldBe(MqttSessionState.Connected);
        connections["app-broker"].State.ShouldBe(MqttSessionState.Disconnected);
        service.State.ShouldBe(MqttSessionState.Connected);
        await service.DisposeAsync();
    }

    private static LiveMqttWorkspaceService CreateService(Func<MqttConnectionProfile, IMqttSession> sessionFactory)
        => new(
            new TopicIndex(),
            new FakeSessionRepository(),
            new FakeMessageRepository(),
            sessionFactory);

    private sealed class FakeMqttSession(MqttConnectionProfile profile, bool failConnect = false) : IMqttSession
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
        private readonly bool _failConnect = failConnect;
        private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];

        public MqttConnectionProfile Profile { get; } = profile;
        public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCalls { get; private set; }
        public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;

        public event EventHandler<MqttSessionState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            State = MqttSessionState.Connecting;
            StateChanged?.Invoke(this, State);

            if (_failConnect)
            {
                State = MqttSessionState.Faulted;
                StateChanged?.Invoke(this, State);
                throw new InvalidOperationException("connect failed");
            }

            State = MqttSessionState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttSessionState.Disconnected;
            StateChanged?.Invoke(this, State);
            _messages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            CancellationToken ct = default)
        {
            if (State != MqttSessionState.Connected)
            {
                throw new InvalidOperationException("MQTT client is not connected.");
            }

            _subscriptions.Add((topicFilter, qos));
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;

        public Task PublishAsync(
            string topic,
            byte[] payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public StoredSession Start(MqttConnectionProfile profile, string? name = null, string? projectName = null)
            => StoredSession.From(profile, name, projectName);

        public void End(SessionId sessionId)
        {
        }

        public StoredSession? Get(SessionId sessionId) => null;
        public IReadOnlyList<StoredSession> GetAll() => [];
        public bool Delete(SessionId sessionId) => false;
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public void Add(SessionId sessionId, MqttEnvelope envelope)
        {
        }

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
        {
        }

        public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId) => [];
        public IReadOnlyList<StoredMessage> GetByTopic(string topic) => [];
        public long CountBySession(SessionId sessionId) => 0;

        public async IAsyncEnumerable<StoredMessage> ReadBySessionAsync(
            SessionId sessionId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<MqttEnvelope> ReadEnvelopesBySessionAsync(
            SessionId sessionId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
