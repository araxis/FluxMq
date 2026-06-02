using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using FluxMq.App.Definitions;
using FluxMq.App;
using FluxMq.Scenarios;
using Microsoft.Extensions.Configuration;
using MQTTnet.Protocol;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests;

public sealed class FlowApplicationHostTests
{
    [Fact]
    public void CreateDefaultScenarioStepRunnerRegistry_CoversKnownScenarioDefinitions()
    {
        var definitionTypes = ScenarioStepDefinitionCatalog
            .Shared
            .Steps
            .Select(step => step.Type)
            .ToArray();

        var runnerTypes = FlowApplicationHost
            .CreateDefaultScenarioStepRunnerRegistry()
            .Runners
            .Keys
            .ToArray();

        runnerTypes.ShouldBe(definitionTypes, ignoreOrder: true);
    }

    [Fact]
    public void Start_BuildsRuntimeFromConfiguration()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """));

        var result = host.Start();

        result.IsSuccess.ShouldBeTrue();
        host.State.ShouldBe(FlowApplicationHostState.Running);
        host.Runtime.ShouldNotBeNull();
        host.Runtime!.Workflows.ShouldContain(wf => wf.Name.Value == "observe");
    }

    [Fact]
    public void Build_AllowsConditionalLinksFromDefaultHost()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "messages": {
                        "type": "generated.source"
                      },
                      "metrics": {
                        "type": "mqtt.metrics",
                        "Input": {
                          "from": "messages.Output",
                          "when": "input == null || input.Topic.StartsWith(\"factory/\")"
                        }
                      }
                    }
                  }
                }
              }
            }
            """));

        var result = host.Build();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
        result.RuntimeBuild!.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task StopAsync_CompletesBuiltRuntime()
    {
        await using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """));

        host.Start().IsSuccess.ShouldBeTrue();

        await host.StopAsync();

        host.State.ShouldBe(FlowApplicationHostState.Stopped);
        host.Runtime!.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void Start_ReturnsHostErrorWhenConfigurationIsMissing()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration("""{ "FluxMq": {} }"""));

        var result = host.Start();

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(FlowApplicationHostBuildErrorCode.InvalidConfiguration);
        host.State.ShouldBe(FlowApplicationHostState.Empty);
    }

    [Fact]
    public void Start_ReturnsRuntimeBuildErrorsForInvalidComponentConfiguration()
    {
        using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "inspect": {
                        "type": "mqtt.payload-inspector",
                        "configuration": {
                          "boundedCapacity": 0
                        }
                      }
                    }
                  }
                }
              }
            }
            """));

        var result = host.Start();

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
        result.RuntimeBuild!.Errors.ShouldContain(error => error.Message.Contains("boundedCapacity"));
        host.State.ShouldBe(FlowApplicationHostState.Empty);
    }

    [Fact]
    public async Task StopAsync_ConvertsRuntimeCompletionFailureToFaultedState()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.faulting"), (address, _) =>
                RuntimeNode.Create(address, new FaultingNode())));

        await using var host = new FlowApplicationHost(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "workflows": {
                        "observe": {
                          "faulting": {
                            "type": "test.faulting"
                          }
                        }
                      }
                    }
                  }
                }
                """),
            builder);

        host.Start().IsSuccess.ShouldBeTrue();

        await host.StopAsync();

        host.State.ShouldBe(FlowApplicationHostState.Faulted);
        host.LastException.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("completion failed");
    }

    [Fact]
    public async Task StartAsync_ConvertsStartFailureToHostError()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.start-fails"), (address, _) =>
                RuntimeNode.Create(address, new StartFailingNode())));

        await using var host = new FlowApplicationHost(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "workflows": {
                        "observe": {
                          "start": {
                            "type": "test.start-fails"
                          }
                        }
                      }
                    }
                  }
                }
                """),
            builder);

        var result = await host.StartAsync();

        result.IsSuccess.ShouldBeFalse();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowApplicationHostBuildErrorCode.StartFailed);
        error.WorkflowName.ShouldBe("observe");
        error.NodeName.ShouldBe("start");
        host.State.ShouldBe(FlowApplicationHostState.Faulted);
        host.LastException.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("start failed");
    }

    [Fact]
    public async Task RunScenarioAsync_UsesRunningRuntimeAndObservesEvents()
    {
        EventSourceNode? source = null;
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.events"), (address, _) =>
            {
                source = new EventSourceNode();
                return RuntimeNode.Create(address, source);
            }));

        await using var host = new FlowApplicationHost(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "workflows": {
                        "observe": {
                          "events": {
                            "type": "test.events"
                          }
                        }
                      },
                      "tests": {
                        "roundTrip": {
                          "steps": {
                            "expectResponse": {
                              "type": "expect.event",
                              "configuration": {
                                "eventType": "mqtt.message.received",
                                "topicStartsWith": "factory/response/",
                                "status": "received",
                                "timeoutMs": 1000
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
                """),
            builder);

        var startResult = await host.StartAsync();
        startResult.IsSuccess.ShouldBeTrue();
        await WaitUntilAsync(() => source is not null);

        var runTask = host.RunScenarioAsync("roundTrip");
        source!.Post(new FlowEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FluxMqEventTypes.MqttMessageReceived,
            Source = "test",
            Channel = "factory/response/42",
            Status = "received"
        });

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Steps.ShouldHaveSingleItem()
            .MatchedEvent!.Channel.ShouldBe("factory/response/42");
        host.State.ShouldBe(FlowApplicationHostState.Running);
    }

    [Fact]
    public async Task RunScenarioAsync_RequiresExplicitlyRunningRuntime()
    {
        await using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "idle": {
                      "messages": {
                        "type": "generated.source"
                      }
                    }
                  },
                  "tests": {
                    "smoke": {
                      "steps": {}
                    }
                  }
                }
              }
            }
            """));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => host.RunScenarioAsync("smoke"));

        exception.Message.ShouldContain("app runtime is not running");
    }

    [Fact]
    public async Task RunScenarioAsync_CanPublishMqttMessageThroughNamedConnection()
    {
        var mqttClient = new FakeMqttBrokerClient();
        await using var host = FlowApplicationHost.CreateDefault(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "resources": {
                        "localBroker": {
                          "type": "mqtt.connection",
                          "configuration": {
                            "profile": {
                              "name": "local-broker",
                              "host": "localhost",
                              "port": 1883
                            }
                          }
                        }
                      },
                      "workflows": {
                        "idle": {
                          "messages": {
                            "type": "generated.source"
                          }
                        }
                      },
                      "tests": {
                        "publishMessage": {
                          "steps": {
                            "publish": {
                              "type": "mqtt.publisher",
                              "configuration": {
                                "connection": "localBroker",
                                "topic": "topica/b/c",
                                "payload": 12,
                                "qos": 1,
                                "retain": true
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
                """),
            clientFactory: _ => mqttClient);

        var startResult = await host.StartAsync();
        startResult.IsSuccess.ShouldBeTrue();

        var result = await host.RunScenarioAsync("publishMessage");

        result.IsSuccess.ShouldBeTrue();
        var publish = mqttClient.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("topica/b/c");
        Encoding.UTF8.GetString(publish.Payload).ShouldBe("12");
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        publish.Retain.ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_ReportsMissingScenarioMqttConnection()
    {
        var mqttClient = new FakeMqttBrokerClient();
        await using var host = FlowApplicationHost.CreateDefault(
            BuildConfiguration(
                """
                {
                  "FluxMq": {
                    "FlowApplication": {
                      "resources": {
                        "localBroker": {
                          "type": "mqtt.connection",
                          "configuration": {
                            "profile": {
                              "name": "local-broker",
                              "host": "localhost",
                              "port": 1883
                            }
                          }
                        }
                      },
                      "workflows": {
                        "idle": {
                          "messages": {
                            "type": "generated.source"
                          }
                        }
                      },
                      "tests": {
                        "publishMessage": {
                          "steps": {
                            "publish": {
                              "type": "mqtt.publisher",
                              "configuration": {
                                "connection": "missingBroker",
                                "topic": "topica/b/c",
                                "payload": "hello"
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
                """),
            clientFactory: _ => mqttClient);

        var startResult = await host.StartAsync();
        startResult.IsSuccess.ShouldBeFalse();
        var validationError = startResult.DefinitionValidation!.Errors.Single(
            error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.MissingScenarioStepResource);
        validationError.Message.ShouldContain("publishMessage");
        validationError.Message.ShouldContain("publish");
        validationError.Message.ShouldContain("missingBroker");
        mqttClient.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunScenarioAsync_ReportsMissingScenarioName()
    {
        await using var host = FlowApplicationHost.CreateDefault(BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => host.RunScenarioAsync("missing"));

        exception.Message.ShouldBe("Scenario 'missing' does not exist.");
        host.State.ShouldBe(FlowApplicationHostState.Empty);
    }

    private static IConfiguration BuildConfiguration(string json)
        => new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

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

    private sealed class FaultingNode : IFlowNode
    {
        private readonly TaskCompletionSource _completion = new();
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _completion.Task;

        public void Complete()
        {
            _completion.SetException(new InvalidOperationException("completion failed"));
            _errors.Complete();
        }

        public void Fault(Exception exception)
        {
            _completion.SetException(exception);
            _errors.Complete();
        }
    }

    private sealed class StartFailingNode : IFlowNode
    {
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("start failed"));

        public void Complete() => _errors.Complete();

        public void Fault(Exception exception) => _errors.Complete();
    }

    private sealed class EventSourceNode : IFlowNode, IFlowEventSource
    {
        private readonly TaskCompletionSource _completion = new();
        private readonly BufferBlock<FlowError> _errors = new();
        private readonly BufferBlock<FlowEvent> _events = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public ISourceBlock<FlowEvent> Events => _events;
        public Task Completion => _completion.Task;

        public void Post(FlowEvent flowEvent)
            => _events.Post(flowEvent);

        public void Complete()
        {
            _events.Complete();
            _errors.Complete();
            _completion.TrySetResult();
        }

        public void Fault(Exception exception)
        {
            ((IDataflowBlock)_events).Fault(exception);
            ((IDataflowBlock)_errors).Fault(exception);
            _completion.TrySetException(exception);
        }
    }

    private sealed class FakeMqttBrokerClient : IMqttBrokerClient
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();

        public MqttConnectionProfile Profile { get; } = new() { Name = "local-broker" };
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttClientState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
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
