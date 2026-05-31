using FluxMq.App.Definitions;
using FluxMq.Pipeline.Definitions;
using Shouldly;
using System.Text.Json;
using EngineApplicationDefinitionValidator = FluxFlow.Engine.Definitions.ApplicationDefinitionValidator;
using EngineNodeDefinition = FluxFlow.Engine.Definitions.NodeDefinition;
using EngineNodeType = FluxFlow.Engine.Definitions.NodeType;
using EngineWorkflowDefinition = FluxFlow.Engine.Definitions.WorkflowDefinition;

namespace FluxMq.App.Tests.Definitions;

public sealed class FluxMqApplicationDefinitionTests
{
    [Fact]
    public void ToEngineDefinition_ProjectsOnlyExecutableResourcesAndWorkflows()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Resources =
            {
                ["broker"] = new EngineNodeDefinition
                {
                    Type = new EngineNodeType("mqtt.connection")
                }
            },
            Workflows =
            {
                ["main"] = new EngineWorkflowDefinition
                {
                    Nodes =
                    {
                        ["trigger"] = new EngineNodeDefinition
                        {
                            Type = new EngineNodeType("mqtt.trigger")
                        }
                    }
                }
            },
            Dashboards =
            {
                ["ops"] = new DashboardDefinition()
            },
            Tests =
            {
                ["smoke"] = new ScenarioDefinition()
            }
        };

        var engineDefinition = definition.ToEngineDefinition();

        engineDefinition.Resources.Keys.ShouldBe(["broker"]);
        engineDefinition.Workflows.Keys.ShouldBe(["main"]);
        new EngineApplicationDefinitionValidator().Validate(engineDefinition).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void JsonOptions_ReadWorkspaceDocumentAndProjectEngineDefinition()
    {
        var definition = JsonSerializer.Deserialize<FluxMqApplicationDefinition>(
            """
            {
              "resources": {
                "broker": {
                  "type": "mqtt.connection"
                }
              },
              "workflows": {
                "main": {
                  "trigger": {
                    "type": "mqtt.trigger"
                  },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": "trigger.Output"
                  }
                }
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": [ "240", "*", "30%" ],
                    "rows": [ "120", "2*" ]
                  },
                  "widgets": {
                    "latest": {
                      "type": "payload.latest"
                    }
                  }
                }
              },
              "tests": {
                "smoke": {
                  "steps": {
                    "expect": {
                      "type": "expect.event"
                    }
                  }
                }
              }
            }
            """,
            FluxMqApplicationDefinitionJson.CreateSerializerOptions()).ShouldNotBeNull();

        definition.Resources["broker"].Type.Value.ShouldBe("mqtt.connection");
        definition.Workflows["main"].Nodes["inspect"].Ports["Input"].GetString().ShouldBe("trigger.Output");
        definition.Dashboards["ops"].Layout.Columns.ShouldBe([
            DashboardGridTrackDefinition.Fixed(240),
            DashboardGridTrackDefinition.Star(),
            DashboardGridTrackDefinition.Percent(30)
        ]);
        definition.Dashboards["ops"].Layout.Rows.ShouldBe([
            DashboardGridTrackDefinition.Fixed(120),
            DashboardGridTrackDefinition.Star(2)
        ]);
        definition.Tests["smoke"].Steps["expect"].Type.ShouldBe("expect.event");

        var engineDefinition = definition.ToEngineDefinition();

        engineDefinition.Resources.ShouldContainKey("broker");
        engineDefinition.Workflows.ShouldContainKey("main");
        new EngineApplicationDefinitionValidator().Validate(engineDefinition).IsValid.ShouldBeTrue();
    }
}
