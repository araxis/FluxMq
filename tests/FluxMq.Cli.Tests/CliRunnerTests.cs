using Shouldly;
using FluxMq.Cli;
using System.Text.Json;

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
                        "type": "mqtt.metrics-sink"
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
                        "type": "mqtt.metrics-sink"
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
        document.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString().ShouldContain("boundedCapacity");
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
                        "type": "mqtt.metrics-sink"
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
                        "type": "mqtt.metrics-sink"
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
        document.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString().ShouldContain("boundedCapacity");
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
}
