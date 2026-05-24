using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using MQTTnet.Protocol;
using Shouldly;
using System.Threading.Channels;

namespace FluxMq.UI.Tests;

public sealed class FlowWorkspaceServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsCurrentDefinition()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-{Guid.NewGuid():N}.json");
        service.SetFilePath(path);

        try
        {
            var expected = service.DefinitionJson;

            await service.SaveToFileAsync();
            service.SetDefinitionJson("{}");
            await service.LoadFromFileAsync();

            service.DefinitionJson.ShouldBe(expected);
            service.Diagnostics.ShouldNotContain(diagnostic => diagnostic.Severity == "Error");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ValidateAsync_DoesNotChangeDefinitionRevision()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var revision = service.DefinitionRevision;

        await service.ValidateAsync();

        service.DefinitionRevision.ShouldBe(revision);
    }

    [Fact]
    public void SetDefinitionJson_ChangesDefinitionRevisionOnlyWhenContentChanges()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var initialRevision = service.DefinitionRevision;
        var json = service.DefinitionJson;

        service.SetDefinitionJson(json);
        service.DefinitionRevision.ShouldBe(initialRevision);

        service.SetDefinitionJson("{}");
        service.DefinitionRevision.ShouldBe(initialRevision + 1);
    }

    [Fact]
    public void AddComponent_WithRequestedPosition_StagesNodeAtRequestedPosition()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");

        service.AddComponent("flow.mapper", (111d, 222d));

        var staged = service.StagedNodePositions;
        staged.ShouldNotBeNull();
        var position = staged["pipe.mapper"];
        position.X.ShouldBe(111d);
        position.Y.ShouldBe(222d);
        position.Collapsed.ShouldBeFalse();
    }

    [Fact]
    public void AddComponent_WithoutRequestedPosition_StagesNodeNearCanvasStart()
    {
        var composer = new FlowDefinitionComposer();
        var service = new FlowWorkspaceService(composer);
        service.SetDefinitionJson(composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#"));
        service.SetActiveWorkflow(FlowDefinitionComposer.DefaultWorkflowName);
        service.GetDiagramState = () => new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal)
        {
            [$"{FlowDefinitionComposer.DefaultWorkflowName}.trigger"] = (380d, 60d, false),
            [$"{FlowDefinitionComposer.DefaultWorkflowName}.inspect"] = (680d, 60d, false),
            [$"{FlowDefinitionComposer.DefaultWorkflowName}.metrics"] = (980d, 60d, false)
        };

        service.AddComponent("flow.mapper");

        var staged = service.StagedNodePositions;
        staged.ShouldNotBeNull();
        var position = staged[$"{FlowDefinitionComposer.DefaultWorkflowName}.mapper"];
        position.X.ShouldBe(420d);
        position.Y.ShouldBe(290d);
        position.Collapsed.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ConvertsInvalidJsonToDiagnostic()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("{");

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Faulted);
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Severity == "Error");
    }

    [Fact]
    public async Task ValidateAsync_RejectsEmptyDefinition()
    {
        // Runtime requires at least one workflow — empty definition is Faulted
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Faulted);
        service.Diagnostics.ShouldContain(d => d.Severity == "Error");
    }

    [Fact]
    public async Task ValidateAsync_PreservesValidationDiagnosticScope()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
                  "metrics": {
                    "type": "mqtt.metrics",
                    "Input": "missing.Output"
                  }
                }
              }
            }
          }
        }
        """);

        await service.ValidateAsync();

        var diagnostic = service.Diagnostics.Single(d => d.Code == "MissingSourceNode");
        diagnostic.Source.ShouldBe("Definition");
        diagnostic.WorkflowName.ShouldBe("flow");
        diagnostic.NodeName.ShouldBe("metrics");
        diagnostic.PortName.ShouldBe("Input");
    }

    [Fact]
    public async Task ValidateAsync_PreservesRuntimeBuildDiagnosticScope()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
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
        """);

        await service.ValidateAsync();

        var diagnostic = service.Diagnostics.Single(d => d.Code == "FactoryFailed");
        diagnostic.Source.ShouldBe("RuntimeBuild");
        diagnostic.WorkflowName.ShouldBe("flow");
        diagnostic.NodeName.ShouldBe("inspect");
        diagnostic.PortName.ShouldBeNull();
    }

    [Fact]
    public void WorkflowNames_ReflectsCurrentDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var service = new FlowWorkspaceService(composer);
        service.WorkflowNames.ShouldBeEmpty();

        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");

        service.WorkflowNames.ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public void AddWorkflow_SetsActiveWorkflowNameWhenFirstAdded()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        service.AddWorkflow("first");

        service.ActiveWorkflowName.ShouldBe("first");
    }

    [Fact]
    public void SetActiveWorkflow_SwitchesActiveWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");

        service.SetActiveWorkflow("beta");

        service.ActiveWorkflowName.ShouldBe("beta");
    }

    [Fact]
    public void RemoveWorkflow_FallsBackToFirstRemainingWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");
        service.SetActiveWorkflow("alpha");

        service.RemoveWorkflow("alpha");

        service.ActiveWorkflowName.ShouldBe("beta");
    }

    [Fact]
    public async Task ValidateAsync_AcceptsWellFormedDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var profile = new MqttConnectionProfile
        {
            Name = "test-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "test",
            KeepAlive = TimeSpan.FromSeconds(60),
            CleanStart = true
        };
        var service = new FlowWorkspaceService(composer);
        service.SetDefinitionJson(composer.CreateInspectPayloadsDefinition(profile, "#"));

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Valid);
        service.Diagnostics.ShouldContain(d => d.Code == "Ready");
    }

    [Fact]
    public async Task ValidateAsync_AppendsDiagnosticsToLogs()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
                  "metrics": {
                    "type": "mqtt.metrics",
                    "Input": "missing.Output"
                  }
                }
              }
            }
          }
        }
        """);

        await service.ValidateAsync();

        service.Logs.ShouldContain(log =>
            log.Severity == "Error" &&
            log.Source == "Definition" &&
            log.Code == "MissingSourceNode" &&
            log.WorkflowName == "flow" &&
            log.NodeName == "metrics" &&
            log.PortName == "Input");
    }

    [Fact]
    public async Task ClearLogs_RemovesWorkspaceLogHistory()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        await service.ValidateAsync();
        service.Logs.ShouldNotBeEmpty();

        service.ClearLogs();

        service.Logs.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_CollectsFlowLoggerEntries()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
                  "generated": {
                    "type": "generated.source",
                    "configuration": {
                      "messages": [
                        { "topic": "factory/log", "payload": "hello" }
                      ]
                    }
                  },
                  "logger": {
                    "type": "flow.logger",
                    "Input": "generated.Output",
                    "configuration": {
                      "includePayloadPreview": true
                    }
                  }
                }
              }
            }
          }
        }
        """);

        await service.RunAsync();
        await WaitUntilAsync(() => service.Logs.Any(log =>
            log.Source == "MqttEnvelope" &&
            log.NodeName == "logger" &&
            log.Context?.Contains("topic=factory/log", StringComparison.Ordinal) == true));

        service.Logs.ShouldContain(log => log.Code == "Ready");
    }

    [Fact]
    public async Task RunAsync_CollectsComponentErrorsWithoutFlowLogger()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
                  "generated": {
                    "type": "generated.source",
                    "configuration": {
                      "messages": [
                        { "topic": "factory/error", "payload": "hello" }
                      ]
                    }
                  },
                  "filter": {
                    "type": "mqtt.message-filter",
                    "Input": "generated.Output",
                    "configuration": {
                      "expression": "missing.Value > 0"
                    }
                  }
                }
              }
            }
          }
        }
        """);

        await service.RunAsync();
        await WaitUntilAsync(() => service.Logs.Any(log =>
            log.Source == "FlowError" &&
            log.NodeName == "filter" &&
            log.PortName == "Errors" &&
            log.Code == FlowErrorCodes.ProcessingFailed.ToString()));

        service.Logs.Any(log =>
            log.Source == "FlowError" &&
            log.Context is not null &&
            log.Context.Contains("factory/error", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_CollectsPublisherEntriesWithoutFlowLogger()
    {
        var session = new FakeRuntimeMqttSession();
        var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeSessionFactory: _ => session);
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "resources": {
                "local-broker": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": {
                      "name": "local-broker",
                      "host": "localhost",
                      "port": 1883,
                      "clientId": "test-client"
                    }
                  }
                }
              },
              "workflows": {
                "pip1": {
                  "generated": {
                    "type": "generated.source",
                    "configuration": {
                      "messages": [
                        { "topic": "factory/source", "payload": "{\"hello\":\"fluxmq\"}", "qos": 1, "retain": true }
                      ]
                    }
                  },
                  "mapper": {
                    "type": "flow.mapper",
                    "Input": "generated.Output",
                    "configuration": {
                      "engine": "jsonata",
                      "inputType": "MqttEnvelope",
                      "outputType": "MqttPublishRequest",
                      "expression": "{ \"topic\": 'test', \"payload\": payloadText, \"qos\": qos, \"retain\": retain }"
                    }
                  },
                  "publisher": {
                    "type": "mqtt.publisher",
                    "Input": "mapper.Output",
                    "configuration": {
                      "connection": "local-broker"
                    }
                  }
                }
              }
            }
          }
        }
        """);

        await service.RunAsync();
        await WaitUntilAsync(() => service.Logs.Any(log =>
            log.Source == "MqttPublisher" &&
            log.NodeName == "publisher" &&
            log.Context is not null &&
            log.Context.Contains("topic=test", StringComparison.Ordinal)));

        session.Published.ShouldHaveSingleItem().Topic.ShouldBe("test");
        service.Logs.Any(log =>
            log.Source == "MqttPublisher" &&
            log.WorkflowName == "pip1" &&
            log.NodeName == "publisher" &&
            log.Context is not null &&
            log.Context.Contains("qos=1", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_CollectsMetricsSnapshotsFromNodeInputStream()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
                  "generated": {
                    "type": "generated.source",
                    "configuration": {
                      "messages": [
                        { "topic": "factory/qos0", "payload": "low", "qos": 0 },
                        { "topic": "factory/qos1", "payload": "high", "qos": 1 }
                      ]
                    }
                  },
                  "filter": {
                    "type": "mqtt.message-filter",
                    "Input": "generated.Output",
                    "configuration": {
                      "expression": "qos >= 1"
                    }
                  },
                  "metrics": {
                    "type": "mqtt.metrics",
                    "Input": "filter.Output"
                  }
                }
              }
            }
          }
        }
        """);

        await service.RunAsync();
        await WaitUntilAsync(() => service.GetMetricsSnapshot("flow", "metrics").MessageCount == 1);

        var snapshot = service.GetMetricsSnapshot("flow", "metrics");
        snapshot.MessageCount.ShouldBe(1);
        snapshot.LastTopic.ShouldBe("factory/qos1");
        snapshot.TopicCounts.ShouldBe([new FluxMq.Components.MqttMetrics.MqttTopicMetric("factory/qos1", 1)]);
    }

    [Fact]
    public async Task RunAsync_CollectsPayloadInspectionFromNodeInputStream()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "flow": {
                  "generated": {
                    "type": "generated.source",
                    "configuration": {
                      "messages": [
                        { "topic": "factory/qos0", "payload": "low", "qos": 0 },
                        { "topic": "factory/qos1", "payload": "{\"ok\":true}", "qos": 1 }
                      ]
                    }
                  },
                  "filter": {
                    "type": "mqtt.message-filter",
                    "Input": "generated.Output",
                    "configuration": {
                      "expression": "qos >= 1"
                    }
                  },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": "filter.Output"
                  }
                }
              }
            }
          }
        }
        """);

        await service.RunAsync();
        await WaitUntilAsync(() => service.GetPayloadInspection("flow", "inspect") is not null);

        var inspection = service.GetPayloadInspection("flow", "inspect");
        inspection.ShouldNotBeNull();
        inspection.Envelope.Topic.ShouldBe("factory/qos1");
        inspection.Payload.Format.ShouldBe(PayloadFormat.Json);
        service.PayloadInspections.Keys.ShouldContain("flow/inspect");
    }

    [Fact]
    public async Task RunAsync_CollectsTriggerActivityFromTriggerOutputStream()
    {
        var session = new FakeRuntimeMqttSession();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeSessionFactory: _ => session);
        var receivedAt = DateTimeOffset.Parse("2026-05-24T18:30:00Z");
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "resources": {
                "local-broker": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": {
                      "name": "local-broker",
                      "host": "localhost",
                      "port": 1883,
                      "clientId": "test-client"
                    }
                  }
                }
              },
              "workflows": {
                "flow": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "local-broker",
                      "subscriptions": ["factory/#"]
                    }
                  }
                }
              }
            }
          }
        }
        """);

        await service.RunAsync();
        await session.WriteAsync(new MqttEnvelope { Topic = "other/skip", Payload = [1] });
        await session.WriteAsync(new MqttEnvelope { Topic = "factory/one", Payload = [1, 2, 3], ReceivedAt = receivedAt });
        await WaitUntilAsync(() => service.GetTriggerActivitySnapshot("flow", "trigger").MessageCount == 1);

        var snapshot = service.GetTriggerActivitySnapshot("flow", "trigger");
        snapshot.MessageCount.ShouldBe(1);
        snapshot.LastTopic.ShouldBe("factory/one");
        snapshot.LastPayloadBytes.ShouldBe(3);
        snapshot.LastReceivedAt.ShouldBe(receivedAt);
        service.TriggerActivitySnapshots.Keys.ShouldContain("flow/trigger");
    }

    [Fact]
    public void UpdateNodeConfiguration_UpdatesActiveWorkflowWhenNodeNamesRepeat()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pip1": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "broker1",
                      "subscriptions": ["one/#"]
                    }
                  }
                },
                "pip2": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "subscriptions": ["two/#"]
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveWorkflow("pip2");

        service.UpdateNodeConfiguration(
            "trigger",
            new System.Text.Json.Nodes.JsonObject
            {
                ["connection"] = "broker2",
                ["subscriptions"] = new System.Text.Json.Nodes.JsonArray("two/#"),
                ["boundedCapacity"] = 1000
            });

        using var document = System.Text.Json.JsonDocument.Parse(service.DefinitionJson);
        var workflows = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows");

        workflows.GetProperty("pip1")
            .GetProperty("trigger")
            .GetProperty("configuration")
            .GetProperty("connection")
            .GetString()
            .ShouldBe("broker1");

        workflows.GetProperty("pip2")
            .GetProperty("trigger")
            .GetProperty("configuration")
            .GetProperty("connection")
            .GetString()
            .ShouldBe("broker2");
    }

    [Fact]
    public void RemoveWorkflowNode_RemovesNodeFromActiveWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pip1": {
                  "filter": { "type": "mqtt.message-filter" }
                },
                "pip2": {
                  "filter": { "type": "mqtt.message-filter" },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": "filter.Output"
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveWorkflow("pip2");

        service.RemoveWorkflowNode("filter");

        using var document = System.Text.Json.JsonDocument.Parse(service.DefinitionJson);
        var workflows = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows");

        workflows.GetProperty("pip1").TryGetProperty("filter", out _).ShouldBeTrue();
        workflows.GetProperty("pip2").TryGetProperty("filter", out _).ShouldBeFalse();
        workflows.GetProperty("pip2").GetProperty("inspect").TryGetProperty("Input", out _).ShouldBeFalse();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(25);
        }

        predicate().ShouldBeTrue();
    }

    private sealed class FakeRuntimeMqttSession : IMqttSession
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();

        public MqttConnectionProfile Profile { get; } = new() { Name = "test" };
        public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttSessionState>? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            State = MqttSessionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttSessionState.Disconnected;
            _messages.Writer.TryComplete();
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

        public Task WriteAsync(MqttEnvelope message) => _messages.Writer.WriteAsync(message).AsTask();

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
