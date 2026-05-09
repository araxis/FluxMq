using FluentAssertions;
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

        definition.Workflows.Should().ContainKey("observe");
        definition.Workflows["observe"]["inspect"].Type.Value.Should().Be("mqtt.payload-inspector");
        definition.Workflows["observe"]["inspect"].Configuration["boundedCapacity"].GetInt32().Should().Be(250);
    }

    [Fact]
    public void Load_ThrowsWhenSectionIsMissing()
    {
        var configuration = BuildConfiguration("""{ "FluxMq": {} }""");

        var act = () => new FlowApplicationConfigurationLoader().Load(configuration);

        act.Should().Throw<FlowApplicationConfigurationException>()
            .WithMessage("*FluxMq:FlowApplication*");
    }

    private static IConfiguration BuildConfiguration(string json)
        => new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();
}
