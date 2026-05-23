using FluxMq.Core.Models;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

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
}
