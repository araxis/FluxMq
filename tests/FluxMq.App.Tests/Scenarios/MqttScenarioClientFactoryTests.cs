using FluxMq.App.Scenarios;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttPublisher;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using MQTTnet.Protocol;
using Shouldly;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace FluxMq.App.Tests.Scenarios;

public sealed class MqttScenarioClientFactoryTests
{
    [Fact]
    public async Task ApplicationDefinitionFactory_CreatesIsolatedSessionFromSharedAppResource()
    {
        var capturedProfiles = new List<MqttConnectionProfile>();
        var definition = new ApplicationDefinition
        {
            Resources =
            {
                ["shared-broker"] = MqttConnectionResource("""
                    {
                      "name": "shared-broker",
                      "host": "broker.local",
                      "port": 1884,
                      "clientId": "app-client",
                      "useTls": true,
                      "username": "tester",
                      "password": "secret",
                      "keepAliveSeconds": 30,
                      "cleanStart": false
                    }
                    """)
            }
        };
        var factory = new ApplicationDefinitionMqttScenarioClientFactory(
            definition,
            profile =>
            {
                capturedProfiles.Add(profile);
                return new FakeMqttSession(profile);
            });

        await using var client = factory.CreateClient("shared-broker");

        var scenarioProfile = capturedProfiles.ShouldHaveSingleItem();
        client.Profile.ShouldBeSameAs(scenarioProfile);
        scenarioProfile.Name.ShouldBe("shared-broker");
        scenarioProfile.Host.ShouldBe("broker.local");
        scenarioProfile.Port.ShouldBe(1884);
        scenarioProfile.UseTls.ShouldBeTrue();
        scenarioProfile.Username.ShouldBe("tester");
        scenarioProfile.Password.ShouldBe("secret");
        scenarioProfile.KeepAlive.ShouldBe(TimeSpan.FromSeconds(30));
        scenarioProfile.CleanStart.ShouldBeTrue();
        scenarioProfile.ClientId.ShouldStartWith("fluxmq-test-");
        scenarioProfile.ClientId.Length.ShouldBeLessThanOrEqualTo(23);
        scenarioProfile.ClientId.ShouldNotBe("app-client");
    }

    [Fact]
    public async Task RuntimeFactory_CreatesIsolatedSessionFromRunningAppResource()
    {
        var appProfile = new MqttConnectionProfile
        {
            Name = "runtime-broker",
            Host = "runtime.local",
            Port = 2883,
            ClientId = "runtime-client",
            CleanStart = false
        };
        var connection = new MqttConnectionComponent(
            new FakeMqttSession(appProfile),
            disposeSessionOnDispose: false);
        var resource = RuntimeNode.Create(
            new NodeAddress(WellKnownScopes.Resources, new NodeName("runtime-broker")),
            connection);
        await using var runtime = new ApplicationRuntime(
            [resource],
            [],
            [resource]);
        var capturedProfiles = new List<MqttConnectionProfile>();
        var factory = new RuntimeMqttScenarioClientFactory(
            runtime,
            profile =>
            {
                capturedProfiles.Add(profile);
                return new FakeMqttSession(profile);
            });

        await using var client = factory.CreateClient("runtime-broker");

        var scenarioProfile = capturedProfiles.ShouldHaveSingleItem();
        client.Profile.ShouldBeSameAs(scenarioProfile);
        scenarioProfile.Name.ShouldBe("runtime-broker");
        scenarioProfile.Host.ShouldBe("runtime.local");
        scenarioProfile.Port.ShouldBe(2883);
        scenarioProfile.Id.ShouldNotBe(appProfile.Id);
        scenarioProfile.CleanStart.ShouldBeTrue();
        scenarioProfile.ClientId.ShouldStartWith("fluxmq-test-");
        scenarioProfile.ClientId.ShouldNotBe("runtime-client");
    }

    [Fact]
    public async Task Publisher_PublishesThroughScenarioClientFactory()
    {
        var client = new FakeMqttSession(new MqttConnectionProfile { Name = "shared-broker" });
        var factory = new FakeScenarioClientFactory(client);
        var publisher = new ApplicationDefinitionMqttScenarioPublisher(factory);
        var payload = Encoding.UTF8.GetBytes("""{"value":12}""");

        await publisher.PublishAsync(
            "shared-broker",
            new MqttPublishRequest
            {
                Topic = "fluxmq/sample/request",
                Payload = payload,
                QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
                Retain = true
            });

        factory.ConnectionNames.ShouldBe(["shared-broker"]);
        client.ConnectCount.ShouldBe(1);
        client.DisposeCount.ShouldBe(1);
        var published = client.Published.ShouldHaveSingleItem();
        published.Topic.ShouldBe("fluxmq/sample/request");
        published.Payload.ShouldBe(payload);
        published.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        published.Retain.ShouldBeTrue();
    }

    private static NodeDefinition MqttConnectionResource(string profileJson)
        => new()
        {
            Type = PipelineFlowNodeTypes.Connection,
            Configuration =
            {
                ["profile"] = JsonDocument.Parse(profileJson).RootElement.Clone()
            }
        };

    private sealed class FakeScenarioClientFactory(FakeMqttSession client) : IMqttScenarioClientFactory
    {
        public List<string> ConnectionNames { get; } = [];

        public IMqttSession CreateClient(string connectionName)
        {
            ConnectionNames.Add(connectionName);
            return client;
        }
    }

    private sealed class FakeMqttSession(MqttConnectionProfile profile) : IMqttSession
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();

        public MqttConnectionProfile Profile { get; } = profile;
        public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttSessionState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCount++;
            State = MqttSessionState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttSessionState.Disconnected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos,
            bool receiveRetainedMessages,
            bool retainAsPublished = true,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default)
            => Task.CompletedTask;

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
            DisposeCount++;
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public sealed record PublishedMessage(
            string Topic,
            byte[] Payload,
            MqttQualityOfServiceLevel QualityOfService,
            bool Retain);
    }
}
