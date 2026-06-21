using FluxMq.App.Definitions;
using FluxMq.Scenarios;
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
            },
            Explorers =
            {
                ["local"] = new ExplorerDefinition
                {
                    Type = ExplorerDefinition.MqttTopicsType,
                    ConnectionResource = "broker"
                }
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
              },
              "explorers": {
                "local": {
                  "type": "mqtt.topics",
                  "displayName": "Local broker",
                  "connectionResource": "broker",
                  "connection": {
                    "clientId": "topic-monitor",
                    "useTls": true,
                    "allowUntrustedCertificates": true,
                    "caCertificatePath": "certs/root.pem",
                    "clientCertificatePath": "certs/client.pfx",
                    "clientCertificatePassword": "cert-pass",
                    "cleanStart": false,
                    "keepAliveSeconds": 45,
                    "username": "viewer",
                    "passwordSecret": "local-broker-password"
                  },
                  "subscriptions": [ "#", "$SYS/#" ],
                  "autoConnect": true
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
        var explorer = definition.Explorers["local"];
        explorer.Type.ShouldBe(ExplorerDefinition.MqttTopicsType);
        explorer.DisplayName.ShouldBe("Local broker");
        explorer.ConnectionResource.ShouldBe("broker");
        var connection = explorer.Connection.ShouldNotBeNull();
        connection.ClientId.ShouldBe("topic-monitor");
        connection.UseTls.ShouldBe(true);
        connection.AllowUntrustedCertificates.ShouldBe(true);
        connection.CaCertificatePath.ShouldBe("certs/root.pem");
        connection.ClientCertificatePath.ShouldBe("certs/client.pfx");
        connection.ClientCertificatePassword.ShouldBe("cert-pass");
        connection.CleanStart.ShouldBe(false);
        connection.KeepAliveSeconds.ShouldBe(45);
        connection.Username.ShouldBe("viewer");
        var passwordSecret = connection.PasswordSecret.ShouldNotBeNull();
        passwordSecret.Name.Value.ShouldBe("local-broker-password");
        explorer.Subscriptions.ShouldBe(["#", "$SYS/#"]);

        var engineDefinition = definition.ToEngineDefinition();

        engineDefinition.Resources.ShouldContainKey("broker");
        engineDefinition.Workflows.ShouldContainKey("main");
        new EngineApplicationDefinitionValidator().Validate(engineDefinition).IsValid.ShouldBeTrue();
    }
}
