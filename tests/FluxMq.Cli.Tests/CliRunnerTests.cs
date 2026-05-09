using FluentAssertions;
using FluxMq.Cli;

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

        exitCode.Should().Be((int)CliExitCode.Success);
        output.Lines.Should().ContainSingle(line => line.Contains("Flow application is valid."));
        error.Lines.Should().BeEmpty();
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

        exitCode.Should().Be((int)CliExitCode.ValidationError);
        output.Lines.Should().BeEmpty();
        error.Lines.Should().Contain(line => line.Contains("Flow application is invalid."));
        error.Lines.Should().Contain(line => line.Contains("boundedCapacity"));
    }

    [Fact]
    public void Run_ReturnsUsageErrorWhenConfigurationFileIsMissing()
    {
        var output = new TestOutput();
        var error = new TestOutput();
        var runner = new CliRunner(output, error);

        var exitCode = runner.Run(["validate", "--config", Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")]);

        exitCode.Should().Be((int)CliExitCode.UsageError);
        output.Lines.Should().BeEmpty();
        error.Lines.Should().Contain(line => line.Contains("Configuration file was not found"));
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
