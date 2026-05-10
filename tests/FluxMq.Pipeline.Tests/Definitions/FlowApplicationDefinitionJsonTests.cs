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

        var definition = JsonSerializer.Deserialize<FlowApplicationDefinition>(
            json,
            FlowApplicationDefinitionJson.CreateSerializerOptions());

        definition.Should().NotBeNull();
        definition!.Resources.Keys.Should().Contain("broker");
        definition.Workflows.Keys.Should().Contain("observeTraffic");
        definition.Workflows["observeTraffic"].Keys.Should().Contain(["source", "metrics"]);

        var metrics = definition.Workflows["observeTraffic"]["metrics"];
        metrics.Type.Should().Be(new FlowNodeType("mqtt.metrics-sink"));
        metrics.GetPortLinks("Input").Should().ContainSingle()
            .Which.From.Should().Be(new FlowPortReference(new FlowNodeName("source"), new FlowPortName("Output")));
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

        var node = JsonSerializer.Deserialize<FlowNodeDefinition>(
            json,
            FlowApplicationDefinitionJson.CreateSerializerOptions());

        var links = node!.GetPortLinks("Input");

        links.Should().HaveCount(3);
        links[0].From.ToString().Should().Be("source.Output");
        links[0].When.Should().Be("payload.size > 0");
        links[1].From.ToString().Should().Be("replay.Output");
        links[1].When.Should().Be("topic.startsWith('factory/')");
        links[2].From.ToString().Should().Be("router.WhenTrue");
        links[2].When.Should().Be("payload.size > 0");
    }

    [Fact]
    public void Serialize_KeepsWorkflowsAndNodesAsObjectProperties()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["observeTraffic"] = new()
                {
                    ["source"] = new FlowNodeDefinition
                    {
                        Type = new FlowNodeType("mqtt.trigger")
                    },
                    ["metrics"] = new FlowNodeDefinition
                    {
                        Type = new FlowNodeType("mqtt.metrics-sink"),
                        Ports =
                        {
                            ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(definition, FlowApplicationDefinitionJson.CreateSerializerOptions());

        json.Should().Contain("\"observeTraffic\"");
        json.Should().Contain("\"source\"");
        json.Should().Contain("\"metrics\"");
        json.Should().Contain("\"Input\": \"source.Output\"");
        json.Should().NotContain("\"nodes\"");
        json.Should().NotContain("\"connections\"");
    }
}
