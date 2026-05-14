using Shouldly;
using FluxMq.App;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace FluxMq.App.Tests;

public sealed class FlowApplicationConfigurationLoaderTests
{
    [Fact]
    public void Load_ReadsFlowApplicationDefinitionFromConfigurationSection()
    {
        var configuration = BuildConfiguration(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "inspect": {
                        "type": "mqtt.payload-inspector",
                        "configuration": {
                          "boundedCapacity": 250
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var definition = new FlowApplicationConfigurationLoader().Load(configuration);

        definition.Workflows.ShouldContainKey("observe");
        definition.Workflows["observe"].Nodes["inspect"].Type.Value.ShouldBe("mqtt.payload-inspector");
        definition.Workflows["observe"].Nodes["inspect"].Configuration["boundedCapacity"].GetInt32().ShouldBe(250);
    }

    [Fact]
    public void Load_ThrowsWhenSectionIsMissing()
    {
        var configuration = BuildConfiguration("""{ "FluxMq": {} }""");

        var act = () => new FlowApplicationConfigurationLoader().Load(configuration);

        var ex = Should.Throw<FlowApplicationConfigurationException>(act);
        ex.Message.ShouldContain("FluxMq:FlowApplication");
    }

    private static IConfiguration BuildConfiguration(string json)
        => new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();
}
