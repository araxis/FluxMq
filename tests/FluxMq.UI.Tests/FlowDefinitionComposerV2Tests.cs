using FluxMq.Scenarios;
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
        var dashboard = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("dashboards")
            .GetProperty("ops");

        dashboard.GetProperty("version").GetInt32().ShouldBe(2);
        dashboard.GetProperty("responsive").GetProperty("breakpoints").TryGetProperty("desktop", out _).ShouldBeTrue();
        dashboard.GetProperty("view").GetProperty("mode").GetString().ShouldBe("design");
        dashboard.GetProperty("metrics").EnumerateObject().ShouldHaveSingleItem();
        dashboard.GetProperty("bindings").EnumerateObject().ShouldHaveSingleItem();
    }
}
