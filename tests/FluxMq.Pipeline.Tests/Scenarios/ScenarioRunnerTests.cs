using Shouldly;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Scenarios;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Scenarios;

public sealed class ScenarioRunnerTests
{
    [Fact]
    public async Task RunAsync_PassesExpectEventWhenMatchingEventArrives()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectResponse"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "factory/line-a/"),
                    ("status", "received"),
                    ("timeoutMs", 1000))
            }
        };

        var runTask = new ScenarioRunner().RunAsync("roundTrip", scenario, events);

        events.Post(Event(
            FlowEventTypes.MqttMessageReceived,
            topic: "factory/line-a/temperature",
            status: "received"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.Passed);
        step.MatchedEvent.ShouldNotBeNull();
        step.MatchedEvent.Topic.ShouldBe("factory/line-a/temperature");
    }

    [Fact]
    public async Task RunAsync_IgnoresNonMatchingEventsUntilMatchArrives()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectValidation"] = ExpectEvent(
                    ("eventType", FlowEventTypes.JsonSchemaValidated),
                    ("topicStartsWith", "factory/line-a/"),
                    ("status", "valid"),
                    ("payloadContains", "\"ok\""),
                    ("attributes", new Dictionary<string, string>
                    {
                        ["schema"] = "temperature"
                    }),
                    ("timeoutMs", 1000))
            }
        };

        var runTask = new ScenarioRunner().RunAsync("schema", scenario, events);

        events.Post(Event(
            FlowEventTypes.JsonSchemaValidated,
            topic: "factory/line-b/temperature",
            status: "valid",
            payloadPreview: """{"status":"ok"}""",
            attributes: new Dictionary<string, string> { ["schema"] = "temperature" }));
        events.Post(Event(
            FlowEventTypes.JsonSchemaValidated,
            topic: "factory/line-a/temperature",
            status: "invalid",
            payloadPreview: """{"status":"ok"}""",
            attributes: new Dictionary<string, string> { ["schema"] = "temperature" }));
        events.Post(Event(
            FlowEventTypes.JsonSchemaValidated,
            topic: "factory/line-a/temperature",
            status: "valid",
            payloadPreview: """{"status":"ok"}""",
            attributes: new Dictionary<string, string> { ["schema"] = "temperature" }));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Steps[0].MatchedEvent!.Topic.ShouldBe("factory/line-a/temperature");
        result.Steps[0].MatchedEvent!.Status.ShouldBe("valid");
    }

    [Fact]
    public async Task RunAsync_TimesOutWhenExpectedEventDoesNotArrive()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectResponse"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "factory/response/"),
                    ("timeoutMs", 20))
            }
        };

        var result = await new ScenarioRunner()
            .RunAsync("roundTrip", scenario, events)
            .WaitAsync(TimeSpan.FromSeconds(2));

        result.Status.ShouldBe(ScenarioRunStatus.Failed);
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.TimedOut);
        step.Message!.ShouldContain("Expected event was not observed");
    }

    [Fact]
    public async Task RunAsync_DoesNotReuseMatchedEventsForLaterExpectations()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["first"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("timeoutMs", 1000)),
                ["second"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("timeoutMs", 20))
            }
        };

        var runTask = new ScenarioRunner().RunAsync("ordered", scenario, events);
        events.Post(Event(FlowEventTypes.MqttMessageReceived));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.Status.ShouldBe(ScenarioRunStatus.Failed);
        result.Steps.Count.ShouldBe(2);
        result.Steps[0].Status.ShouldBe(ScenarioStepRunStatus.Passed);
        result.Steps[1].Status.ShouldBe(ScenarioStepRunStatus.TimedOut);
    }

    [Fact]
    public async Task RunAsync_FailsUnknownStepTypeWithClearMessage()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["unknown"] = new ScenarioStepDefinition { Type = "unknown.step" }
            }
        };

        var result = await new ScenarioRunner().RunAsync("broken", scenario, events);

        result.Status.ShouldBe(ScenarioRunStatus.Failed);
        result.Steps.ShouldHaveSingleItem()
            .Message.ShouldBe("Scenario step type 'unknown.step' is not registered.");
    }

    [Fact]
    public async Task RunAsync_PassesServicesToStepRunners()
    {
        var events = new BufferBlock<FlowEvent>();
        var registry = new ScenarioStepRunnerRegistry()
            .Register(new CaptureScenarioServiceStepRunner());
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["capture"] = new ScenarioStepDefinition { Type = CaptureScenarioServiceStepRunner.StepType }
            }
        };
        var services = ScenarioStepServices.Empty
            .Add(new CapturedScenarioService("ready"));

        var result = await new ScenarioRunner(registry)
            .RunAsync("services", scenario, events, services);

        result.IsSuccess.ShouldBeTrue();
        result.Steps.ShouldHaveSingleItem()
            .Message.ShouldBe("ready");
    }

    private static ScenarioStepDefinition ExpectEvent(params (string Key, object Value)[] values)
    {
        var configuration = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            configuration[key] = JsonSerializer.SerializeToElement(value);
        }

        return new ScenarioStepDefinition
        {
            Type = ExpectEventScenarioStepRunner.StepType,
            Configuration = configuration
        };
    }

    private static FlowEvent Event(
        string type,
        string? topic = null,
        string? status = null,
        string? subject = null,
        string? payloadPreview = null,
        IReadOnlyDictionary<string, string>? attributes = null)
        => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            Source = "test",
            Topic = topic,
            Subject = subject,
            Status = status,
            PayloadPreview = payloadPreview,
            Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };

    private sealed record CapturedScenarioService(string Value);

    private sealed class CaptureScenarioServiceStepRunner : IScenarioStepRunner
    {
        public const string StepType = "test.capture-service";

        public string Type => StepType;

        public Task<ScenarioStepResult> RunAsync(
            ScenarioStepRunContext context,
            CancellationToken cancellationToken = default)
        {
            var service = context.Services.GetRequired<CapturedScenarioService>();
            return Task.FromResult(new ScenarioStepResult
            {
                Name = context.StepName,
                Type = context.Step.Type,
                Status = ScenarioStepRunStatus.Passed,
                StartedAt = DateTimeOffset.UtcNow,
                FinishedAt = DateTimeOffset.UtcNow,
                Message = service.Value,
                NextEventOffset = context.EventOffset
            });
        }
    }
}
