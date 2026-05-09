using FluentAssertions;
using FluxMq.Cli;

namespace FluxMq.Cli.Tests;

public sealed class CliOptionsParserTests
{
    [Fact]
    public void TryParse_ReadsValidateOptions()
    {
        var parsed = CliOptionsParser.TryParse(
            ["validate", "--config", "flow.json", "--section", "Custom:Flow", "--output", "json"],
            out var options,
            out var error);

        parsed.Should().BeTrue();
        error.Should().BeNull();
        options.Command.Should().Be("validate");
        options.ConfigurationPath.Should().Be("flow.json");
        options.SectionName.Should().Be("Custom:Flow");
        options.OutputFormat.Should().Be(CliOutputFormat.Json);
        options.RunDuration.Should().BeNull();
    }

    [Fact]
    public void TryParse_ReadsRunOptions()
    {
        var parsed = CliOptionsParser.TryParse(
            ["run", "--config", "flow.json", "--duration-ms", "25", "--output", "json"],
            out var options,
            out var error);

        parsed.Should().BeTrue();
        error.Should().BeNull();
        options.Command.Should().Be("run");
        options.ConfigurationPath.Should().Be("flow.json");
        options.OutputFormat.Should().Be(CliOutputFormat.Json);
        options.RunDuration.Should().Be(TimeSpan.FromMilliseconds(25));
    }

    [Fact]
    public void TryParse_RequiresConfigurationPath()
    {
        var parsed = CliOptionsParser.TryParse(["validate"], out _, out var error);

        parsed.Should().BeFalse();
        error.Should().Contain("--config");
    }

    [Fact]
    public void TryParse_RejectsUnknownOutputFormat()
    {
        var parsed = CliOptionsParser.TryParse(["validate", "--config", "flow.json", "--output", "xml"], out _, out var error);

        parsed.Should().BeFalse();
        error.Should().Contain("Output format");
    }

    [Fact]
    public void TryParse_RejectsValidateDuration()
    {
        var parsed = CliOptionsParser.TryParse(["validate", "--config", "flow.json", "--duration-ms", "25"], out _, out var error);

        parsed.Should().BeFalse();
        error.Should().Contain("run command");
    }
}
