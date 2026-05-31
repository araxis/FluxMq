using Shouldly;
using FluxFlow.Engine.Components;
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

        var runTask = CreateRunner().RunAsync("roundTrip", scenario, events);

        events.Post(Event(
            FlowEventTypes.MqttMessageReceived,
            topic: "factory/line-a/temperature",
            status: "received"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.Passed);
        step.MatchedEvent.ShouldNotBeNull();
        step.MatchedEvent.Channel.ShouldBe("factory/line-a/temperature");
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

        var runTask = CreateRunner().RunAsync("schema", scenario, events);

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
        result.Steps[0].MatchedEvent!.Channel.ShouldBe("factory/line-a/temperature");
        result.Steps[0].MatchedEvent!.Status.ShouldBe("valid");
    }

    [Fact]
    public async Task RunAsync_MatchesMqttQosAndRetainAttributes()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectPublished"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessagePublished),
                    ("topicStartsWith", "test"),
                    ("attributes", new Dictionary<string, object>
                    {
                        ["qos"] = 1,
                        ["retain"] = false
                    }),
                    ("timeoutMs", 1000))
            }
        };

        var runTask = CreateRunner().RunAsync("mqttAttributes", scenario, events);
        events.Post(Event(
            FlowEventTypes.MqttMessagePublished,
            topic: "test",
            attributes: new Dictionary<string, string>
            {
                ["qos"] = "1",
                ["retain"] = "True"
            }));
        events.Post(Event(
            FlowEventTypes.MqttMessagePublished,
            topic: "test",
            attributes: new Dictionary<string, string>
            {
                ["qos"] = "1",
                ["retain"] = "False"
            }));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Steps[0].MatchedEvent!.GetAttribute("retain").ShouldBe("False");
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

        var result = await CreateRunner()
            .RunAsync("roundTrip", scenario, events)
            .WaitAsync(TimeSpan.FromSeconds(2));

        result.Status.ShouldBe(ScenarioRunStatus.Failed);
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.TimedOut);
        var message = step.Message.ShouldNotBeNull();
        message.ShouldContain("Expected event was not observed");
        message.ShouldContain("Observed 0 events while waiting.");
    }

    [Fact]
    public async Task RunAsync_ReturnsCanceledWhenExpectationWaitIsCanceled()
    {
        var events = new BufferBlock<FlowEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectResponse"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "factory/response/"),
                    ("timeoutMs", 10000))
            }
        };

        var result = await CreateRunner()
            .RunAsync("roundTrip", scenario, events, cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(2));

        result.Status.ShouldBe(ScenarioRunStatus.Canceled);
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.Canceled);
        step.Message.ShouldBe("Scenario step was canceled.");
    }

    [Fact]
    public async Task RunAsync_IgnoresBroadcastReplayFromBeforeScenarioStarted()
    {
        var events = new BroadcastBlock<FlowEvent>(static flowEvent => flowEvent);
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectPublished"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessagePublished),
                    ("topicStartsWith", "test"),
                    ("status", "published"),
                    ("payloadContains", "\"value\":12"),
                    ("timeoutMs", 20))
            }
        };

        events.Post(Event(
            FlowEventTypes.MqttMessagePublished,
            topic: "test",
            status: "published",
            payloadPreview: """{"value":12}""",
            timestamp: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var result = await CreateRunner()
            .RunAsync("roundTrip", scenario, events)
            .WaitAsync(TimeSpan.FromSeconds(2));

        result.Status.ShouldBe(ScenarioRunStatus.Failed);
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.TimedOut);
        step.Message.ShouldNotBeNull().ShouldContain("Observed 0 events while waiting.");
    }

    [Fact]
    public async Task RunAsync_DescribesObservedNonMatchingEventsOnTimeout()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectPublished"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessagePublished),
                    ("topicStartsWith", "test"),
                    ("status", "published"),
                    ("payloadContains", "\"value\":12"),
                    ("timeoutMs", 50))
            }
        };

        var runTask = CreateRunner().RunAsync("roundTrip", scenario, events);

        events.Post(Event(
            FlowEventTypes.MqttMessageReceived,
            topic: "fluxmq/sample",
            status: "received",
            payloadPreview: "1"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.TimedOut);
        var message = step.Message.ShouldNotBeNull();
        message.ShouldContain("Observed while waiting: mqtt.message.received");
        message.ShouldContain("topic 'fluxmq/sample'");
        message.ShouldContain("payload '1'");
        message.ShouldContain("A mqtt.message.published expectation must match a scenario mqtt.publisher event or a running app MQTT publisher node event.");
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

        var runTask = CreateRunner().RunAsync("ordered", scenario, events);
        events.Post(Event(FlowEventTypes.MqttMessageReceived));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.Status.ShouldBe(ScenarioRunStatus.Failed);
        result.Steps.Count.ShouldBe(2);
        result.Steps[0].Status.ShouldBe(ScenarioStepRunStatus.Passed);
        result.Steps[1].Status.ShouldBe(ScenarioStepRunStatus.TimedOut);
    }

    [Fact]
    public async Task RunAsync_ContinuesAfterWhenEventMatches()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["whenReady"] = WhenEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "factory/ready"),
                    ("timeoutMs", 1000)),
                ["expectPublish"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessagePublished),
                    ("topicStartsWith", "factory/result"),
                    ("timeoutMs", 1000))
            }
        };

        var runTask = CreateRunner().RunAsync("conditional", scenario, events);
        events.Post(Event(FlowEventTypes.MqttMessageReceived, topic: "factory/ready"));
        events.Post(Event(FlowEventTypes.MqttMessagePublished, topic: "factory/result"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Steps.Count.ShouldBe(2);
        result.Steps[0].Status.ShouldBe(ScenarioStepRunStatus.Passed);
        result.Steps[1].Status.ShouldBe(ScenarioStepRunStatus.Passed);
    }

    [Fact]
    public async Task RunAsync_SkipsRemainingStepsWhenWhenEventDoesNotMatch()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["whenReady"] = WhenEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "factory/ready"),
                    ("timeoutMs", 20)),
                ["expectPublish"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessagePublished),
                    ("topicStartsWith", "factory/result"),
                    ("timeoutMs", 1000))
            }
        };

        var runTask = CreateRunner()
            .RunAsync("conditional", scenario, events);
        events.Post(Event(FlowEventTypes.MqttMessageReceived, topic: "factory/other"));

        var result = await runTask
            .WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(ScenarioRunStatus.Passed);
        var step = result.Steps.ShouldHaveSingleItem();
        step.Status.ShouldBe(ScenarioStepRunStatus.Skipped);
        step.Message.ShouldNotBeNull().ShouldContain("Remaining steps were skipped.");
        step.Message.ShouldContain("Observed while waiting: mqtt.message.received");
    }

    [Fact]
    public async Task RunAsync_DoesNotSkipUnmatchedEventsRecordedBeforePreviousMatch()
    {
        var events = new BufferBlock<FlowEvent>();
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["expectReceive"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "fluxmq/sample"),
                    ("status", "received"),
                    ("payloadContains", "\"value\":12"),
                    ("timeoutMs", 1000)),
                ["expectPublish"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessagePublished),
                    ("topicStartsWith", "test"),
                    ("status", "published"),
                    ("payloadContains", "\"value\":12"),
                    ("timeoutMs", 1000))
            }
        };

        var runTask = CreateRunner().RunAsync("mappedPublish", scenario, events);
        events.Post(Event(
            FlowEventTypes.MqttMessagePublished,
            topic: "test",
            status: "published",
            payloadPreview: """{"value":12}"""));
        events.Post(Event(
            FlowEventTypes.MqttMessageReceived,
            topic: "fluxmq/sample/request",
            status: "received",
            payloadPreview: """{"value":12}"""));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Steps[0].MatchedEvent!.Type.ShouldBe(FlowEventTypes.MqttMessageReceived);
        result.Steps[1].MatchedEvent!.Type.ShouldBe(FlowEventTypes.MqttMessagePublished);
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

        var result = await CreateRunner().RunAsync("broken", scenario, events);

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

    [Fact]
    public async Task RunAsync_AllowsStepRunnersToAppendEventsForLaterExpectations()
    {
        var events = new BufferBlock<FlowEvent>();
        var registry = ScenarioStepRunnerRegistry.CreateEventExpectationOnly()
            .Register(new AppendScenarioEventStepRunner());
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["trigger"] = new ScenarioStepDefinition { Type = AppendScenarioEventStepRunner.StepType },
                ["expect"] = ExpectEvent(
                    ("eventType", FlowEventTypes.MqttMessageReceived),
                    ("topicStartsWith", "runner/topic"),
                    ("status", "received"),
                    ("timeoutMs", 1000))
            }
        };

        var result = await new ScenarioRunner(registry)
            .RunAsync("runnerEvents", scenario, events)
            .WaitAsync(TimeSpan.FromSeconds(2));

        result.IsSuccess.ShouldBeTrue();
        result.Steps.Count.ShouldBe(2);
        result.Steps[1].MatchedEvent!.Source.ShouldBe("AppendScenarioEventStepRunner");
        result.Steps[1].MatchedEvent!.Channel.ShouldBe("runner/topic/value");
    }

    [Fact]
    public async Task RunAsync_DisposesRegisteredLifetimeResourcesAfterPassedRun()
    {
        var events = new BufferBlock<FlowEvent>();
        var resource = new TestLifetimeResource();
        var registry = new ScenarioStepRunnerRegistry()
            .Register(new RegisterLifetimeResourceStepRunner(resource));
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["register"] = new ScenarioStepDefinition { Type = RegisterLifetimeResourceStepRunner.StepType }
            }
        };

        var result = await new ScenarioRunner(registry)
            .RunAsync("lifetime", scenario, events);

        result.IsSuccess.ShouldBeTrue();
        resource.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_DisposesRegisteredLifetimeResourcesAfterFailedRun()
    {
        var events = new BufferBlock<FlowEvent>();
        var resource = new TestLifetimeResource();
        var registry = new ScenarioStepRunnerRegistry()
            .Register(new RegisterLifetimeResourceStepRunner(resource));
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["register"] = new ScenarioStepDefinition { Type = RegisterLifetimeResourceStepRunner.StepType },
                ["unknown"] = new ScenarioStepDefinition { Type = "unknown.step" }
            }
        };

        var result = await new ScenarioRunner(registry)
            .RunAsync("lifetime", scenario, events);

        result.IsSuccess.ShouldBeFalse();
        resource.DisposeCount.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ReportsCleanupFailureAsScenarioFailure()
    {
        var events = new BufferBlock<FlowEvent>();
        var registry = new ScenarioStepRunnerRegistry()
            .Register(new RegisterLifetimeResourceStepRunner(new TestLifetimeResource(failDispose: true)));
        var scenario = new ScenarioDefinition
        {
            Steps =
            {
                ["register"] = new ScenarioStepDefinition { Type = RegisterLifetimeResourceStepRunner.StepType }
            }
        };

        var result = await new ScenarioRunner(registry)
            .RunAsync("lifetime", scenario, events);

        result.IsSuccess.ShouldBeFalse();
        result.Steps.Count.ShouldBe(2);
        var cleanup = result.Steps[1];
        cleanup.Name.ShouldBe("cleanup");
        cleanup.Type.ShouldBe("scenario.cleanup");
        cleanup.Status.ShouldBe(ScenarioStepRunStatus.Failed);
        cleanup.Message.ShouldNotBeNull().ShouldContain("cleanup boom");
    }

    private static ScenarioRunner CreateRunner()
        => new(ScenarioStepRunnerRegistry.CreateEventExpectationOnly());

    private static ScenarioStepDefinition ExpectEvent(params (string Key, object Value)[] values)
        => EventStep(ExpectEventScenarioStepRunner.StepType, values);

    private static ScenarioStepDefinition WhenEvent(params (string Key, object Value)[] values)
        => EventStep(WhenEventScenarioStepRunner.StepType, values);

    private static ScenarioStepDefinition EventStep(string stepType, params (string Key, object Value)[] values)
    {
        var configuration = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            configuration[key] = JsonSerializer.SerializeToElement(value);
        }

        return new ScenarioStepDefinition
        {
            Type = stepType,
            Configuration = configuration
        };
    }

    private static FlowEvent Event(
        string type,
        string? topic = null,
        string? status = null,
        string? subject = null,
        string? payloadPreview = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        DateTimeOffset? timestamp = null)
        => new()
        {
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Type = type,
            Source = "test",
            Channel = topic,
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

    private sealed class AppendScenarioEventStepRunner : IScenarioStepRunner
    {
        public const string StepType = "test.append-event";

        public string Type => StepType;

        public Task<ScenarioStepResult> RunAsync(
            ScenarioStepRunContext context,
            CancellationToken cancellationToken = default)
        {
            context.Events.Append(new FlowEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = FlowEventTypes.MqttMessageReceived,
                Source = nameof(AppendScenarioEventStepRunner),
                Channel = "runner/topic/value",
                Subject = "runner/topic/value",
                Status = "received",
                PayloadPreview = "42",
                PayloadBytes = 2
            });

            return Task.FromResult(new ScenarioStepResult
            {
                Name = context.StepName,
                Type = context.Step.Type,
                Status = ScenarioStepRunStatus.Passed,
                StartedAt = DateTimeOffset.UtcNow,
                FinishedAt = DateTimeOffset.UtcNow,
                Message = "Appended runner event.",
                NextEventOffset = context.EventOffset
            });
        }
    }

    private sealed class RegisterLifetimeResourceStepRunner(TestLifetimeResource resource) : IScenarioStepRunner
    {
        public const string StepType = "test.register-lifetime";

        public string Type => StepType;

        public Task<ScenarioStepResult> RunAsync(
            ScenarioStepRunContext context,
            CancellationToken cancellationToken = default)
        {
            context.Lifetime.Register(resource);
            return Task.FromResult(new ScenarioStepResult
            {
                Name = context.StepName,
                Type = context.Step.Type,
                Status = ScenarioStepRunStatus.Passed,
                StartedAt = DateTimeOffset.UtcNow,
                FinishedAt = DateTimeOffset.UtcNow,
                Message = "Registered lifetime resource.",
                NextEventOffset = context.EventOffset
            });
        }
    }

    private sealed class TestLifetimeResource(bool failDispose = false) : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            if (failDispose)
            {
                throw new InvalidOperationException("cleanup boom");
            }

            return ValueTask.CompletedTask;
        }
    }
}
