using FluxFlow.Engine.Components;
using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;
using System.Text.Json;

namespace FluxMq.UI.Tests;

public sealed class ScenarioRunReportFormatterTests
{
    [Fact]
    public void Report_WritesStableScenarioReportShape()
    {
        var started = new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero);
        var generatedAt = started.AddMinutes(5);
        var result = new ScenarioRunResult
        {
            Name = "roundTrip",
            Status = ScenarioRunStatus.Passed,
            StartedAt = started,
            FinishedAt = started.AddMilliseconds(250),
            Steps =
            [
                new ScenarioStepResult
                {
                    Name = "expectResponse",
                    Type = ScenarioStepTypes.ExpectEvent,
                    Status = ScenarioStepRunStatus.Passed,
                    StartedAt = started.AddMilliseconds(10),
                    FinishedAt = started.AddMilliseconds(240),
                    Message = "Matched event.",
                    MatchedEvent = new FlowEvent
                    {
                        Timestamp = started.AddMilliseconds(200),
                        Type = "mqtt.message.published",
                        Source = "mqtt.publisher",
                        Status = "published",
                        Channel = "fluxmq/sample/response",
                        PayloadBytes = 12,
                        PayloadPreview = """{"ok":true}""",
                        Attributes = new Dictionary<string, string>
                        {
                            ["schemaId"] = "response.v1"
                        }
                    }
                }
            ]
        };
        var scenario = new TestScenarioSnapshot(
            "roundTrip",
            [
                new ScenarioStepSnapshot(
                    "expectResponse",
                    ScenarioStepTypes.ExpectEvent,
                    new Dictionary<string, string>
                    {
                        ["eventType"] = "mqtt.message.published",
                        ["topicStartsWith"] = "fluxmq/sample/",
                        ["timeoutMs"] = "5000"
                    })
            ]);

        var report = ScenarioRunReportFormatter.Create(result, scenario, generatedAt);
        var json = ScenarioRunReportFormatter.ToJson(report);
        ScenarioRunReportFormatter.ToJson(result, scenario, generatedAt).ShouldBe(json);
        var laterJson = ScenarioRunReportFormatter.ToJson(result, scenario, generatedAt.AddSeconds(1));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetString().ShouldBe(ScenarioRunReportFormatter.CurrentSchemaVersion);
        root.GetProperty("runId").GetString().ShouldBe("roundtrip-20260526T100000000Z");
        root.GetProperty("generatedAt").GetDateTimeOffset().ShouldBe(generatedAt);
        root.GetProperty("name").GetString().ShouldBe("roundTrip");
        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("status").GetString().ShouldBe("Passed");
        root.GetProperty("durationMilliseconds").GetDouble().ShouldBe(250);
        var stepSummary = root.GetProperty("stepSummary");
        stepSummary.GetProperty("total").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("planned").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("executed").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("notRun").GetInt32().ShouldBe(0);
        stepSummary.GetProperty("passed").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("skipped").GetInt32().ShouldBe(0);
        stepSummary.GetProperty("failed").GetInt32().ShouldBe(0);
        stepSummary.GetProperty("timedOut").GetInt32().ShouldBe(0);
        stepSummary.GetProperty("canceled").GetInt32().ShouldBe(0);
        var plannedStep = root.GetProperty("plannedSteps")[0];
        plannedStep.GetProperty("sequence").GetInt32().ShouldBe(1);
        plannedStep.GetProperty("stepName").GetString().ShouldBe("expectResponse");
        plannedStep.GetProperty("stepType").GetString().ShouldBe(ScenarioStepTypes.ExpectEvent);
        plannedStep.GetProperty("configuration").GetProperty("timeoutMs").GetString().ShouldBe("5000");
        root.GetProperty("issues").GetArrayLength().ShouldBe(0);
        root.GetProperty("notRunSteps").GetArrayLength().ShouldBe(0);
        root.GetProperty("firstIssue").ValueKind.ShouldBe(JsonValueKind.Null);
        var step = root.GetProperty("steps")[0];
        step.GetProperty("name").GetString().ShouldBe("expectResponse");
        step.GetProperty("sequence").GetInt32().ShouldBe(1);
        step.GetProperty("status").GetString().ShouldBe("Passed");
        step.GetProperty("startedOffsetMilliseconds").GetDouble().ShouldBe(10);
        step.GetProperty("finishedOffsetMilliseconds").GetDouble().ShouldBe(240);
        step.GetProperty("durationMilliseconds").GetDouble().ShouldBe(230);
        var configuration = step.GetProperty("configuration");
        configuration.GetProperty("eventType").GetString().ShouldBe("mqtt.message.published");
        configuration.GetProperty("topicStartsWith").GetString().ShouldBe("fluxmq/sample/");
        configuration.GetProperty("timeoutMs").GetString().ShouldBe("5000");
        var matched = step.GetProperty("matchedEvent");
        matched.GetProperty("scenarioOffsetMilliseconds").GetDouble().ShouldBe(200);
        matched.GetProperty("stepOffsetMilliseconds").GetDouble().ShouldBe(190);
        matched.GetProperty("topic").GetString().ShouldBe("fluxmq/sample/response");
        matched.GetProperty("attributes").GetProperty("schemaId").GetString().ShouldBe("response.v1");
        using var laterDocument = JsonDocument.Parse(laterJson);
        laterDocument.RootElement.GetProperty("runId").GetString().ShouldBe(root.GetProperty("runId").GetString());
        laterDocument.RootElement.GetProperty("generatedAt").GetDateTimeOffset().ShouldBe(generatedAt.AddSeconds(1));

        var text = ScenarioRunReportFormatter.ToText(report);
        ScenarioRunReportFormatter.ToText(result, scenario, generatedAt).ShouldBe(text);
        text.ShouldContain(
            $"Report {ScenarioRunReportFormatter.CurrentSchemaVersion} run roundtrip-20260526T100000000Z generated {generatedAt:O}.");
        text.ShouldContain(
            "matched: mqtt.message.published / fluxmq/sample/response / published (+200 ms scenario, +190 ms step)");
        text.ShouldContain("""event: source=mqtt.publisher; subject=no subject; payloadBytes=12; payload={"ok":true}""");
        text.ShouldContain("attributes: schemaId=response.v1");
    }

    [Fact]
    public void ToText_WritesReadableScenarioSummary()
    {
        var started = DateTimeOffset.UtcNow;
        var result = new ScenarioRunResult
        {
            Name = "broken",
            Status = ScenarioRunStatus.Failed,
            StartedAt = started,
            FinishedAt = started.AddMilliseconds(10),
            Steps =
            [
                new ScenarioStepResult
                {
                    Name = "unknown",
                    Type = "unknown.step",
                    Status = ScenarioStepRunStatus.Failed,
                    StartedAt = started,
                    FinishedAt = started.AddMilliseconds(10),
                    Message = "Scenario step type 'unknown.step' is not registered."
                },
                new ScenarioStepResult
                {
                    Name = "expectLater",
                    Type = ScenarioStepTypes.ExpectEvent,
                    Status = ScenarioStepRunStatus.TimedOut,
                    StartedAt = started.AddMilliseconds(10),
                    FinishedAt = started.AddMilliseconds(510),
                    Message = "Timed out after 500 ms."
                }
            ]
        };
        var scenario = new TestScenarioSnapshot(
            "broken",
            [
                new ScenarioStepSnapshot(
                    "unknown",
                    "unknown.step",
                    new Dictionary<string, string>
                    {
                        ["timeoutMs"] = "250"
                    }),
                new ScenarioStepSnapshot(
                    "expectLater",
                    ScenarioStepTypes.ExpectEvent,
                    new Dictionary<string, string>
                    {
                        ["eventType"] = "mqtt.message.published",
                        ["timeoutMs"] = "500"
                    }),
                new ScenarioStepSnapshot(
                    "publishCleanup",
                    ScenarioStepTypes.MqttPublisher,
                    new Dictionary<string, string>
                    {
                        ["payload"] = """{"cleanup":true}""",
                        ["topic"] = "factory/cleanup"
                    })
            ]);

        var text = ScenarioRunReportFormatter.ToText(result, scenario);

        text.ShouldContain("Scenario 'broken' Failed.");
        text.ShouldContain("Steps: 3 planned, 2 run, 1 not run, 0 passed, 1 failed, 1 timed out.");
        text.ShouldContain("First issue: unknown [unknown.step] Failed: Scenario step type 'unknown.step' is not registered.");
        text.ShouldContain("Issues: 2.");
        text.ShouldContain("#1 unknown [unknown.step] Failed: Scenario step type 'unknown.step' is not registered.");
        text.ShouldContain("#2 expectLater [expect.event] TimedOut: Timed out after 500 ms.");
        text.ShouldContain("Not run: 1.");
        text.ShouldContain("#3 publishCleanup [mqtt.publisher]");
        text.ShouldContain("""config: payload={"cleanup":true}; topic=factory/cleanup""");
        text.ShouldContain("- unknown [unknown.step] Failed: Scenario step type 'unknown.step' is not registered.");
        text.ShouldContain("config: timeoutMs=250");
        text.ShouldContain("timing: #1, start +0 ms, finish +10 ms, duration 10 ms");

        var json = ScenarioRunReportFormatter.ToJson(result, scenario);
        using var document = JsonDocument.Parse(json);
        var stepSummary = document.RootElement.GetProperty("stepSummary");
        stepSummary.GetProperty("total").GetInt32().ShouldBe(2);
        stepSummary.GetProperty("planned").GetInt32().ShouldBe(3);
        stepSummary.GetProperty("executed").GetInt32().ShouldBe(2);
        stepSummary.GetProperty("notRun").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("failed").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("timedOut").GetInt32().ShouldBe(1);
        stepSummary.GetProperty("skipped").GetInt32().ShouldBe(0);
        var plannedSteps = document.RootElement.GetProperty("plannedSteps");
        plannedSteps.GetArrayLength().ShouldBe(3);
        plannedSteps[0].GetProperty("sequence").GetInt32().ShouldBe(1);
        plannedSteps[0].GetProperty("stepName").GetString().ShouldBe("unknown");
        plannedSteps[1].GetProperty("sequence").GetInt32().ShouldBe(2);
        plannedSteps[1].GetProperty("stepName").GetString().ShouldBe("expectLater");
        plannedSteps[1].GetProperty("configuration").GetProperty("eventType").GetString().ShouldBe("mqtt.message.published");
        plannedSteps[2].GetProperty("sequence").GetInt32().ShouldBe(3);
        plannedSteps[2].GetProperty("stepName").GetString().ShouldBe("publishCleanup");
        plannedSteps[2].GetProperty("stepType").GetString().ShouldBe(ScenarioStepTypes.MqttPublisher);
        plannedSteps[2].GetProperty("configuration").GetProperty("payload").GetString().ShouldBe("""{"cleanup":true}""");
        var firstIssue = document.RootElement.GetProperty("firstIssue");
        firstIssue.GetProperty("stepName").GetString().ShouldBe("unknown");
        firstIssue.GetProperty("stepType").GetString().ShouldBe("unknown.step");
        firstIssue.GetProperty("status").GetString().ShouldBe("Failed");
        firstIssue.GetProperty("message").GetString().ShouldBe("Scenario step type 'unknown.step' is not registered.");
        var issues = document.RootElement.GetProperty("issues");
        issues.GetArrayLength().ShouldBe(2);
        issues[0].GetProperty("sequence").GetInt32().ShouldBe(1);
        issues[0].GetProperty("stepName").GetString().ShouldBe("unknown");
        issues[1].GetProperty("sequence").GetInt32().ShouldBe(2);
        issues[1].GetProperty("stepName").GetString().ShouldBe("expectLater");
        issues[1].GetProperty("status").GetString().ShouldBe("TimedOut");
        var notRunSteps = document.RootElement.GetProperty("notRunSteps");
        notRunSteps.GetArrayLength().ShouldBe(1);
        notRunSteps[0].GetProperty("sequence").GetInt32().ShouldBe(3);
        notRunSteps[0].GetProperty("stepName").GetString().ShouldBe("publishCleanup");
        notRunSteps[0].GetProperty("stepType").GetString().ShouldBe(ScenarioStepTypes.MqttPublisher);
        var notRunConfiguration = notRunSteps[0].GetProperty("configuration");
        notRunConfiguration.GetProperty("payload").GetString().ShouldBe("""{"cleanup":true}""");
        notRunConfiguration.GetProperty("topic").GetString().ShouldBe("factory/cleanup");
    }

    [Fact]
    public void Report_TreatsSkippedWhenStepAsSuccessfulGuard()
    {
        var started = DateTimeOffset.UtcNow;
        var result = new ScenarioRunResult
        {
            Name = "conditional",
            Status = ScenarioRunStatus.Passed,
            StartedAt = started,
            FinishedAt = started.AddMilliseconds(25),
            Steps =
            [
                new ScenarioStepResult
                {
                    Name = "whenReady",
                    Type = ScenarioStepTypes.WhenEvent,
                    Status = ScenarioStepRunStatus.Skipped,
                    StartedAt = started,
                    FinishedAt = started.AddMilliseconds(25),
                    Message = "When condition was not met within 20 ms. Remaining steps were skipped."
                }
            ]
        };
        var scenario = new TestScenarioSnapshot(
            "conditional",
            [
                new ScenarioStepSnapshot(
                    "whenReady",
                    ScenarioStepTypes.WhenEvent,
                    new Dictionary<string, string>
                    {
                        ["eventType"] = "mqtt.message.received",
                        ["topicStartsWith"] = "factory/ready",
                        ["timeoutMs"] = "20"
                    }),
                new ScenarioStepSnapshot(
                    "expectPublish",
                    ScenarioStepTypes.ExpectEvent,
                    new Dictionary<string, string>
                    {
                        ["eventType"] = "mqtt.message.published",
                        ["topicStartsWith"] = "factory/result"
                    })
            ]);

        var json = ScenarioRunReportFormatter.ToJson(result, scenario);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        root.GetProperty("issues").GetArrayLength().ShouldBe(0);
        root.GetProperty("firstIssue").ValueKind.ShouldBe(JsonValueKind.Null);
        var summary = root.GetProperty("stepSummary");
        summary.GetProperty("planned").GetInt32().ShouldBe(2);
        summary.GetProperty("executed").GetInt32().ShouldBe(1);
        summary.GetProperty("notRun").GetInt32().ShouldBe(1);
        summary.GetProperty("skipped").GetInt32().ShouldBe(1);
        root.GetProperty("notRunSteps")[0].GetProperty("stepName").GetString().ShouldBe("expectPublish");

        var text = ScenarioRunReportFormatter.ToText(result, scenario);
        text.ShouldContain("Steps: 2 planned, 1 run, 1 not run, 0 passed, 1 skipped.");
        text.ShouldNotContain("First issue:");
        text.ShouldNotContain("Issues:");
    }
}
