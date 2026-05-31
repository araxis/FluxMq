using Shouldly;
using FluxMq.App.Scenarios;
using FluxMq.Cli;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using MQTTnet.Protocol;
using System.Text.Json;
using System.Threading.Channels;

namespace FluxMq.Cli.Tests;

public sealed class CliRunnerTests
{
    [Fact]
    public void Run_ReturnsSuccessForValidFlowConfiguration()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = runner.Run(["validate", "--config", temp.Path]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        output.Lines.ShouldContain(line => line.Contains("Flow application is valid."));
        error.Lines.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ReturnsValidationErrorForInvalidFlowConfiguration()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "inspect": {
                        "type": "mqtt.payload-inspector",
                        "configuration": {
                          "boundedCapacity": 0
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = runner.Run(["validate", "--config", temp.Path]);

        exitCode.ShouldBe((int)CliExitCode.ValidationError);
        output.Lines.ShouldBeEmpty();
        error.Lines.ShouldContain(line => line.Contains("Flow application is invalid."));
        error.Lines.ShouldContain(line => line.Contains("boundedCapacity"));
    }

    [Fact]
    public void Run_WritesJsonToStdoutForValidFlowConfiguration()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = runner.Run(["validate", "--config", temp.Path, "--output", "json"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();

        using var document = JsonDocument.Parse(string.Join(Environment.NewLine, output.Lines));
        document.RootElement.GetProperty("isValid").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("workflowCount").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("resourceCount").GetInt32().ShouldBe(0);
        document.RootElement.GetProperty("diagnostics").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Run_WritesJsonToStdoutForInvalidFlowConfiguration()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "inspect": {
                        "type": "mqtt.payload-inspector",
                        "configuration": {
                          "boundedCapacity": 0
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = runner.Run(["validate", "--config", temp.Path, "--output", "json"]);

        exitCode.ShouldBe((int)CliExitCode.ValidationError);
        error.Lines.ShouldBeEmpty();

        using var document = JsonDocument.Parse(string.Join(Environment.NewLine, output.Lines));
        document.RootElement.GetProperty("isValid").GetBoolean().ShouldBeFalse();
        var message = document.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString();
        message.ShouldNotBeNull();
        message.ShouldContain("boundedCapacity");
    }

    [Fact]
    public void Run_ReturnsUsageErrorWhenConfigurationFileIsMissing()
    {
        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = runner.Run(["validate", "--config", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")]);

        exitCode.ShouldBe((int)CliExitCode.UsageError);
        output.Lines.ShouldBeEmpty();
        error.Lines.ShouldContain(line => line.Contains("Configuration file was not found"));
    }

    [Fact]
    public async Task RunAsync_StartsAndStopsValidFlowConfiguration()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["run", "--config", temp.Path, "--duration-ms", "1"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();
        output.Lines.ShouldContain(line => line.Contains("Flow application is running."));
        output.Lines.ShouldContain(line => line.Contains("Flow application stopped."));
    }

    [Fact]
    public async Task RunAsync_WritesJsonToStdoutForRunCommand()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "metrics": {
                        "type": "mqtt.metrics"
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["run", "--config", temp.Path, "--duration-ms", "1", "--output", "json"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();

        using var document = JsonDocument.Parse(string.Join(Environment.NewLine, output.Lines));
        document.RootElement.GetProperty("started").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("workflowCount").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("resourceCount").GetInt32().ShouldBe(0);
        document.RootElement.GetProperty("exitReason").GetString().ShouldBe("duration elapsed");
        document.RootElement.GetProperty("hostState").GetString().ShouldBe("Stopped");
    }

    [Fact]
    public async Task RunAsync_WritesRunJsonShapeWhenStartValidationFails()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "inspect": {
                        "type": "mqtt.payload-inspector",
                        "configuration": {
                          "boundedCapacity": 0
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["run", "--config", temp.Path, "--duration-ms", "1", "--output", "json"]);

        exitCode.ShouldBe((int)CliExitCode.ValidationError);
        error.Lines.ShouldBeEmpty();

        using var document = JsonDocument.Parse(string.Join(Environment.NewLine, output.Lines));
        document.RootElement.GetProperty("started").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("exitReason").GetString().ShouldBe("validation failed");
        var message = document.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString();
        message.ShouldNotBeNull();
        message.ShouldContain("boundedCapacity");
    }

    [Fact]
    public async Task RunAsync_RunsScenarioByName()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "source": {
                        "type": "generated.source"
                      }
                    }
                  },
                  "tests": {
                    "smoke": {
                      "steps": {}
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "smoke"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();
        output.Lines.ShouldContain(line => line.Contains("Scenario 'smoke' passed."));
    }

    [Fact]
    public async Task RunAsync_ReturnsValidationErrorWhenCliScenarioRequiresRuntimeEvents()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "tests": {
                    "roundTrip": {
                      "steps": {
                        "expect": {
                          "type": "expect.event",
                          "configuration": {
                            "eventType": "mqtt.message.published",
                            "topicStartsWith": "test"
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "roundTrip"]);

        exitCode.ShouldBe((int)CliExitCode.ValidationError);
        output.Lines.ShouldBeEmpty();
        error.Lines.ShouldContain(line =>
            line.Contains("contains event-observing steps before any scenario event source", StringComparison.Ordinal) &&
            line.Contains("Add a scenario mqtt.publisher or mqtt.trigger step before expect.event or when.event", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ReturnsValidationErrorWhenCliScenarioWhenEventRequiresRuntimeEvents()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "tests": {
                    "conditional": {
                      "steps": {
                        "when": {
                          "type": "when.event",
                          "configuration": {
                            "eventType": "mqtt.message.published",
                            "topicStartsWith": "test",
                            "timeoutMs": 100
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "conditional"]);

        exitCode.ShouldBe((int)CliExitCode.ValidationError);
        output.Lines.ShouldBeEmpty();
        error.Lines.ShouldContain(line =>
            line.Contains("contains event-observing steps before any scenario event source", StringComparison.Ordinal) &&
            line.Contains("Add a scenario mqtt.publisher or mqtt.trigger step before expect.event or when.event", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_RunsScenarioWithRunnerOwnedMqttPublisherAndExpectation()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "resources": {
                    "local-broker": {
                      "type": "mqtt.connection",
                      "configuration": {
                        "profile": {
                          "name": "local-broker",
                          "host": "localhost",
                          "port": 1883
                        }
                      }
                    }
                  },
                  "tests": {
                    "publishOnly": {
                      "steps": {
                        "publish": {
                          "type": "mqtt.publisher",
                          "configuration": {
                            "connection": "local-broker",
                            "topic": "test",
                            "payload": { "hello": "fluxmq" },
                            "payloadEncoding": "json",
                            "qos": 1,
                            "retain": false
                          }
                        },
                        "expect": {
                          "type": "expect.event",
                          "configuration": {
                            "eventType": "mqtt.message.published",
                            "topicStartsWith": "test",
                            "status": "published",
                            "payloadContains": "\"hello\":\"fluxmq\"",
                            "timeoutMs": 1000
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var client = new FakeMqttBrokerClient();
        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error, _ => new FakeScenarioClientFactory(client));

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "publishOnly"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();
        output.Lines.ShouldContain(line => line.Contains("Scenario 'publishOnly' passed."));
        client.PublishCalls.ShouldBe(1);
        client.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_RunsScenarioWithRunnerOwnedMqttPublisherAndWhenEvent()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "resources": {
                    "local-broker": {
                      "type": "mqtt.connection",
                      "configuration": {
                        "profile": {
                          "name": "local-broker",
                          "host": "localhost",
                          "port": 1883
                        }
                      }
                    }
                  },
                  "tests": {
                    "conditionalPublish": {
                      "steps": {
                        "publish": {
                          "type": "mqtt.publisher",
                          "configuration": {
                            "connection": "local-broker",
                            "topic": "test",
                            "payload": { "hello": "fluxmq" },
                            "payloadEncoding": "json",
                            "qos": 1,
                            "retain": false
                          }
                        },
                        "when": {
                          "type": "when.event",
                          "configuration": {
                            "eventType": "mqtt.message.published",
                            "topicStartsWith": "test",
                            "status": "published",
                            "payloadContains": "\"hello\":\"fluxmq\"",
                            "timeoutMs": 1000
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var client = new FakeMqttBrokerClient();
        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error, _ => new FakeScenarioClientFactory(client));

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "conditionalPublish"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();
        output.Lines.ShouldContain(line => line.Contains("Scenario 'conditionalPublish' passed."));
        output.Lines.ShouldContain(line => line.Contains("- when [when.event] Passed"));
        client.PublishCalls.ShouldBe(1);
        client.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_SkipsRemainingCliScenarioStepsWhenWhenEventDoesNotMatch()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "resources": {
                    "local-broker": {
                      "type": "mqtt.connection",
                      "configuration": {
                        "profile": {
                          "name": "local-broker",
                          "host": "localhost",
                          "port": 1883
                        }
                      }
                    }
                  },
                  "tests": {
                    "conditionalPublish": {
                      "steps": {
                        "publish": {
                          "type": "mqtt.publisher",
                          "configuration": {
                            "connection": "local-broker",
                            "topic": "test",
                            "payload": { "hello": "fluxmq" },
                            "payloadEncoding": "json"
                          }
                        },
                        "when": {
                          "type": "when.event",
                          "configuration": {
                            "eventType": "mqtt.message.published",
                            "topicStartsWith": "other",
                            "status": "published",
                            "timeoutMs": 1
                          }
                        },
                        "afterSkip": {
                          "type": "mqtt.publisher",
                          "configuration": {
                            "connection": "local-broker",
                            "topic": "after/skip",
                            "payload": "should-not-publish"
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var client = new FakeMqttBrokerClient();
        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error, _ => new FakeScenarioClientFactory(client));

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "conditionalPublish"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();
        output.Lines.ShouldContain(line => line.Contains("Scenario 'conditionalPublish' passed."));
        output.Lines.ShouldContain(line => line.Contains("- when [when.event] Skipped"));
        output.Lines.ShouldNotContain(line => line.Contains("afterSkip", StringComparison.Ordinal));
        client.PublishCalls.ShouldBe(1);
        client.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_RunsScenarioWithRunnerOwnedMqttTriggerAndExpectation()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "resources": {
                    "local-broker": {
                      "type": "mqtt.connection",
                      "configuration": {
                        "profile": {
                          "name": "local-broker",
                          "host": "localhost",
                          "port": 1883
                        }
                      }
                    }
                  },
                  "tests": {
                    "roundTrip": {
                      "steps": {
                        "trigger": {
                          "type": "mqtt.trigger",
                          "configuration": {
                            "connection": "local-broker",
                            "subscriptions": "sample/#",
                            "qos": 1
                          }
                        },
                        "expect": {
                          "type": "expect.event",
                          "configuration": {
                            "eventType": "mqtt.message.received",
                            "topicStartsWith": "sample/",
                            "status": "received",
                            "payloadContains": "hello",
                            "timeoutMs": 1000
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var client = new FakeMqttBrokerClient();
        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error, _ => new FakeScenarioClientFactory(client));

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "roundTrip"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();
        output.Lines.ShouldContain(line => line.Contains("Scenario 'roundTrip' passed."));
        client.SubscribeCalls.ShouldBe(1);
        client.DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ReturnsScenarioFailedWhenScenarioStepFails()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "source": {
                        "type": "generated.source"
                      }
                    }
                  },
                  "tests": {
                    "broken": {
                      "steps": {
                        "unknown": {
                          "type": "unknown.step"
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "broken"]);

        exitCode.ShouldBe((int)CliExitCode.ScenarioFailed);
        output.Lines.ShouldBeEmpty();
        error.Lines.ShouldContain(line => line.Contains("Scenario 'broken' failed."));
        error.Lines.ShouldContain(line => line.Contains("unknown.step"));
    }

    [Fact]
    public async Task RunAsync_WritesJsonToStdoutForScenarioCommand()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "source": {
                        "type": "generated.source"
                      }
                    }
                  },
                  "tests": {
                    "smoke": {
                      "steps": {}
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "smoke", "--output", "json"]);

        exitCode.ShouldBe((int)CliExitCode.Success);
        error.Lines.ShouldBeEmpty();

        using var document = JsonDocument.Parse(string.Join(Environment.NewLine, output.Lines));
        document.RootElement.GetProperty("name").GetString().ShouldBe("smoke");
        document.RootElement.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("status").GetString().ShouldBe("Passed");
        document.RootElement.GetProperty("steps").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ReturnsValidationErrorWhenScenarioDoesNotExist()
    {
        using var temp = TemporaryJsonFile(
            """
            {
              "FluxMq": {
                "FlowApplication": {
                  "workflows": {
                    "observe": {
                      "source": {
                        "type": "generated.source"
                      }
                    }
                  }
                }
              }
            }
            """);

        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = await runner.RunAsync(["scenario", "--config", temp.Path, "--name", "missing"]);

        exitCode.ShouldBe((int)CliExitCode.ValidationError);
        output.Lines.ShouldBeEmpty();
        error.Lines.ShouldContain(line => line.Contains("Scenario 'missing' does not exist."));
    }

    private static TemporaryFile TemporaryJsonFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        return new TemporaryFile(path);
    }

    private sealed class TestOutput : ICliOutput
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines => _lines;

        public void WriteLine(string message) => _lines.Add(message);
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    private sealed class FakeScenarioClientFactory(FakeMqttBrokerClient client) : IMqttScenarioClientFactory
    {
        public IMqttBrokerClient CreateClient(string connectionName) => client;
    }

    private sealed class FakeMqttBrokerClient : IMqttBrokerClient
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();

        public MqttConnectionProfile Profile { get; } = new() { Name = "local-broker" };
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int SubscribeCalls { get; private set; }
        public int PublishCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public event EventHandler<MqttClientState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Disconnected;
            StateChanged?.Invoke(this, State);
            _messages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            CancellationToken ct = default)
            => SubscribeAsync(topicFilter, qos, receiveRetainedMessages: true, retainAsPublished: true, ct);

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos,
            bool receiveRetainedMessages,
            bool retainAsPublished = true,
            CancellationToken ct = default)
        {
            SubscribeCalls++;
            _ = Task.Run(async () =>
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
                await _messages.Writer.WriteAsync(new MqttEnvelope
                {
                    Topic = "sample/response",
                    Payload = "hello"u8.ToArray(),
                    QualityOfService = qos,
                    Retain = false
                }, ct).ConfigureAwait(false);
            }, ct);
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task PublishAsync(
            string topic,
            byte[] payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken ct = default)
        {
            PublishCalls++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
