using FluxMq.Scenarios;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;
using System.Text.Json;

namespace FluxMq.UI.Tests;

public sealed class FlowDefinitionComposerV2Tests
{
    [Fact]
    public void GetTestScenario_MigratesFlatStepsIntoImportedPhase()
    {
        var composer = new FlowDefinitionComposer();
        const string json = """
            {
              "FluxMq": {
                "FlowApplication": {
                  "tests": {
                    "legacy": {
                      "steps": {
                        "first": {
                          "type": "delay.wait",
                          "configuration": {
                            "delayMs": 0
                          }
                        },
                        "second": {
                          "type": "expect.event",
                          "configuration": {
                            "timeoutMs": 5
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        var scenario = composer.GetTestScenario(json, "legacy").ShouldNotBeNull();

        scenario.Steps.Select(static step => step.Name).ShouldBe(["first", "second"]);
        var phase = scenario.Phases.ShouldHaveSingleItem();
        phase.Kind.ShouldBe(ScenarioPhaseKinds.Imported);
        phase.Steps.Select(static step => step.Name).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void AddScenarioStep_WritesV2PhaseShapeAndFlatCompatibilityMirror()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddTest(composer.CreateEmptyDefinition(), "scenario");
        json = composer.AddScenarioStep(json, "scenario", ScenarioStepTypes.MetricThresholdAssertion);

        using var document = JsonDocument.Parse(json);
        var test = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("tests")
            .GetProperty("scenario");

        test.GetProperty("version").GetInt32().ShouldBe(2);
        var assertPhase = test.GetProperty("phases").GetProperty("assert");
        assertPhase.GetProperty("steps").EnumerateObject().ShouldHaveSingleItem().Value
            .GetProperty("type")
            .GetString()
            .ShouldBe(ScenarioStepTypes.MetricThresholdAssertion);
        test.GetProperty("steps").EnumerateObject().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddDashboardWidget_WritesV2MetricsBindingsAndResponsiveMetadata()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");

        using var document = JsonDocument.Parse(json);
        var app = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication");
        var dashboard = app
            .GetProperty("dashboards")
            .GetProperty("ops");

        dashboard.GetProperty("version").GetInt32().ShouldBe(2);
        dashboard.GetProperty("responsive").GetProperty("breakpoints").TryGetProperty("desktop", out _).ShouldBeTrue();
        dashboard.GetProperty("view").GetProperty("mode").GetString().ShouldBe("design");
        app.GetProperty("metrics").EnumerateObject().ShouldHaveSingleItem();
        dashboard.TryGetProperty("metrics", out _).ShouldBeFalse();
        dashboard.GetProperty("bindings").EnumerateObject().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddDashboardWidget_InlineEventWidgetHasNoMetricResourceOrBinding()
    {
        // Inline event widgets (charts/topic/payload/breakdowns/table/latest/activity) render directly
        // from the runtime event snapshot using their own configuration: no promoted metric, no binding.
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.LineChartType, "slot:0:0");

        using var document = JsonDocument.Parse(json);
        var app = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication");
        var dashboard = app
            .GetProperty("dashboards")
            .GetProperty("ops");

        app.GetProperty("metrics").EnumerateObject().ShouldBeEmpty();
        dashboard.GetProperty("bindings").EnumerateObject().ShouldBeEmpty();
        var widget = dashboard.GetProperty("widgets").EnumerateObject().ShouldHaveSingleItem().Value;
        widget.GetProperty("configuration").TryGetProperty("metric", out _).ShouldBeFalse();
    }

    [Fact]
    public void AddDashboardWidget_WritesFocusedEventCounterWidgetAndMetricQuery()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        var widget = layout.Widgets["eventCounter"];
        var metricId = "ops.eventCounterMetric";
        var metric = layout.Metrics[metricId];

        widget.Configuration.Keys.ShouldContain(DashboardWidgetCatalog.MetricVisualizationKey);
        widget.Configuration["metric"].ShouldBe(metricId);
        widget.Configuration[DashboardMetricValueVisualizationOptions.TitleKey].ShouldBe("Events");
        widget.Configuration[DashboardMetricValueVisualizationOptions.SubtitleKey].ShouldBe("All runtime events");
        widget.Configuration.ContainsKey(DashboardEventFilterCatalog.EventTypeKey).ShouldBeFalse();
        metric.Aggregation.ShouldBe("count");
        metric.Source.ShouldBe("runtimeEvents");
        layout.Bindings["eventCounter"].PrimaryMetric.ShouldBe(metricId);
    }

    [Fact]
    public void GetDashboardLayout_ReadsV2MetricsAndBindings()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventRateType, "slot:0:0");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();

        var metricId = "ops.eventRateMetric";
        layout.Metrics.ContainsKey(metricId).ShouldBeTrue();
        layout.Metrics[metricId].Aggregation.ShouldBe("rate");
        layout.Bindings.ContainsKey("eventRate").ShouldBeTrue();
        layout.Bindings["eventRate"].PrimaryMetric.ShouldBe(metricId);
        layout.Bindings["eventRate"].Metrics.ShouldBe([metricId]);
    }

    [Fact]
    public void GetDashboardLayout_MigratesLegacyKpiPrimaryMetricIntoMetricQuery()
    {
        var composer = new FlowDefinitionComposer();
        const string json = """
            {
              "FluxMq": {
                "FlowApplication": {
                  "dashboards": {
                    "ops": {
                      "layout": {
                        "columns": ["*"],
                        "rows": ["*"],
                        "cells": {
                          "payload": {
                            "row": 0,
                            "column": 0,
                            "widget": "payload"
                          }
                        }
                      },
                      "widgets": {
                        "payload": {
                          "type": "kpi.tile",
                          "configuration": {
                            "title": "KPI tile",
                            "primaryMetric": "payloadBytes"
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();

        layout.Widgets["payload"].Configuration.ContainsKey(DashboardWidgetCatalog.PrimaryMetricKey).ShouldBeFalse();
        layout.Widgets["payload"].Configuration["title"].ShouldBe("Payload bytes");
        layout.Widgets["payload"].Configuration["metric"].ShouldBe("ops.payloadMetric");
        layout.Metrics["ops.payloadMetric"].Aggregation.ShouldBe("payloadBytes");
        layout.Metrics["ops.payloadMetric"].Format["unit"].ShouldBe("bytes");
        layout.Bindings["payload"].PrimaryMetric.ShouldBe("ops.payloadMetric");
    }

    [Fact]
    public void AddDashboardWidget_PlacesDuplicateOrdinalAfterMetricSuffix()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:1");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        layout.Widgets.Keys.ShouldBe(["eventCounter", "eventCounter2"]);
        layout.Metrics.Keys.ShouldBe(["ops.eventCounterMetric", "ops.eventCounterMetric2"], ignoreOrder: true);
        layout.Bindings["eventCounter2"].PrimaryMetric.ShouldBe("ops.eventCounterMetric2");
        layout.Bindings["eventCounter2"].Metrics.ShouldBe(["ops.eventCounterMetric2"]);
    }

    [Fact]
    public void RemoveDashboardMetricIfUnused_KeepsSharedMetricAndDeletesUnusedMetric()
    {
        // A bound app metric is kept; an unreferenced promoted metric is removed.
        const string json = """
            {
              "FluxMq": {
                "FlowApplication": {
                  "metrics": {
                    "ops.eventCounterMetric": {
                      "typeId": "event.count",
                      "displayName": "Events",
                      "parameters": { "window": "60s" },
                      "labels": { "promotedFrom": "ops.eventCounterMetric" }
                    },
                    "ops.unused": {
                      "typeId": "event.count",
                      "displayName": "Unused",
                      "parameters": { "window": "60s" },
                      "labels": { "promotedFrom": "ops.unused" }
                    }
                  },
                  "dashboards": {
                    "ops": {
                      "layout": {
                        "columns": ["*"],
                        "rows": ["*"],
                        "cells": {
                          "eventCounter": { "row": 0, "column": 0, "widget": "eventCounter" }
                        }
                      },
                      "bindings": {
                        "eventCounter": {
                          "primaryMetric": "ops.eventCounterMetric",
                          "metrics": ["ops.eventCounterMetric"]
                        }
                      },
                      "widgets": {
                        "eventCounter": {
                          "type": "event.counter",
                          "configuration": { "metric": "ops.eventCounterMetric" }
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

        var composer = new FlowDefinitionComposer();
        var trimmed = composer.RemoveDashboardMetricIfUnused(json, "ops", "ops.eventCounterMetric");
        trimmed = composer.RemoveDashboardMetricIfUnused(trimmed, "ops", "ops.unused");

        var layout = composer.GetDashboardLayout(trimmed, "ops").ShouldNotBeNull();
        layout.Metrics.ContainsKey("ops.eventCounterMetric").ShouldBeTrue();
        layout.Metrics.ContainsKey("ops.unused").ShouldBeFalse();
    }

    [Fact]
    public void DuplicateDashboardWidget_CopiesWidgetConfigurationAndBinding()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");
        json = composer.UpdateDashboardWidgetConfiguration(
            json,
            "ops",
            "eventCounter",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Factory events",
                ["metric"] = "eventCounterMetric",
                [DashboardEventFilterCatalog.TopicStartsWithKey] = "factory/"
            });

        json = composer.DuplicateDashboardWidget(json, "ops", "eventCounter", "slot:0:1");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        layout.Widgets.ContainsKey("eventCounterCopy").ShouldBeTrue();
        layout.Widgets["eventCounterCopy"].Configuration["title"].ShouldBe("Factory events");
        layout.Bindings["eventCounterCopy"].PrimaryMetric.ShouldBe("ops.eventCounterMetric");
        layout.Cells.ShouldContain(cell => cell.Widget == "eventCounterCopy" && cell.Column == 1);
    }
}
