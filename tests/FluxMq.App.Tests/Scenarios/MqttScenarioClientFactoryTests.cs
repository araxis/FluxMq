using FluxMq.App.Scenarios;
using FluxMq.App.Definitions;
using FluxMq.Components.MessageSource;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using FluxMq.Pipeline.Scenarios;
using MQTTnet.Protocol;
using Shouldly;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests.Scenarios;

public sealed class MqttScenarioClientFactoryTests
{
    [Fact]
    public async Task ApplicationDefinitionFactory_CreatesIsolatedClientFromSharedAppResource()
    {
        var capturedProfiles = new List<MqttConnectionProfile>();
        var definition = new FluxMqApplicationDefinition
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
                return new FakeMqttBrokerClient(profile);
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
        scenarioProfile.ClientId.Length.ShouldBe(44);
        scenarioProfile.ClientId.ShouldNotBe("app-client");
    }

    [Fact]
    public async Task RuntimeFactory_CreatesIsolatedClientFromRunningAppResource()
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
            new FakeMqttBrokerClient(appProfile),
            disposeClientOnDispose: false);
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
                return new FakeMqttBrokerClient(profile);
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
    public async Task MqttPublishStep_UsesNormalPublisherComponentAndAppendsScenarioEvents()
    {
        var client = new FakeMqttBrokerClient(new MqttConnectionProfile { Name = "shared-broker" });
        var factory = new FakeScenarioClientFactory(client);
        var events = new BroadcastBlock<FluxFlow.Engine.Components.FlowEvent>(static flowEvent => flowEvent);
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["publish"] = new ScenarioStepDefinition
                {
                    Type = ScenarioStepTypes.MqttPublisher,
                    Configuration =
                    {
                        ["connection"] = JsonSerializer.SerializeToElement("shared-broker"),
                        ["topic"] = JsonSerializer.SerializeToElement("fluxmq/sample/request"),
                        ["payload"] = JsonSerializer.SerializeToElement(new { value = 12 }),
                        ["payloadEncoding"] = JsonSerializer.SerializeToElement("json"),
                        ["qos"] = JsonSerializer.SerializeToElement(1),
                        ["retain"] = JsonSerializer.SerializeToElement(true)
                    }
                },
                ["expectPublished"] = new ScenarioStepDefinition
                {
                    Type = ScenarioStepTypes.ExpectEvent,
                    Configuration =
                    {
                        ["eventType"] = JsonSerializer.SerializeToElement(FluxMqEventTypes.MqttMessagePublished),
                        ["topicStartsWith"] = JsonSerializer.SerializeToElement("fluxmq/sample/request"),
                        ["status"] = JsonSerializer.SerializeToElement("published"),
                        ["payloadContains"] = JsonSerializer.SerializeToElement("\"value\":12"),
                        ["timeoutMs"] = JsonSerializer.SerializeToElement(1000)
                    }
                }
            }
        };
        var services = ScenarioStepServices.Empty
            .Add<IMqttScenarioClientFactory>(factory);
        var runner = new ScenarioRunner(
            new ScenarioStepRunnerRegistry()
                .Register(new MqttPublishScenarioStepRunner())
                .Register(new ExpectEventScenarioStepRunner()));

        var result = await runner.RunAsync("publish", scenario, events, services);

        result.IsSuccess.ShouldBeTrue();
        factory.ConnectionNames.ShouldBe(["shared-broker"]);
        client.ConnectCount.ShouldBe(1);
        client.DisposeCount.ShouldBe(1);
        var published = client.Published.ShouldHaveSingleItem();
        published.Topic.ShouldBe("fluxmq/sample/request");
        JsonDocument.Parse(published.Payload).RootElement.GetProperty("value").GetInt32().ShouldBe(12);
        published.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        published.Retain.ShouldBeTrue();
        result.Steps.Count.ShouldBe(2);
        result.Steps[1].MatchedEvent.ShouldNotBeNull().Channel.ShouldBe("fluxmq/sample/request");
        events.Complete();
    }

    [Fact]
    public async Task MqttTriggerStep_UsesNormalTriggerComponentAndAppendsScenarioEvents()
    {
        var client = new FakeMqttBrokerClient(new MqttConnectionProfile { Name = "shared-broker" });
        var factory = new FakeScenarioClientFactory(client);
        var appEvents = new BroadcastBlock<FluxFlow.Engine.Components.FlowEvent>(static flowEvent => flowEvent);
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["trigger"] = new ScenarioStepDefinition
                {
                    Type = ScenarioStepTypes.MqttTrigger,
                    Configuration =
                    {
                        ["connection"] = JsonSerializer.SerializeToElement("shared-broker"),
                        ["subscriptions"] = JsonSerializer.SerializeToElement("sample/#"),
                        ["qos"] = JsonSerializer.SerializeToElement(1),
                        ["receiveRetained"] = JsonSerializer.SerializeToElement(false),
                        ["retainAsPublished"] = JsonSerializer.SerializeToElement(true)
                    }
                },
                ["expect"] = new ScenarioStepDefinition
                {
                    Type = ScenarioStepTypes.ExpectEvent,
                    Configuration =
                    {
                        ["eventType"] = JsonSerializer.SerializeToElement(FluxMqEventTypes.MqttMessageReceived),
                        ["topicStartsWith"] = JsonSerializer.SerializeToElement("sample/"),
                        ["status"] = JsonSerializer.SerializeToElement("received"),
                        ["payloadContains"] = JsonSerializer.SerializeToElement("hello"),
                        ["timeoutMs"] = JsonSerializer.SerializeToElement(1000)
                    }
                }
            }
        };
        var services = ScenarioStepServices.Empty
            .Add<IMqttScenarioClientFactory>(factory);
        var runner = new ScenarioRunner(
            new ScenarioStepRunnerRegistry()
                .Register(new MqttTriggerScenarioStepRunner())
                .Register(new ExpectEventScenarioStepRunner()));

        var runTask = runner.RunAsync("trigger", scenario, appEvents, services);
        await WaitUntilAsync(() => client.SubscriptionOptions.Count == 1);

        await client.WriteAsync(new MqttEnvelope
        {
            Topic = "sample/response",
            Payload = "hello"u8.ToArray(),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false
        });

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        factory.ConnectionNames.ShouldBe(["shared-broker"]);
        client.ConnectCount.ShouldBe(1);
        client.DisposeCount.ShouldBe(1);
        var subscription = client.SubscriptionOptions.ShouldHaveSingleItem();
        subscription.TopicFilter.ShouldBe("sample/#");
        subscription.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        subscription.ReceiveRetainedMessages.ShouldBeFalse();
        subscription.RetainAsPublished.ShouldBeTrue();
        result.Steps[1].MatchedEvent.ShouldNotBeNull().Channel.ShouldBe("sample/response");
        appEvents.Complete();
    }

    private static NodeDefinition MqttConnectionResource(string profileJson)
        => new()
        {
            Type = FluxMqNodeTypes.Connection,
            Configuration =
            {
                ["profile"] = JsonDocument.Parse(profileJson).RootElement.Clone()
            }
        };

    private sealed class FakeScenarioClientFactory(FakeMqttBrokerClient client) : IMqttScenarioClientFactory
    {
        public List<string> ConnectionNames { get; } = [];

        public IMqttBrokerClient CreateClient(string connectionName)
        {
            ConnectionNames.Add(connectionName);
            return client;
        }
    }

    private sealed class FakeMqttBrokerClient(MqttConnectionProfile profile) : IMqttBrokerClient
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();

        public MqttConnectionProfile Profile { get; } = profile;
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public List<PublishedMessage> Published { get; } = [];
        public List<Subscription> SubscriptionOptions { get; } = [];

        public event EventHandler<MqttClientState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCount++;
            State = MqttClientState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Disconnected;
            StateChanged?.Invoke(this, State);
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
            SubscriptionOptions.Add(new Subscription(
                topicFilter,
                qos,
                receiveRetainedMessages,
                retainAsPublished));
            return Task.CompletedTask;
        }

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

        public ValueTask WriteAsync(MqttEnvelope envelope)
            => _messages.Writer.WriteAsync(envelope);

        public sealed record PublishedMessage(
            string Topic,
            byte[] Payload,
            MqttQualityOfServiceLevel QualityOfService,
            bool Retain);

        public sealed record Subscription(
            string TopicFilter,
            MqttQualityOfServiceLevel QualityOfService,
            bool ReceiveRetainedMessages,
            bool RetainAsPublished);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not reached.");
            }

            await Task.Delay(10);
        }
    }
}
