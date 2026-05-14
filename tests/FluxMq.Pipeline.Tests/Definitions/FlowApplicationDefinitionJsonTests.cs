using FluentAssertions;
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
                    "type": "mqtt.metrics-sink",
                    "Input": "source.Output"
                  }
                }
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<ApplicationDefinition>(
            json,
            ApplicationDefinitionJson.CreateSerializerOptions());

        definition.Should().NotBeNull();
        definition!.Resources.Keys.Should().Contain("broker");
        definition.Workflows.Keys.Should().Contain("observeTraffic");
        definition.Workflows["observeTraffic"].Nodes.Keys.Should().Contain(["source", "metrics"]);

        var metrics = definition.Workflows["observeTraffic"].Nodes["metrics"];
        metrics.Type.Should().Be(new NodeType("mqtt.metrics-sink"));
        metrics.GetPortLinks("Input", "observeTraffic").Should().ContainSingle()
            .Which.From.Should().Be(new PortAddress("observeTraffic", new NodeName("source"), new PortName("Output")));
    }

    [Fact]
    public void ParsePortLinks_SupportsStringArrayAndObjectForms()
    {
        const string json = """
            {
              "type": "mqtt.recording-sink",
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

        links.Should().HaveCount(3);
        links[0].From.ToString().Should().Be("myWorkflow.source.Output");
        links[0].When.Should().Be("payload.size > 0");
        links[1].From.ToString().Should().Be("myWorkflow.replay.Output");
        links[1].When.Should().Be("topic.startsWith('factory/')");
        links[2].From.ToString().Should().Be("myWorkflow.router.WhenTrue");
        links[2].When.Should().Be("payload.size > 0");
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
                            Type = new NodeType("mqtt.metrics-sink"),
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

        json.Should().Contain("\"observeTraffic\"");
        json.Should().Contain("\"source\"");
        json.Should().Contain("\"metrics\"");
        json.Should().Contain("\"Input\": \"source.Output\"");
        json.Should().NotContain("\"nodes\"");
        json.Should().NotContain("\"connections\"");
    }
}
