using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Mqtt;
using FluxFlow.Components.Control;
using FluxFlow.Engine.Components;
using FluxMq.Scenarios;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using MQTTnet.Protocol;
using Shouldly;
using System.Text;
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
    public void GetAssertionNames_ReturnsConfiguredWorkflowAssertions()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "primary": {
                  "assertQoS": {
                    "type": "flow.assert",
                    "configuration": {
                      "assertionName": "QoS at least once"
                    }
                  },
                  "assertPayload": {
                    "type": "flow.assert",
                    "configuration": {
                      "assertionName": "Payload has id"
                    }
                  },
                  "logger": {
                    "type": "log.console"
                  }
                },
                "secondary": {
                  "duplicate": {
                    "type": "flow.assert",
                    "configuration": {
                      "assertionName": "Payload has id"
                    }
                  }
                }
              }
            }
          }
        }
        """);

        service.GetAssertionNames().ShouldBe(["Payload has id", "QoS at least once"]);
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
    public async Task SaveToFileAsync_WritesLastNodePositionsWhenDesignerIsUnmounted()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-{Guid.NewGuid():N}.json");
        service.SetFilePath(path);
        service.AddWorkflow("pipe");
        service.AddComponent("flow.mapper");
        service.GetDiagramState = null;
        service.LastNodePositions["pipe.mapper"] = (444d, 222d, true);

        try
        {
            await service.SaveToFileAsync();

            using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var node = document.RootElement
                .GetProperty("FluxMq")
                .GetProperty("Designer")
                .GetProperty("nodes")
                .GetProperty("pipe.mapper");

            node.GetProperty("x").GetDouble().ShouldBe(444d);
            node.GetProperty("y").GetDouble().ShouldBe(222d);
            node.GetProperty("collapsed").GetBoolean().ShouldBeTrue();
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
    public void GetFullDefinitionJson_MergesLiveAndLastNodePositions()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pip1");
        service.AddWorkflow("pip2");
        service.LastNodePositions["pip1.trigger"] = (110d, 120d, false);
        service.GetDiagramState = () => new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal)
        {
            ["pip2.trigger"] = (210d, 220d, true)
        };

        var json = service.GetFullDefinitionJson();

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var nodes = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("Designer")
            .GetProperty("nodes");

        nodes.GetProperty("pip1.trigger").GetProperty("x").GetDouble().ShouldBe(110d);
        nodes.GetProperty("pip2.trigger").GetProperty("x").GetDouble().ShouldBe(210d);
        nodes.GetProperty("pip2.trigger").GetProperty("collapsed").GetBoolean().ShouldBeTrue();
        service.LastNodePositions.Keys.ShouldContain("pip1.trigger");
        service.LastNodePositions.Keys.ShouldContain("pip2.trigger");
    }

    [Fact]
    public async Task LoadAndSave_PreservesPositionsForInactivePipelines()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-{Guid.NewGuid():N}.json");
        service.SetFilePath(path);
        await File.WriteAllTextAsync(path, """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pip1": {
                  "trigger": { "type": "mqtt.trigger" }
                },
                "pip2": {
                  "trigger": { "type": "mqtt.trigger" }
                }
              }
            },
            "Designer": {
              "nodes": {
                "pip1.trigger": { "x": 110, "y": 120, "collapsed": false },
                "pip2.trigger": { "x": 210, "y": 220, "collapsed": false }
              }
            }
          }
        }
        """);

        try
        {
            await service.LoadFromFileAsync();
            service.SetActiveWorkflow("pip2");
            service.GetDiagramState = () => new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal)
            {
                ["pip2.trigger"] = (310d, 320d, true)
            };

            await service.SaveToFileAsync();

            using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var nodes = document.RootElement
                .GetProperty("FluxMq")
                .GetProperty("Designer")
                .GetProperty("nodes");

            nodes.GetProperty("pip1.trigger").GetProperty("x").GetDouble().ShouldBe(110d);
            nodes.GetProperty("pip1.trigger").GetProperty("y").GetDouble().ShouldBe(120d);
            nodes.GetProperty("pip2.trigger").GetProperty("x").GetDouble().ShouldBe(310d);
            nodes.GetProperty("pip2.trigger").GetProperty("y").GetDouble().ShouldBe(320d);
            nodes.GetProperty("pip2.trigger").GetProperty("collapsed").GetBoolean().ShouldBeTrue();
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
        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Pipeline);
        service.ActiveArtifactName.ShouldBe("beta");
    }

    [Fact]
    public void SetActiveLogs_SwitchesActiveArtifactToLogsPage()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");

        service.SetActiveLogs();

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Logs);
        service.ActiveArtifactName.ShouldBe("Logs");
    }

    [Fact]
    public void SetActiveTopics_SwitchesActiveArtifactToTopicsPage()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");
        service.SetActiveDashboardLive(true);

        service.SetActiveTopics();

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Topics);
        service.ActiveArtifactName.ShouldBe("Topics");
        service.IsActiveDashboardLive.ShouldBeFalse();
    }

    [Fact]
    public void SetDefinitionJson_TracksWorkspaceArtifactNames()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {}
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"]
                  }
                }
              },
              "tests": {
                "roundTrip": {
                  "steps": {}
                }
              }
            }
          }
        }
        """);

        service.WorkflowNames.ShouldBe(["pipe"]);
        service.DashboardNames.ShouldBe(["ops"]);
        service.TestNames.ShouldBe(["roundTrip"]);
        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Pipeline);
        service.ActiveArtifactName.ShouldBe("pipe");
    }

    [Fact]
    public void DashboardAndTestSelection_DoesNotChangeActiveWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.AddTest("roundTrip");

        service.SetActiveDashboard("ops");

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Dashboard);
        service.ActiveArtifactName.ShouldBe("ops");
        service.ActiveWorkflowName.ShouldBe("pipe");

        service.SetActiveTest("roundTrip");

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Test);
        service.ActiveArtifactName.ShouldBe("roundTrip");
        service.ActiveWorkflowName.ShouldBe("pipe");
    }

    [Fact]
    public void SetActiveDashboardLive_OnlyEnablesLiveModeForActiveDashboard()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");

        service.SetActiveWorkflow("pipe");
        service.SetActiveDashboardLive(true);

        service.IsActiveDashboardLive.ShouldBeFalse();

        service.SetActiveDashboard("ops");
        service.SetActiveDashboardLive(true);

        service.IsActiveDashboardLive.ShouldBeTrue();
    }

    [Fact]
    public void ActiveDashboardLiveMode_ClearsWhenLeavingDashboard()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.AddTest("roundTrip");

        service.SetActiveDashboard("ops");
        service.SetActiveDashboardLive(true);

        service.SetActiveTest("roundTrip");

        service.IsActiveDashboardLive.ShouldBeFalse();

        service.SetActiveDashboard("ops");
        service.SetActiveDashboardLive(true);

        service.SetActiveWorkflow("pipe");

        service.IsActiveDashboardLive.ShouldBeFalse();
    }

    [Fact]
    public void SetActiveDashboard_ChangingDashboardClearsLiveMode()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.AddDashboard("debug");

        service.SetActiveDashboard("ops");
        service.SetActiveDashboardLive(true);

        service.SetActiveDashboard("debug");

        service.IsActiveDashboardLive.ShouldBeFalse();
        service.ActiveDashboardName.ShouldBe("debug");
    }

    [Fact]
    public void GetActiveTestScenario_ReflectsLoadedSteps()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "tests": {
                "t1": {
                  "steps": {
                    "publishSampleRequest": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "fluxmq/sample/request"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);

        service.SetActiveTest("t1");

        var scenario = service.GetActiveTestScenario().ShouldNotBeNull();
        scenario.StepCount.ShouldBe(1);
        scenario.Steps[0].Name.ShouldBe("publishSampleRequest");
        scenario.Steps[0].ReadString("topic").ShouldBe("fluxmq/sample/request");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_RunsSelectedScenarioAndStoresResult()
    {
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "observe": {
                  "source": {
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
        """);
        service.SetActiveTest("smoke");

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.Name.ShouldBe("smoke");
        result.IsSuccess.ShouldBeTrue();
        service.LastScenarioRunResult.ShouldBe(result);
        service.ScenarioRunHistory.ShouldBe([result]);
        service.IsScenarioRunning.ShouldBeFalse();
        service.Logs.ShouldContain(log =>
            log.Source == "Scenario" &&
            log.Code == "Passed" &&
            log.Scope == WorkspaceLogScopes.TestRunner &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Test &&
            log.ArtifactName == "smoke");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_StoresFailedScenarioResult()
    {
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "observe": {
                  "source": {
                    "type": "generated.source"
                  }
                }
              },
              "tests": {
                "broken": {
                  "steps": {
                    "unknown": {
                      "type": "unknown.step"
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("broken");

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeFalse();
        result.Steps.ShouldHaveSingleItem().Message.ShouldBe("Scenario step type 'unknown.step' is not registered.");
        service.LastScenarioRunResult.ShouldBe(result);
        service.ScenarioRunHistory.ShouldBe([result]);
        service.Logs.ShouldContain(log =>
            log.Source == "Scenario" &&
            log.Code == "Failed" &&
            log.Scope == WorkspaceLogScopes.TestRunner &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Test &&
            log.ArtifactName == "broken");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_PublishesWithoutStartingRuntime()
    {
        var mqttClient = new FakeRuntimeMqttBrokerClient();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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
              "tests": {
                "publishOnly": {
                  "steps": {
                    "publish": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "fluxmq/sample/request",
                        "payload": { "value": 12 },
                        "payloadEncoding": "json",
                        "qos": 1,
                        "retain": false
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("publishOnly");

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue();
        service.State.ShouldNotBe(RuntimeWorkspaceState.Running);
        service.RuntimeEvents.ShouldBeEmpty();
        var publish = mqttClient.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("fluxmq/sample/request");
        Encoding.UTF8.GetString(publish.Payload).ShouldBe("""{"value":12}""");
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        publish.Retain.ShouldBeFalse();
        service.Logs.ShouldContain(log =>
            log.Source == "MqttPublisher" &&
            log.Code == FluxMqEventTypes.MqttMessagePublished &&
            log.Scope == WorkspaceLogScopes.TestRunner &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Test &&
            log.ArtifactName == "publishOnly" &&
            log.Context != null &&
            log.Context.Contains("topic=fluxmq/sample/request", StringComparison.Ordinal) &&
            log.Context.Contains("status=published", StringComparison.Ordinal) &&
            log.Context.Contains("qos=1", StringComparison.Ordinal) &&
            log.Context.Contains("retain=False", StringComparison.Ordinal) &&
            log.Context.Contains("""payload={"value":12}""", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_AllowsWhenEventAfterRunnerOwnedPublisherWithoutStartingRuntime()
    {
        var mqttClient = new FakeRuntimeMqttBrokerClient();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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
              "tests": {
                "conditionalPublish": {
                  "steps": {
                    "publish": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "test",
                        "payload": { "hello": "fluxmq" },
                        "payloadEncoding": "json",
                        "qos": 1,
                        "retain": false
                      }
                    },
                    "when": {
                      "type": "when.event",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test",
                        "status": "published",
                        "payloadContains": "\"hello\":\"fluxmq\"",
                        "timeoutMs": 1000
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("conditionalPublish");

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue(CreateScenarioFailureDetails(result, service.Logs));
        result.Steps.Count.ShouldBe(2);
        result.Steps[1].Status.ShouldBe(ScenarioStepRunStatus.Passed);
        result.Steps[1].MatchedEvent.ShouldNotBeNull().Channel.ShouldBe("test");
        service.State.ShouldNotBe(RuntimeWorkspaceState.Running);
        service.RuntimeEvents.ShouldBeEmpty();
        mqttClient.Published.ShouldHaveSingleItem().Topic.ShouldBe("test");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_SkipsRemainingStepsWhenWhenEventDoesNotMatch()
    {
        var mqttClient = new FakeRuntimeMqttBrokerClient();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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
              "tests": {
                "conditionalPublish": {
                  "steps": {
                    "publish": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "test",
                        "payload": { "hello": "fluxmq" },
                        "payloadEncoding": "json"
                      }
                    },
                    "when": {
                      "type": "when.event",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "other",
                        "status": "published",
                        "timeoutMs": 1
                      }
                    },
                    "afterSkip": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "after/skip",
                        "payload": "should-not-publish"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("conditionalPublish");

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue(CreateScenarioFailureDetails(result, service.Logs));
        result.Steps.Count.ShouldBe(2);
        result.Steps[1].Name.ShouldBe("when");
        result.Steps[1].Status.ShouldBe(ScenarioStepRunStatus.Skipped);
        service.LastScenarioRunResult.ShouldBe(result);
        service.ScenarioRunHistory.ShouldBe([result]);
        mqttClient.Published.ShouldHaveSingleItem().Topic.ShouldBe("test");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_FailsFastWhenEventObserverHasNoEventSource()
    {
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "tests": {
                "conditional": {
                  "steps": {
                    "when": {
                      "type": "when.event",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test",
                        "timeoutMs": 10000
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("conditional");

        var result = await service.RunActiveTestScenarioAsync().WaitAsync(TimeSpan.FromSeconds(1));

        result.ShouldBeNull();
        service.State.ShouldBe(RuntimeWorkspaceState.Idle);
        service.LastScenarioRunResult.ShouldBeNull();
        var diagnostic = service.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Code.ShouldBe("RunFailed");
        diagnostic.Message.ShouldContain("contains event-observing steps before any scenario event source");
        diagnostic.Message.ShouldContain("start the app runtime");
        service.Logs.ShouldContain(log =>
            log.Scope == WorkspaceLogScopes.TestRunner &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Test &&
            log.ArtifactName == "conditional" &&
            log.Code == "RunFailed");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_UsesRunningRuntimeConnectionProfileForScenarioClients()
    {
        var factoryProfiles = new List<MqttConnectionProfile>();
        await using var service = new FlowWorkspaceService(
            new FlowDefinitionComposer(),
            runtimeClientFactory: profile =>
            {
                factoryProfiles.Add(profile);
                var clientProfile = profile.Host == "definition-broker.local"
                    ? profile with { Host = "running-runtime.local" }
                    : profile;
                return new FakeRuntimeMqttBrokerClient(profile: clientProfile);
            });
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
                      "host": "definition-broker.local",
                      "port": 1883,
                      "clientId": "runtime-client"
                    }
                  }
                }
              },
              "workflows": {
                "observe": {
                  "source": {
                    "type": "generated.source"
                  }
                }
              },
              "tests": {
                "publishOnly": {
                  "steps": {
                    "publish": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "test",
                        "payload": { "hello": "fluxmq" },
                        "payloadEncoding": "json"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("publishOnly");
        await service.RunAsync();
        service.State.ShouldBe(RuntimeWorkspaceState.Running);
        factoryProfiles.Clear();

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue();
        var scenarioProfile = factoryProfiles.ShouldHaveSingleItem();
        scenarioProfile.Host.ShouldBe("running-runtime.local");
        scenarioProfile.ClientId.ShouldStartWith("fluxmq-test-");
        scenarioProfile.CleanStart.ShouldBeTrue();
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_MirrorsRuntimeEventsToLogs()
    {
        var mqttClient = new FakeRuntimeMqttBrokerClient(echoPublishes: true);
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "local-broker",
                      "subscriptions": ["fluxmq/#"]
                    }
                  }
                }
              },
              "tests": {
                "t1": {
                  "steps": {
                    "aPublishSampleRequest": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "fluxmq/sample/request",
                        "payload": { "value": 12 },
                        "payloadEncoding": "json",
                        "qos": 1,
                        "retain": false
                      }
                    },
                    "bExpectTriggerReceive": {
                      "type": "expect.event",
                      "configuration": {
                        "eventType": "mqtt.message.received",
                        "topicStartsWith": "fluxmq/sample",
                        "status": "received",
                        "payloadContains": "\"value\":12",
                        "timeoutMs": 1000
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("t1");
        await service.RunAsync();

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue(string.Join(
            Environment.NewLine,
            result.Steps.Select(step => $"{step.Name}: {step.Status} - {step.Message}")));
        await WaitUntilAsync(() => service.Logs.Any(log =>
            log.Source == "MqttTrigger" &&
            log.Code == FluxMqEventTypes.MqttMessageReceived &&
            log.Scope == WorkspaceLogScopes.App &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Pipeline &&
            log.ArtifactName == "pip1" &&
            log.WorkflowName == "pip1" &&
            log.NodeName == "trigger" &&
            log.Context is not null &&
            log.Context.Contains("topic=fluxmq/sample/request", StringComparison.Ordinal) &&
            log.Context.Contains("status=received", StringComparison.Ordinal) &&
            log.Context.Contains("payload={\"value\":12}", StringComparison.Ordinal)));
        service.RuntimeEvents.ShouldContain(flowEvent =>
            flowEvent.Type == FluxMqEventTypes.MqttMessageReceived &&
            flowEvent.Channel == "fluxmq/sample/request");
    }

    [Fact]
    public async Task RunActiveTestScenarioAsync_CanObserveMappedPublisherEventFromAppFlow()
    {
        var broker = new SharedFakeBroker();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: broker.CreateClient);
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
                      "clientId": "test-client-a"
                    }
                  }
                },
                "local-broker2": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": {
                      "name": "local-broker2",
                      "host": "localhost",
                      "port": 1883,
                      "clientId": "test-client-b"
                    }
                  }
                }
              },
              "workflows": {
                "pip1": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "local-broker",
                      "subscriptions": [
                        {
                          "topicFilter": "fluxmq/#",
                          "qos": 1,
                          "receiveRetained": false,
                          "retainAsPublished": true
                        }
                      ]
                    }
                  },
                  "metrics": {
                    "type": "mqtt.metrics",
                    "Input": "trigger.Output"
                  },
                  "jsonSchemaValidator": {
                    "type": "json.schema-validator",
                    "Input": "trigger.Output",
                    "configuration": {
                      "schemaId": "payload-object",
                      "schema": "{ \"type\": \"object\" }"
                    }
                  },
                  "router": {
                    "type": "flow.when",
                    "Input": "trigger.Output",
                    "configuration": {
                      "expression": "topic.StartsWith(\"flux\")"
                    }
                  },
                  "mapper": {
                    "type": "flow.mapper",
                    "Input": "router.WhenTrue",
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
                },
                "pip2": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "local-broker2",
                      "subscriptions": ["#"]
                    }
                  }
                }
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "published": {
                        "row": 0,
                        "column": 0,
                        "widget": "published"
                      }
                    }
                  },
                  "widgets": {
                    "published": {
                      "type": "event.counter",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test",
                        "status": "published",
                        "attribute:qos": "1",
                        "attribute:retain": "false"
                      }
                    }
                  }
                }
              },
              "tests": {
                "t1": {
                  "steps": {
                    "publishSampleRequest": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "fluxmq/sample/request",
                        "payload": { "value": 12, "unit": "c", "status": "ok" },
                        "payloadEncoding": "json",
                        "qos": 1,
                        "retain": false
                      }
                    },
                    "expectTriggerReceive": {
                      "type": "expect.event",
                      "configuration": {
                        "eventType": "mqtt.message.received",
                        "topicStartsWith": "fluxmq/sample",
                        "status": "received",
                        "payloadContains": "\"value\":12",
                        "timeoutMs": 1000,
                        "attributes": {
                          "qos": "1",
                          "retain": "false"
                        }
                      }
                    },
                    "expectMappedPublish": {
                      "type": "expect.event",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test",
                        "status": "published",
                        "payloadContains": "\"value\":12",
                        "timeoutMs": 1000,
                        "attributes": {
                          "qos": "1",
                          "retain": "false"
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");
        service.SetActiveTest("t1");
        await service.RunAsync();

        var result = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        result.IsSuccess.ShouldBeTrue(CreateScenarioFailureDetails(result, service.Logs));
        await WaitUntilAsync(() => service.RuntimeEvents.Any(flowEvent =>
            flowEvent.Type == FluxMqEventTypes.MqttMessagePublished &&
            flowEvent.Channel == "test" &&
            flowEvent.PayloadPreview == """{"value":12,"unit":"c","status":"ok"}"""));
        service.RuntimeEvents.ShouldContain(flowEvent =>
            flowEvent.Type == FluxMqEventTypes.MqttMessagePublished &&
            flowEvent.Channel == "test" &&
            flowEvent.PayloadPreview == """{"value":12,"unit":"c","status":"ok"}""");
        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["published"];
        await WaitUntilAsync(() => service.GetDashboardEventSnapshot(widget).Count == 1);
        var snapshot = service.GetDashboardEventSnapshot(widget);
        snapshot.LatestEvent.ShouldNotBeNull();
        snapshot.LatestEvent.Channel.ShouldBe("test");
        service.Logs.ShouldContain(log =>
            log.Source == "MqttPublisher" &&
            log.WorkflowName == "pip1" &&
            log.NodeName == "publisher" &&
            log.Context != null &&
            log.Context.Contains("retain=False", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetActiveTest_RestoresLatestScenarioResultForSelectedTest()
    {
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "observe": {
                  "source": {
                    "type": "generated.source"
                  }
                }
              },
              "tests": {
                "one": {
                  "steps": {}
                },
                "two": {
                  "steps": {}
                }
              }
            }
          }
        }
        """);

        service.SetActiveTest("one");
        var first = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        service.SetActiveTest("two");
        service.LastScenarioRunResult.ShouldBeNull();
        var second = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();

        service.ScenarioRunHistory.ShouldBe([second, first]);

        service.SetActiveTest("one");
        service.LastScenarioRunResult.ShouldBe(first);

        service.SetActiveTest("two");
        service.LastScenarioRunResult.ShouldBe(second);
    }

    [Fact]
    public async Task SelectScenarioRunHistoryResult_RestoresSelectedRunForActiveTestOnly()
    {
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "observe": {
                  "source": {
                    "type": "generated.source"
                  }
                }
              },
              "tests": {
                "one": {
                  "steps": {}
                },
                "two": {
                  "steps": {}
                }
              }
            }
          }
        }
        """);

        service.SetActiveTest("one");
        var first = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();
        var second = (await service.RunActiveTestScenarioAsync()).ShouldNotBeNull();
        service.LastScenarioRunResult.ShouldBe(second);

        service.SelectScenarioRunHistoryResult(first).ShouldBeTrue();
        service.LastScenarioRunResult.ShouldBe(first);

        service.SetActiveTest("two");
        service.SelectScenarioRunHistoryResult(first).ShouldBeFalse();
        service.LastScenarioRunResult.ShouldBeNull();
    }

    [Fact]
    public async Task SaveScenarioReportAsync_WritesScenarioReportJson()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-report-{Guid.NewGuid():N}.json");
        var started = DateTimeOffset.Parse("2026-05-26T10:00:00Z");
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "tests": {
                "smoke": {
                  "steps": {
                    "publishMessage": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "topic": "factory/request",
                        "qos": "1"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveTest("smoke");
        var result = new ScenarioRunResult
        {
            Name = "smoke",
            Status = ScenarioRunStatus.Passed,
            StartedAt = started,
            FinishedAt = started.AddMilliseconds(50),
            Steps =
            [
                new ScenarioStepResult
                {
                    Name = "publishMessage",
                    Type = ScenarioStepTypes.MqttPublisher,
                    Status = ScenarioStepRunStatus.Passed,
                    StartedAt = started,
                    FinishedAt = started.AddMilliseconds(50),
                    Message = "Published message."
                }
            ]
        };

        try
        {
            await service.SaveScenarioReportAsync(result, path);

            using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var root = document.RootElement;
            root.GetProperty("name").GetString().ShouldBe("smoke");
            root.GetProperty("runId").GetString().ShouldBe("smoke-20260526T100000000Z");
            root.GetProperty("status").GetString().ShouldBe("Passed");
            var step = root.GetProperty("steps")[0];
            step.GetProperty("name").GetString().ShouldBe("publishMessage");
            var configuration = step.GetProperty("configuration");
            configuration.GetProperty("topic").GetString().ShouldBe("factory/request");
            configuration.GetProperty("qos").GetString().ShouldBe("1");
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
    public async Task SaveScenarioReportJsonAsync_WritesProvidedJsonWithoutRegenerating()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-report-{Guid.NewGuid():N}.json");
        const string json = """
        {
          "schemaVersion": "scenario-run-report.v1",
          "runId": "existing-run",
          "generatedAt": "2026-05-26T10:00:00+00:00"
        }
        """;

        try
        {
            await FlowWorkspaceService.SaveScenarioReportJsonAsync(json, path);

            var written = await File.ReadAllTextAsync(path);
            written.ShouldBe(json);
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
    public async Task SaveScenarioReportTextAsync_WritesProvidedTextReport()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-report-{Guid.NewGuid():N}.txt");
        const string text = """
        Report scenario-run-report.v1 run existing-run generated 2026-05-26T10:00:00.0000000+00:00.
        Scenario 'smoke' Passed. Steps: 1 total, 1 passed. Duration: 50 ms.
        """;

        try
        {
            await FlowWorkspaceService.SaveScenarioReportTextAsync(text, path);

            var written = await File.ReadAllTextAsync(path);
            written.ShouldBe(text);
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
    public void ActiveDashboardLayoutCommands_UpdateDefinitionAndDiagnostics()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.SetActiveDashboard("ops");

        service.UpdateDashboardGridTracks(["320", "*"], ["160"]);
        service.UpdateDashboardTrack("column", 1, "2*", 9);
        service.AddDashboardCell();

        var layout = service.GetActiveDashboardLayout().ShouldNotBeNull();
        layout.Columns.ShouldBe(["320", "2*"]);
        layout.Rows.ShouldBe(["160"]);
        layout.ColumnPadding.ShouldBe([0d, 9d]);
        layout.Cells.ShouldHaveSingleItem().Name.ShouldBe("cell");
        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Dashboard);
        service.Diagnostics.ShouldBeEmpty();
        service.HasUnsavedChanges.ShouldBeTrue();

        service.UpdateDashboardGridTracks(["150%"], ["*"]);

        service.State.ShouldBe(RuntimeWorkspaceState.Faulted);
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "DashboardUpdateFailed");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsDashboardAndTestArtifacts()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-{Guid.NewGuid():N}.json");
        service.SetFilePath(path);
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.AddTest("roundTrip");

        try
        {
            await service.SaveToFileAsync();

            var loaded = new FlowWorkspaceService(new FlowDefinitionComposer());
            loaded.SetFilePath(path);
            await loaded.LoadFromFileAsync();

            loaded.WorkflowNames.ShouldBe(["pipe"]);
            loaded.DashboardNames.ShouldBe(["ops"]);
            loaded.TestNames.ShouldBe(["roundTrip"]);
            loaded.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Pipeline);
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
    public void RemoveWorkflow_ClearsStoredPositionsForDeletedWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");
        service.LastNodePositions["alpha.trigger"] = (100d, 120d, false);
        service.LastNodePositions["beta.trigger"] = (240d, 120d, false);

        service.RemoveWorkflow("alpha");

        service.LastNodePositions.Keys.ShouldNotContain("alpha.trigger");
        service.LastNodePositions.Keys.ShouldContain("beta.trigger");
    }

    [Fact]
    public void RemoveDashboard_FallsBackToFirstRemainingDashboard()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.AddDashboard("debug");
        service.SetActiveDashboard("ops");

        service.RemoveDashboard("ops");

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Dashboard);
        service.ActiveDashboardName.ShouldBe("debug");
    }

    [Fact]
    public void RemoveDashboard_FallsBackToPipelineWhenLastDashboardIsRemoved()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddDashboard("ops");
        service.SetActiveDashboard("ops");

        service.RemoveDashboard("ops");

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Pipeline);
        service.ActiveWorkflowName.ShouldBe("pipe");
        service.ActiveDashboardName.ShouldBeNull();
    }

    [Fact]
    public void RemoveTest_FallsBackToFirstRemainingTest()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddTest("roundTrip");
        service.AddTest("timeout");
        service.SetActiveTest("roundTrip");

        service.RemoveTest("roundTrip");

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Test);
        service.ActiveTestName.ShouldBe("timeout");
    }

    [Fact]
    public void RemoveTest_FallsBackToPipelineWhenLastTestIsRemoved()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("pipe");
        service.AddTest("roundTrip");
        service.SetActiveTest("roundTrip");

        service.RemoveTest("roundTrip");

        service.ActiveArtifactKind.ShouldBe(WorkspaceArtifactKind.Pipeline);
        service.ActiveWorkflowName.ShouldBe("pipe");
        service.ActiveTestName.ShouldBeNull();
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
            log.Scope == WorkspaceLogScopes.System &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Pipeline &&
            log.ArtifactName == "flow" &&
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
            log.Scope == WorkspaceLogScopes.App &&
            log.ArtifactKind == WorkspaceLogArtifactKinds.Pipeline &&
            log.ArtifactName == "flow" &&
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
                    "type": "flow.filter",
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
            log.Code == ControlErrorCodes.FilterExpressionFailed.ToString()));

        service.Logs.Any(log =>
            log.Source == "FlowError" &&
            log.Context is not null &&
            log.Context.Contains("inputType=MqttEnvelope", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_CollectsPublisherEntriesWithoutFlowLogger()
    {
        var mqttClient = new FakeRuntimeMqttBrokerClient();
        var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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

        mqttClient.Published.ShouldHaveSingleItem().Topic.ShouldBe("test");
        service.Logs.Any(log =>
            log.Source == "MqttPublisher" &&
            log.WorkflowName == "pip1" &&
            log.NodeName == "publisher" &&
            log.Context is not null &&
            log.Context.Contains("qos=1", StringComparison.Ordinal)).ShouldBeTrue();
        await WaitUntilAsync(() => service.Logs.Any(log =>
            log.Source == "MqttPublisher" &&
            log.Code == FluxMqEventTypes.MqttMessagePublished &&
            log.WorkflowName == "pip1" &&
            log.NodeName == "publisher" &&
            log.Context is not null &&
            log.Context.Contains("status=published", StringComparison.Ordinal) &&
            log.Context.Contains("payload={\"hello\":\"fluxmq\"}", StringComparison.Ordinal)));
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
                    "type": "flow.filter",
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
                    "type": "flow.filter",
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
        var mqttClient = new FakeRuntimeMqttBrokerClient();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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
        await mqttClient.WriteAsync(new MqttEnvelope { Topic = "other/skip", Payload = [1] });
        await mqttClient.WriteAsync(new MqttEnvelope { Topic = "factory/one", Payload = [1, 2, 3], ReceivedAt = receivedAt });
        await WaitUntilAsync(() => service.GetTriggerActivitySnapshot("flow", "trigger").MessageCount == 1);

        var snapshot = service.GetTriggerActivitySnapshot("flow", "trigger");
        snapshot.MessageCount.ShouldBe(1);
        snapshot.LastTopic.ShouldBe("factory/one");
        snapshot.LastPayloadBytes.ShouldBe(3);
        snapshot.LastReceivedAt.ShouldBe(receivedAt);
        service.TriggerActivitySnapshots.Keys.ShouldContain("flow/trigger");
    }

    [Fact]
    public void UpdateDashboardWidget_UpdatesActiveDashboardWidgetConfiguration()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddDashboard("ops");
        service.SetActiveDashboard("ops");
        service.AddDashboardWidget("event.counter", "slot:0:0");

        service.UpdateDashboardWidget(
            "eventCounter",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Received from factory",
                ["eventType"] = FluxMqEventTypes.MqttMessageReceived,
                ["topicStartsWith"] = "factory/",
                ["subjectStartsWith"] = "factory/",
                ["status"] = "received"
            });

        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["eventCounter"];
        widget.Configuration["title"].ShouldBe("Received from factory");
        widget.Configuration["eventType"].ShouldBe(FluxMqEventTypes.MqttMessageReceived);
        widget.Configuration["topicStartsWith"].ShouldBe("factory/");
        widget.Configuration["subjectStartsWith"].ShouldBe("factory/");
        widget.Configuration["status"].ShouldBe("received");
    }

    [Fact]
    public void RemoveDashboardWidget_ClearsAssignedCell()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddDashboard("ops");
        service.SetActiveDashboard("ops");
        service.AddDashboardWidget("event.counter", "slot:0:0");

        service.RemoveDashboardWidget("eventCounter");

        var layout = service.GetActiveDashboardLayout().ShouldNotBeNull();
        layout.Widgets.ContainsKey("eventCounter").ShouldBeFalse();
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 0 && string.IsNullOrWhiteSpace(cell.Widget));
        service.HasUnsavedChanges.ShouldBeTrue();
    }

    [Fact]
    public void TestScenarioStepCommands_UpdateActiveScenario()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddTest("t1");
        service.SetActiveTest("t1");

        service.AddTestScenarioStep("expect.event");

        var scenario = service.GetActiveTestScenario().ShouldNotBeNull();
        var step = scenario.Steps.ShouldHaveSingleItem();
        step.Name.ShouldBe("expectEvent");

        service.UpdateTestScenarioStep(
            step.Name,
            step.Type,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventType"] = FluxMqEventTypes.MqttMessageReceived,
                ["topicStartsWith"] = "factory/",
                ["subjectStartsWith"] = "",
                ["status"] = "received",
                ["payloadContains"] = "ok",
                ["timeoutMs"] = "2500"
            });

        scenario = service.GetActiveTestScenario().ShouldNotBeNull();
        scenario.Steps[0].Configuration["topicStartsWith"].ShouldBe("factory/");
        scenario.Steps[0].Configuration["timeoutMs"].ShouldBe("2500");

        service.RemoveTestScenarioStep(step.Name);

        service.GetActiveTestScenario().ShouldNotBeNull().Steps.ShouldBeEmpty();
    }

    [Fact]
    public void MoveTestScenarioStep_ReordersActiveScenario()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddTest("t1");
        service.SetActiveTest("t1");
        service.AddTestScenarioStep("mqtt.publisher");
        service.AddTestScenarioStep("expect.event");
        service.AddTestScenarioStep("expect.event");

        service.MoveTestScenarioStep("expectEvent2", -1);

        service.GetActiveTestScenario()
            .ShouldNotBeNull()
            .Steps
            .Select(step => step.Name)
            .ShouldBe(["publishMessage", "expectEvent2", "expectEvent"]);
        service.HasUnsavedChanges.ShouldBeTrue();
    }

    [Fact]
    public void RecordManualMqttPublish_UpdatesPublishedDashboardCounter()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "published": {
                        "row": 0,
                        "column": 0,
                        "widget": "published"
                      }
                    }
                  },
                  "widgets": {
                    "published": {
                      "type": "event.counter",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test",
                        "status": "published"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");
        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["published"];

        service.RecordManualMqttPublish("test", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");

        var snapshot = service.GetDashboardEventSnapshot(widget);
        snapshot.Count.ShouldBe(1);
        snapshot.LatestEvent.ShouldNotBeNull();
        snapshot.LatestEvent.Source.ShouldBe("LivePublisher");
        snapshot.LatestEvent.Channel.ShouldBe("test");
        snapshot.Events.ShouldHaveSingleItem().Channel.ShouldBe("test");
        snapshot.LatestEvent.GetAttribute("qos").ShouldBe("0");
        snapshot.LatestEvent.GetAttribute("retain").ShouldBe("False");
        snapshot.TopicCounts.ShouldBe([new DashboardTopicMetric("test", 1)]);
        snapshot.UniqueTopicCount.ShouldBe(1);
        snapshot.TotalPayloadBytes.ShouldBe(Encoding.UTF8.GetByteCount("""{"hello":"fluxmq"}"""));
        snapshot.RetainedCount.ShouldBe(0);
        service.Logs.ShouldContain(log =>
            log.Source == "LivePublisher" &&
            log.Code == FluxMqEventTypes.MqttMessagePublished &&
            log.Context != null &&
            log.Context.Contains("topic=test", StringComparison.Ordinal) &&
            log.Context.Contains("connection=local-broker", StringComparison.Ordinal));
    }

    [Fact]
    public void RecordManualMqttPublish_UpdatesPublishedDashboardRate()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "publishedRate": {
                        "row": 0,
                        "column": 0,
                        "widget": "publishedRate"
                      }
                    }
                  },
                  "widgets": {
                    "publishedRate": {
                      "type": "event.rate",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test/",
                        "status": "published"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");
        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["publishedRate"];

        service.RecordManualMqttPublish("test/one", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("other/skip", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("test/two", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");

        var snapshot = service.GetDashboardEventSnapshot(widget);
        snapshot.Count.ShouldBe(2);
        snapshot.RecentCount.ShouldBe(2);
        snapshot.RateWindow.ShouldBe(TimeSpan.FromMinutes(1));
        snapshot.EventsPerSecond.ShouldBe(2d / snapshot.RateWindow.TotalSeconds, 0.0001);
        snapshot.LatestEvent.ShouldNotBeNull();
        snapshot.LatestEvent.Channel.ShouldBe("test/two");
        snapshot.Events.Select(static flowEvent => flowEvent.Channel).ShouldBe(["test/two", "test/one"]);
        snapshot.TopicCounts.ShouldBe([
            new DashboardTopicMetric("test/one", 1),
            new DashboardTopicMetric("test/two", 1)
        ]);
        snapshot.UniqueTopicCount.ShouldBe(2);
        snapshot.TotalPayloadBytes.ShouldBe(Encoding.UTF8.GetByteCount("""{"hello":"fluxmq"}""") * 2);
    }

    [Fact]
    public void GetDashboardEventSnapshot_UsesBoundMetricFiltersAndWindow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "publishedRate": {
                        "row": 0,
                        "column": 0,
                        "widget": "publishedRate"
                      }
                    }
                  },
                  "metrics": {
                    "publishedRateMetric": {
                      "source": "runtimeEvents",
                      "aggregation": "rate",
                      "window": "300s",
                      "filters": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test/",
                        "status": "published"
                      }
                    }
                  },
                  "bindings": {
                    "publishedRate": {
                      "primaryMetric": "publishedRateMetric",
                      "metrics": ["publishedRateMetric"]
                    }
                  },
                  "widgets": {
                    "publishedRate": {
                      "type": "event.rate",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "other/",
                        "status": "published"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");
        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["publishedRate"];

        service.RecordManualMqttPublish("test/one", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("other/skip", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("test/two", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");

        var snapshot = service.GetDashboardEventSnapshot(widget);

        snapshot.Count.ShouldBe(2);
        snapshot.RateWindow.ShouldBe(TimeSpan.FromMinutes(5));
        snapshot.EventsPerSecond.ShouldBe(2d / snapshot.RateWindow.TotalSeconds, 0.0001);
        snapshot.Events.Select(static flowEvent => flowEvent.Channel).ShouldBe(["test/two", "test/one"]);
    }

    [Fact]
    public void GetDashboardMetricValue_UsesBoundMetricAggregationForKpi()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("""
        {
          "FluxMq": {
            "FlowApplication": {
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "payload": {
                        "row": 0,
                        "column": 0,
                        "widget": "payload"
                      }
                    }
                  },
                  "metrics": {
                    "payloadMetric": {
                      "source": "runtimeEvents",
                      "aggregation": "payloadBytes",
                      "window": "60s",
                      "filters": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test/",
                        "status": "published"
                      },
                      "format": {
                        "unit": "bytes"
                      }
                    }
                  },
                  "bindings": {
                    "payload": {
                      "primaryMetric": "payloadMetric",
                      "metrics": ["payloadMetric"]
                    }
                  },
                  "widgets": {
                    "payload": {
                      "type": "kpi.tile",
                      "configuration": {
                        "title": "Payload",
                        "primaryMetric": "messages"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");
        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["payload"];

        service.RecordManualMqttPublish("test/one", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("other/skip", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");
        service.RecordManualMqttPublish("test/two", """{"hello":"fluxmq"}""", 0, retain: false, "local-broker");

        var value = service.GetDashboardMetricValue(widget);

        var expectedBytes = Encoding.UTF8.GetByteCount("""{"hello":"fluxmq"}""") * 2;
        value.Label.ShouldBe("Payload bytes");
        value.Value.ShouldBe(expectedBytes);
        value.Unit.ShouldBe("bytes");
        value.FormattedValue.ShouldBe($"{expectedBytes} B");
    }

    [Fact]
    public async Task RunAsync_CollectsRuntimeEventsForDashboardWidgets()
    {
        var mqttClient = new FakeRuntimeMqttBrokerClient();
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer(), runtimeClientFactory: _ => mqttClient);
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
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "events": {
                        "row": 0,
                        "column": 0,
                        "widget": "received"
                      }
                    }
                  },
                  "widgets": {
                    "received": {
                      "type": "event.counter",
                      "configuration": {
                        "eventType": "mqtt.message.received",
                        "topicStartsWith": "factory/",
                        "subjectStartsWith": "factory/",
                        "status": "received"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");

        await service.RunAsync();
        await mqttClient.WriteAsync(new MqttEnvelope { Topic = "other/skip", Payload = [1] });
        await mqttClient.WriteAsync(new MqttEnvelope { Topic = "factory/one", Payload = [1, 2, 3] });

        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["received"];
        await WaitUntilAsync(() => service.GetDashboardEventSnapshot(widget).Count == 1);

        var snapshot = service.GetDashboardEventSnapshot(widget);
        snapshot.Count.ShouldBe(1);
        snapshot.LatestEvent.ShouldNotBeNull();
        snapshot.LatestEvent.Channel.ShouldBe("factory/one");
        snapshot.TopicCounts.ShouldBe([new DashboardTopicMetric("factory/one", 1)]);
        snapshot.UniqueTopicCount.ShouldBe(1);
        snapshot.TotalPayloadBytes.ShouldBe(3);
    }

    [Fact]
    public async Task RunAsync_ProjectsGeneratedSourceMessagesToDashboardEvents()
    {
        await using var service = new FlowWorkspaceService(new FlowDefinitionComposer());
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
                        { "topic": "factory/line-a/temperature", "payload": "{\"value\":21}", "qos": 0, "retain": false },
                        { "topic": "other/skip", "payload": "skip", "qos": 0, "retain": false },
                        { "topic": "factory/line-b/temperature", "payload": "{\"value\":28}", "qos": 1, "retain": true }
                      ]
                    }
                  }
                }
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": {
                      "events": {
                        "row": 0,
                        "column": 0,
                        "widget": "received"
                      }
                    }
                  },
                  "widgets": {
                    "received": {
                      "type": "event.counter",
                      "configuration": {
                        "eventType": "mqtt.message.received",
                        "topicStartsWith": "factory/",
                        "status": "received"
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """);
        service.SetActiveDashboard("ops");

        await service.RunAsync();

        var widget = service.GetActiveDashboardLayout()
            .ShouldNotBeNull()
            .Widgets["received"];
        await WaitUntilAsync(() => service.GetDashboardEventSnapshot(widget).Count == 2);

        var snapshot = service.GetDashboardEventSnapshot(widget);
        snapshot.Count.ShouldBe(2);
        snapshot.LatestEvent.ShouldNotBeNull();
        snapshot.LatestEvent.Source.ShouldBe("RuntimeSource");
        snapshot.LatestEvent.Channel.ShouldBe("factory/line-b/temperature");
        snapshot.TopicCounts.ShouldBe([
            new DashboardTopicMetric("factory/line-a/temperature", 1),
            new DashboardTopicMetric("factory/line-b/temperature", 1)
        ]);
        snapshot.TotalPayloadBytes.ShouldBe(24);
        snapshot.RetainedCount.ShouldBe(1);
        service.Logs.ShouldContain(log =>
            log.Source == "RuntimeSource" &&
            log.WorkflowName == "flow" &&
            log.NodeName == "generated" &&
            log.Code == FluxMqEventTypes.MqttMessageReceived);
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
                  "filter": { "type": "flow.filter" }
                },
                "pip2": {
                  "filter": { "type": "flow.filter" },
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

    private static string CreateScenarioFailureDetails(
        ScenarioRunResult result,
        IReadOnlyList<WorkspaceLogEntry> logs)
    {
        var stepDetails = string.Join(
            Environment.NewLine,
            result.Steps.Select(step =>
                $"{step.Name}: {step.Status} - {step.Message} - matched={step.MatchedEvent?.Type}/{step.MatchedEvent?.Channel}/{step.MatchedEvent?.SourceNodeId}"));
        var logDetails = string.Join(
            Environment.NewLine,
            logs.Select(log => $"{log.Source}/{log.Code}/{log.WorkflowName}/{log.NodeName}: {log.Message} {log.Context}"));

        return string.Join(
            Environment.NewLine,
            [
                "Scenario steps:",
                stepDetails,
                $"Logs: {logs.Count}",
                logDetails
            ]);
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

    private sealed class SharedFakeBroker
    {
        private readonly object _sync = new();
        private readonly List<BrokerMqttClient> _mqttClients = [];

        public IMqttBrokerClient CreateClient(MqttConnectionProfile profile)
        {
            var mqttClient = new BrokerMqttClient(this, profile);
            lock (_sync)
            {
                _mqttClients.Add(mqttClient);
            }

            return mqttClient;
        }

        private Task PublishAsync(
            string topic,
            byte[] payload,
            MqttQualityOfServiceLevel qos,
            bool retain)
        {
            BrokerMqttClient[] mqttClients;
            lock (_sync)
            {
                mqttClients = _mqttClients
                    .Where(mqttClient => mqttClient.State == MqttClientState.Connected && mqttClient.Matches(topic))
                    .ToArray();
            }

            foreach (var mqttClient in mqttClients)
            {
                mqttClient.Write(new MqttEnvelope
                {
                    Topic = topic,
                    Payload = payload,
                    QualityOfService = qos,
                    Retain = retain
                });
            }

            return Task.CompletedTask;
        }

        private void Remove(BrokerMqttClient mqttClient)
        {
            lock (_sync)
            {
                _mqttClients.Remove(mqttClient);
            }
        }

        private sealed class BrokerMqttClient(SharedFakeBroker broker, MqttConnectionProfile profile) : IMqttBrokerClient
        {
            private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
            private readonly List<string> _subscriptions = [];

            public MqttConnectionProfile Profile { get; } = profile;
            public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
            public ChannelReader<MqttEnvelope> Messages => _messages.Reader;

            public event EventHandler<MqttClientState>? StateChanged
            {
                add { }
                remove { }
            }

            public Task ConnectAsync(CancellationToken ct = default)
            {
                State = MqttClientState.Connected;
                return Task.CompletedTask;
            }

            public Task DisconnectAsync(CancellationToken ct = default)
            {
                State = MqttClientState.Disconnected;
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
                _subscriptions.Add(topicFilter);
                return Task.CompletedTask;
            }

            public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default)
            {
                _subscriptions.RemoveAll(subscription => string.Equals(subscription, topicFilter, StringComparison.Ordinal));
                return Task.CompletedTask;
            }

            public Task PublishAsync(
                string topic,
                byte[] payload,
                MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
                bool retain = false,
                CancellationToken ct = default)
                => broker.PublishAsync(topic, payload, qos, retain);

            public bool Matches(string topic)
                => _subscriptions.Any(subscription => TopicMatches(subscription, topic));

            public void Write(MqttEnvelope envelope)
                => _messages.Writer.TryWrite(envelope);

            public ValueTask DisposeAsync()
            {
                broker.Remove(this);
                _messages.Writer.TryComplete();
                return ValueTask.CompletedTask;
            }

            private static bool TopicMatches(string filter, string topic)
            {
                if (filter == "#")
                {
                    return true;
                }

                if (filter.EndsWith("/#", StringComparison.Ordinal))
                {
                    var prefix = filter[..^1];
                    return topic.StartsWith(prefix, StringComparison.Ordinal);
                }

                return string.Equals(filter, topic, StringComparison.Ordinal);
            }
        }
    }

    private sealed class FakeRuntimeMqttBrokerClient : IMqttBrokerClient
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
        private readonly bool _echoPublishes;

        public FakeRuntimeMqttBrokerClient(bool echoPublishes = false, MqttConnectionProfile? profile = null)
        {
            _echoPublishes = echoPublishes;
            Profile = profile ?? new MqttConnectionProfile { Name = "test" };
        }

        public MqttConnectionProfile Profile { get; }
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public List<PublishedMessage> Published { get; } = [];

        public event EventHandler<MqttClientState>? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Disconnected;
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

            if (_echoPublishes)
            {
                return _messages.Writer.WriteAsync(new MqttEnvelope
                {
                    Topic = topic,
                    Payload = payload,
                    QualityOfService = qos,
                    Retain = retain
                }, ct).AsTask();
            }

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
