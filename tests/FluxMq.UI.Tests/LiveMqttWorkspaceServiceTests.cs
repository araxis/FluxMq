using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
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
        var createdClients = new List<FakeFluxMqttClient>();
        var service = CreateService(profile =>
        {
            createdProfiles.Add(profile);
            var client = new FakeFluxMqttClient(profile);
            createdClients.Add(client);
            return client;
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
        service.State.ShouldBe(MqttClientState.Connected);
        service.Connections.ShouldHaveSingleItem().Profile.ClientId.ShouldBe("runtime-client");
        createdProfiles.ShouldHaveSingleItem().ClientId.ShouldStartWith("runtime-client-workspace-");
        createdProfiles.Single().ClientId.ShouldNotBe("runtime-client");
        createdClients.ShouldHaveSingleItem().ConnectCalls.ShouldBe(1);
        createdClients.Single().Subscriptions.ShouldContain(("factory/#", MqttQualityOfServiceLevel.AtMostOnce));
        await service.DisposeAsync();
    }

    [Fact]
    public async Task AddConnection_UsesBrokerMonitorSubscriptionByDefault()
    {
        var createdClients = new List<FakeFluxMqttClient>();
        var service = CreateService(profile =>
        {
            var client = new FakeFluxMqttClient(profile);
            createdClients.Add(client);
            return client;
        });

        service.AddConnection(new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "workspace-client"
        });

        await service.ConnectAsync(service.Connections.ShouldHaveSingleItem().Id);

        createdClients.ShouldHaveSingleItem().Subscriptions
            .Select(subscription => subscription.TopicFilter)
            .ShouldBe(["#", "$SYS/#"], ignoreOrder: true);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionsAsync_DoesNotReconnectAlreadyConnectedBroker()
    {
        var createdClients = new List<FakeFluxMqttClient>();
        var service = CreateService(profile =>
        {
            var client = new FakeFluxMqttClient(profile);
            createdClients.Add(client);
            return client;
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
        createdClients.ShouldHaveSingleItem().ConnectCalls.ShouldBe(1);
        service.Connections.ShouldHaveSingleItem().State.ShouldBe(MqttClientState.Connected);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionsAsync_TreatsDifferentResourceNamesAsDifferentBrokers()
    {
        var createdClients = new List<FakeFluxMqttClient>();
        var service = CreateService(profile =>
        {
            var client = new FakeFluxMqttClient(profile);
            createdClients.Add(client);
            return client;
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
        createdClients.Count.ShouldBe(2);
        createdClients.SelectMany(client => client.Subscriptions.Select(subscription => subscription.TopicFilter))
            .ShouldBe(["factory/one/#", "factory/two/#"], ignoreOrder: true);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectionsAsync_ReturnsFalseWhenBrokerConnectionFails()
    {
        var service = CreateService(profile => new FakeFluxMqttClient(profile, failConnect: true));
        var profile = new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        var ready = await service.EnsureConnectionsAsync([(profile, "#")]);

        ready.ShouldBeFalse();
        service.State.ShouldBe(MqttClientState.Faulted);
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "ConnectFailed");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task DisconnectAutoStartedConnectionsAsync_DisconnectsOnlyConnectionsStartedByEnsure()
    {
        var service = CreateService(profile => new FakeFluxMqttClient(profile));
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
        connections["manual-broker"].State.ShouldBe(MqttClientState.Connected);
        connections["app-broker"].State.ShouldBe(MqttClientState.Disconnected);
        service.State.ShouldBe(MqttClientState.Connected);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_ReturnsTrueWhenMessageWasPublished()
    {
        var createdClients = new List<FakeFluxMqttClient>();
        var service = CreateService(profile =>
        {
            var client = new FakeFluxMqttClient(profile);
            createdClients.Add(client);
            return client;
        });
        var connection = service.AddConnectionIfAbsent(
            new MqttConnectionProfile
            {
                Name = "local-broker",
                Host = "localhost",
                Port = 1883,
                ClientId = "workspace-client"
            },
            "#",
            "local-broker");
        await service.ConnectAsync(connection.Id);

        var published = await service.PublishAsync(
            connection.Id,
            "test",
            """{"hello":"fluxmq"}""",
            MqttQualityOfServiceLevel.AtMostOnce,
            retain: false);

        published.ShouldBeTrue();
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "Published");
        var message = createdClients.ShouldHaveSingleItem().Published.ShouldHaveSingleItem();
        message.Topic.ShouldBe("test");
        message.Payload.ShouldBe("""{"hello":"fluxmq"}"""u8.ToArray());
        message.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtMostOnce);
        message.Retain.ShouldBeFalse();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_ReturnsFalseWhenNoConnectedConnectionExists()
    {
        var service = CreateService(profile => new FakeFluxMqttClient(profile));

        var published = await service.PublishAsync("test", "{}");

        published.ShouldBeFalse();
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "NotConnected");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task RemoveConnectionsAsync_RemovesMatchingWorkspaceConnections()
    {
        var service = CreateService(profile => new FakeFluxMqttClient(profile));
        var profile = new MqttConnectionProfile
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "runtime-client"
        };

        service.AddConnectionIfAbsent(profile, "#", "broker1");
        service.AddConnectionIfAbsent(profile with { Port = 1884 }, "#", "broker2");

        await service.RemoveConnectionsAsync([("broker1", profile)]);

        service.Connections.ShouldHaveSingleItem().ResourceName.ShouldBe("broker2");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task CloseProjectAsync_RemovesClosedAppConnectionsOnlyWhenUnreferenced()
    {
        var live = CreateService(profile => new FakeFluxMqttClient(profile));
        var manager = new ProjectManagerService(new FlowDefinitionComposer(), live: live);
        var app1 = manager.NewProject();
        app1.SetDefinitionJson(ProjectWithBroker("broker1", 1883));
        var app2 = manager.NewProject();
        app2.SetDefinitionJson(ProjectWithBroker("broker2", 1884));
        live.AddConnectionIfAbsent(new MqttConnectionProfile { Name = "broker1", Host = "localhost", Port = 1883 }, "#", "broker1");
        live.AddConnectionIfAbsent(new MqttConnectionProfile { Name = "broker2", Host = "localhost", Port = 1884 }, "#", "broker2");

        await manager.CloseProjectAsync(app1);

        live.Connections.ShouldHaveSingleItem().ResourceName.ShouldBe("broker2");
        await manager.DisposeAsync();
        await live.DisposeAsync();
    }

    private static LiveMqttWorkspaceService CreateService(Func<MqttConnectionProfile, IFluxMqttClient> clientFactory)
        => new(
            new TopicIndex(),
            new FakeSessionRepository(),
            new FakeMessageRepository(),
            clientFactory);

    private static string ProjectWithBroker(string brokerName, int port)
        => $$"""
        {
          "FluxMq": {
            "FlowApplication": {
              "resources": {
                "{{brokerName}}": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": {
                      "name": "{{brokerName}}",
                      "host": "localhost",
                      "port": {{port}},
                      "clientId": "runtime-client"
                    }
                  }
                }
              }
            }
          }
        }
        """;

    private sealed class FakeFluxMqttClient(MqttConnectionProfile profile, bool failConnect = false) : IFluxMqttClient
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
        private readonly bool _failConnect = failConnect;
        private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];

        public MqttConnectionProfile Profile { get; } = profile;
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCalls { get; private set; }
        public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttClientState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            State = MqttClientState.Connecting;
            StateChanged?.Invoke(this, State);

            if (_failConnect)
            {
                State = MqttClientState.Faulted;
                StateChanged?.Invoke(this, State);
                throw new InvalidOperationException("connect failed");
            }

            State = MqttClientState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Disconnected;
            StateChanged?.Invoke(this, State);
            _messages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            CancellationToken ct = default)
            => SubscribeAsync(topicFilter, qos, receiveRetainedMessages: true, retainAsPublished: true, ct);

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos,
            bool receiveRetainedMessages,
            bool retainAsPublished = true,
            CancellationToken ct = default)
        {
            if (State != MqttClientState.Connected)
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
        {
            Published.Add(new PublishedMessage(topic, payload, qos, retain));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed record PublishedMessage(
        string Topic,
        byte[] Payload,
        MqttQualityOfServiceLevel QualityOfService,
        bool Retain);

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
