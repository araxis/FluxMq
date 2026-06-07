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
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.PayloadDistributionType, "slot:0:0");

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
    public void AddDashboardWidget_WritesFocusedEventCounterWidgetAndMetricQuery()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        var widget = layout.Widgets["eventCounter"];
        var metricId = "ops.eventCounterMetric";
        var metric = layout.Metrics[metricId];

        widget.Configuration.Keys.ShouldBe(["title", "metric"], ignoreOrder: true);
        widget.Configuration["metric"].ShouldBe(metricId);
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
    public void UpdateDashboardMetric_WritesQueryShapeWithoutSchemaMigration()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventGaugeType, "slot:0:0");

        json = composer.UpdateDashboardMetric(
            json,
            "ops",
            "eventGaugeMetric",
            new DashboardMetricQueryDefinition(
                "runtimeEvents",
                "count",
                "300s",
                EventType: "mqtt.message.received",
                TopicStartsWith: "factory/",
                TopicNotStartsWith: "$SYS/",
                Status: "received",
                GroupBy: "topic",
                Format: "bytes",
                AdditionalFilters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1",
                    [DashboardEventFilterCatalog.AttributeFilterKey("retain")] = "false"
                }));

        var metric = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull().Metrics["ops.eventGaugeMetric"];
        metric.Source.ShouldBe("runtimeEvents");
        metric.Aggregation.ShouldBe("count");
        metric.Window.ShouldBe("300s");
        metric.GroupBy.ShouldBe("topic");
        metric.Filters[DashboardEventFilterCatalog.EventTypeKey].ShouldBe("mqtt.message.received");
        metric.Filters[DashboardEventFilterCatalog.TopicStartsWithKey].ShouldBe("factory/");
        metric.Filters[DashboardEventFilterCatalog.AttributeFilterKey("qos")].ShouldBe("1");
        metric.Filters[DashboardEventFilterCatalog.AttributeFilterKey("retain")].ShouldBe("false");
        metric.Format["unit"].ShouldBe("bytes");
    }

    [Fact]
    public void UpdateDashboardWidgetBinding_PreservesOrderedMetricSlots()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.StatusStripType, "slot:0:0");
        json = composer.UpdateDashboardMetric(json, "ops", "rate", new DashboardMetricQueryDefinition("runtimeEvents", "rate", "60s"));

        json = composer.UpdateDashboardWidgetBinding(
            json,
            "ops",
            "statusStrip",
            "rate",
            ["rate", "statusStripMetric"]);

        var binding = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull().Bindings["statusStrip"];
        binding.PrimaryMetric.ShouldBe("rate");
        binding.Metrics.ShouldBe(["rate", "statusStripMetric"]);
    }

    [Fact]
    public void RemoveDashboardMetricIfUnused_KeepsSharedMetricAndDeletesUnusedMetric()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");
        json = composer.UpdateDashboardMetric(json, "ops", "unused", new DashboardMetricQueryDefinition("runtimeEvents", "count", "60s"));

        json = composer.RemoveDashboardMetricIfUnused(json, "ops", "ops.eventCounterMetric");
        json = composer.RemoveDashboardMetricIfUnused(json, "ops", "unused");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        layout.Metrics.ContainsKey("ops.eventCounterMetric").ShouldBeTrue();
        layout.Metrics.ContainsKey("unused").ShouldBeFalse();
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
