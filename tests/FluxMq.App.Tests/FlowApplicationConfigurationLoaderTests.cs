using Shouldly;
using FluxMq.App;
using FluxMq.Pipeline.Definitions;
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
                  },
                  "dashboards": {
                    "ops": {
                      "layout": {
                        "columns": ["240", "*", "30%"],
                        "rows": ["120", "2*"],
                        "cells": {
                          "main": {
                            "row": 0,
                            "column": 0,
                            "columnSpan": 3,
                            "widget": "latest"
                          }
                        }
                      },
                      "widgets": {
                        "latest": {
                          "type": "payload.latest"
                        }
                      }
                    }
                  },
                  "tests": {
                    "roundTrip": {
                      "steps": {
                        "expect": {
                          "type": "expect.event"
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
        definition.Dashboards["ops"].Layout.Columns.ShouldBe([
            DashboardGridTrackDefinition.Fixed(240),
            DashboardGridTrackDefinition.Star(),
            DashboardGridTrackDefinition.Percent(30)
        ]);
        definition.Dashboards["ops"].Layout.Rows.ShouldBe([
            DashboardGridTrackDefinition.Fixed(120),
            DashboardGridTrackDefinition.Star(2)
        ]);
        definition.Dashboards["ops"].Widgets["latest"].Type.ShouldBe("payload.latest");
        definition.Tests["roundTrip"].Steps["expect"].Type.ShouldBe("expect.event");
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
