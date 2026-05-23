using Shouldly;
using FluxMq.Pipeline.Definitions;
using System.Text.Json;

namespace FluxMq.Pipeline.Tests.Definitions;

public sealed class FlowApplicationDefinitionJsonTests
{
    [Fact]
    public void Deserialize_SupportsApplicationResourcesAndWorkflowNodeObjects()
    {
        const string json = """
            {
              "resources": {
                "broker": {
                  "type": "mqtt.connection"
                }
              },
              "workflows": {
                "observeTraffic": {
                  "source": {
                    "type": "mqtt.trigger"
                  },
                  "metrics": {
                    "type": "mqtt.metrics",
                    "Input": "source.Output"
                  }
                }
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<ApplicationDefinition>(
            json,
            ApplicationDefinitionJson.CreateSerializerOptions());

        definition.ShouldNotBeNull();
        definition!.Resources.Keys.ShouldContain("broker");
        definition.Workflows.Keys.ShouldContain("observeTraffic");
        definition.Workflows["observeTraffic"].Nodes.Keys.ShouldContain("source");
        definition.Workflows["observeTraffic"].Nodes.Keys.ShouldContain("metrics");

        var metrics = definition.Workflows["observeTraffic"].Nodes["metrics"];
        metrics.Type.ShouldBe(new NodeType("mqtt.metrics"));
        metrics.GetPortLinks("Input", "observeTraffic").ShouldHaveSingleItem()
            .From.ShouldBe(new PortAddress("observeTraffic", new NodeName("source"), new PortName("Output")));
    }

    [Fact]
    public void ParsePortLinks_SupportsStringArrayAndObjectForms()
    {
        const string json = """
            {
              "type": "mqtt.recorder",
              "When": "payload.size > 0",
              "Input": [
                "source.Output",
                {
                  "From": "replay.Output",
                  "When": "topic.startsWith('factory/')"
                },
                {
                  "From": "router.WhenTrue"
                }
              ]
            }
            """;

        var node = JsonSerializer.Deserialize<NodeDefinition>(
            json,
            ApplicationDefinitionJson.CreateSerializerOptions());

        var links = node!.GetPortLinks("Input", "myWorkflow");

        links.Count.ShouldBe(3);
        links[0].From.ToString().ShouldBe("myWorkflow.source.Output");
        links[0].When.ShouldBe("payload.size > 0");
        links[1].From.ToString().ShouldBe("myWorkflow.replay.Output");
        links[1].When.ShouldBe("topic.startsWith('factory/')");
        links[2].From.ToString().ShouldBe("myWorkflow.router.WhenTrue");
        links[2].When.ShouldBe("payload.size > 0");
    }

    [Fact]
    public void Serialize_KeepsWorkflowsAndNodesAsObjectProperties()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["observeTraffic"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = new NodeDefinition
                        {
                            Type = new NodeType("mqtt.trigger")
                        },
                        ["metrics"] = new NodeDefinition
                        {
                            Type = new NodeType("mqtt.metrics"),
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(definition, ApplicationDefinitionJson.CreateSerializerOptions());

        json.ShouldContain("\"observeTraffic\"");
        json.ShouldContain("\"source\"");
        json.ShouldContain("\"metrics\"");
        json.ShouldContain("\"Input\": \"source.Output\"");
        json.ShouldNotContain("\"nodes\"");
        json.ShouldNotContain("\"connections\"");
    }
}
