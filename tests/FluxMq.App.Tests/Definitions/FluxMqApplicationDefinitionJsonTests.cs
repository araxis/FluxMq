using Shouldly;
using FluxFlow.Engine.Definitions;
using FluxMq.App.Definitions;
using FluxMq.App.Metrics;
using FluxMq.Scenarios;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.App.Tests.Definitions;

public sealed class FluxMqApplicationDefinitionJsonTests
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

        var definition = JsonSerializer.Deserialize<FluxMqApplicationDefinition>(
            json,
            FluxMqApplicationDefinitionJson.CreateSerializerOptions());

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
            FluxMqApplicationDefinitionJson.CreateSerializerOptions());

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
    public void Deserialize_SupportsDashboardAndTestArtifacts()
    {
        const string json = """
            {
              "workflows": {
                "observeTraffic": {
                  "source": {
                    "type": "mqtt.trigger"
                  }
                }
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["280", "2*", "25%"],
                    "rows": ["96", "*"],
                    "columnPadding": [0, 8, 12],
                    "rowPadding": [4, 0],
                    "cells": {
                      "main": {
                        "row": 0,
                        "column": 0,
                        "rowSpan": 2,
                        "columnSpan": 3,
                        "widget": "messageRate"
                      }
                    }
                  },
                  "widgets": {
                    "messageRate": {
                      "type": "metric.card",
                      "configuration": {
                        "source": "runtime.events",
                        "metric": "messagesPerSecond"
                      }
                    }
                  }
                }
              },
              "tests": {
                "mqttRoundTrip": {
                  "steps": {
                    "publishRequest": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker"
                      }
                    },
                    "expectResponse": {
                      "type": "expect.event",
                      "configuration": {
                        "eventType": "mqtt.message.received"
                      }
                    }
                  }
                }
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<FluxMqApplicationDefinition>(
            json,
            FluxMqApplicationDefinitionJson.CreateSerializerOptions());

        definition.ShouldNotBeNull();
        var dashboard = definition!.Dashboards["ops"];
        dashboard.Layout.Columns.ShouldBe([
            DashboardGridTrackDefinition.Fixed(280),
            DashboardGridTrackDefinition.Star(2),
            DashboardGridTrackDefinition.Percent(25)
        ]);
        dashboard.Layout.Rows.ShouldBe([
            DashboardGridTrackDefinition.Fixed(96),
            DashboardGridTrackDefinition.Star()
        ]);
        dashboard.Layout.ColumnPadding.ShouldBe([0d, 8d, 12d]);
        dashboard.Layout.RowPadding.ShouldBe([4d, 0d]);
        dashboard.Layout.Cells["main"].Widget.ShouldBe("messageRate");
        dashboard.Widgets["messageRate"].Type.ShouldBe("metric.card");
        dashboard.Widgets["messageRate"].Configuration["metric"].GetString().ShouldBe("messagesPerSecond");

        var scenario = definition.Tests["mqttRoundTrip"];
        scenario.Steps["publishRequest"].Type.ShouldBe("mqtt.publisher");
        scenario.Steps["expectResponse"].Configuration["eventType"].GetString().ShouldBe("mqtt.message.received");
    }

    [Fact]
    public void Deserialize_TreatsNullCollectionsAsEmptyCollections()
    {
        const string json = """
            {
              "workflows": {
                "observeTraffic": {
                  "source": {
                    "type": "mqtt.trigger",
                    "configuration": null
                  }
                }
              },
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["*"],
                    "rows": ["*"],
                    "cells": null
                  },
                  "widgets": null
                }
              },
              "tests": {
                "roundTrip": {
                  "steps": null
                }
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<FluxMqApplicationDefinition>(
            json,
            FluxMqApplicationDefinitionJson.CreateSerializerOptions());

        definition.ShouldNotBeNull();
        definition!.Workflows["observeTraffic"].Nodes["source"].Configuration.ShouldBeEmpty();
        definition.Dashboards["ops"].Layout.Cells.ShouldBeEmpty();
        definition.Dashboards["ops"].Widgets.ShouldBeEmpty();
        definition.Tests["roundTrip"].Steps.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_NormalizesLegacyNestedMetricAttributeFilters()
    {
        const string json = """
            {
              "metrics": {
                "d1.eventCounterMetric": {
                  "version": 1,
                  "displayName": "Event counter",
                  "definition": {
                    "name": "Event counter",
                    "source": "runtimeEvents",
                    "measure": "count",
                    "window": "60s",
                    "format": "number",
                    "additionalFilters": {
                      "attributes": {
                        "qos": "1",
                        "retain": false
                      }
                    },
                    "labels": {},
                    "exportPolicy": {
                      "enabled": false
                    },
                    "mode": "builder"
                  }
                }
              }
            }
            """;

        var definition = JsonSerializer.Deserialize<FluxMqApplicationDefinition>(
            json,
            FluxMqApplicationDefinitionJson.CreateSerializerOptions());

        definition.ShouldNotBeNull();
        var filters = definition!.Metrics["d1.eventCounterMetric"].Definition.AdditionalFilters;
        filters[FluxMetricCatalog.AttributeFilterKey("qos")].ShouldBe("1");
        filters[FluxMetricCatalog.AttributeFilterKey("retain")].ShouldBe("false");
        filters.ContainsKey("attributes").ShouldBeFalse();
    }

    [Fact]
    public void MigrateRoot_PromotesDashboardMetricAttributesAsFlatAdditionalFilters()
    {
        var root = JsonNode.Parse(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "dashboards": {
                    "d1": {
                      "metrics": {
                        "eventCounterMetric": {
                          "source": "runtimeEvents",
                          "aggregation": "count",
                          "window": "60s",
                          "filters": {
                            "eventType": "mqtt.message.published",
                            "attributes": {
                              "qos": "1",
                              "retain": false
                            }
                          },
                          "format": {
                            "unit": "number"
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """)!.AsObject();

        FluxMqApplicationDefinitionMigrator.MigrateRoot(root);

        var flowApplication = root["FluxMq"]!["FlowApplication"]!.AsObject();
        var additionalFilters = flowApplication["metrics"]!["d1.eventCounterMetric"]!["definition"]!["additionalFilters"]!.AsObject();
        additionalFilters.ContainsKey("attributes").ShouldBeFalse();
        additionalFilters[FluxMetricCatalog.AttributeFilterKey("qos")]!.GetValue<string>().ShouldBe("1");
        additionalFilters[FluxMetricCatalog.AttributeFilterKey("retain")]!.GetValue<string>().ShouldBe("false");
    }

    [Fact]
    public void Serialize_KeepsWorkflowsAndNodesAsObjectProperties()
    {
        var definition = new FluxMqApplicationDefinition
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

        var json = JsonSerializer.Serialize(definition, FluxMqApplicationDefinitionJson.CreateSerializerOptions());

        json.ShouldContain("\"observeTraffic\"");
        json.ShouldContain("\"source\"");
        json.ShouldContain("\"metrics\"");
        json.ShouldContain("\"Input\": \"source.Output\"");
        json.ShouldNotContain("\"nodes\"");
        json.ShouldNotContain("\"connections\"");
    }

    [Fact]
    public void Serialize_KeepsDashboardAndTestArtifactsAsNamedObjects()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Workflows =
            {
                ["observeTraffic"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = new NodeDefinition { Type = new NodeType("mqtt.trigger") }
                    }
                }
            },
            Dashboards =
            {
                ["ops"] = new DashboardDefinition
                {
                    Layout = new DashboardLayoutDefinition
                    {
                        Columns =
                        [
                            DashboardGridTrackDefinition.Fixed(280),
                            DashboardGridTrackDefinition.Star(),
                            DashboardGridTrackDefinition.Percent(25)
                        ],
                        Rows =
                        [
                            DashboardGridTrackDefinition.Fixed(96),
                            DashboardGridTrackDefinition.Star()
                        ],
                        ColumnPadding = [0, 8, 12],
                        RowPadding = [4, 0],
                        Cells =
                        {
                            ["main"] = new DashboardCellDefinition
                            {
                                Row = 0,
                                Column = 0,
                                RowSpan = 2,
                                ColumnSpan = 3,
                                Widget = "latest"
                            }
                        }
                    },
                    Widgets =
                    {
                        ["latest"] = new DashboardWidgetDefinition { Type = "payload.latest" }
                    }
                }
            },
            Tests =
            {
                ["roundTrip"] = new ScenarioDefinition
                {
                    Steps =
                    {
                        ["expect"] = new ScenarioStepDefinition { Type = "expect.event" }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(definition, FluxMqApplicationDefinitionJson.CreateSerializerOptions());

        json.ShouldContain("\"dashboards\"");
        json.ShouldContain("\"ops\"");
        json.ShouldContain("\"columns\": [");
        json.ShouldContain("\"280\"");
        json.ShouldContain("\"*\"");
        json.ShouldContain("\"25%\"");
        json.ShouldContain("\"columnPadding\": [");
        json.ShouldContain("8");
        json.ShouldContain("\"rowPadding\": [");
        json.ShouldContain("\"widgets\"");
        json.ShouldContain("\"tests\"");
        json.ShouldContain("\"roundTrip\"");
        json.ShouldContain("\"expect.event\"");
    }

    [Theory]
    [InlineData("320", DashboardGridTrackUnit.Fixed, 320)]
    [InlineData("320px", DashboardGridTrackUnit.Fixed, 320)]
    [InlineData("25%", DashboardGridTrackUnit.Percent, 25)]
    [InlineData("*", DashboardGridTrackUnit.Star, 1)]
    [InlineData("2*", DashboardGridTrackUnit.Star, 2)]
    public void DashboardGridTrackDefinition_ParseSupportsWpfLikeSizes(
        string size,
        DashboardGridTrackUnit expectedUnit,
        double expectedValue)
    {
        var track = DashboardGridTrackDefinition.Parse(size);

        track.Unit.ShouldBe(expectedUnit);
        track.Value.ShouldBe(expectedValue);
    }
}
