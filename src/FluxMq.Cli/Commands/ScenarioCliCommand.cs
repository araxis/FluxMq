using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace FluxMq.Cli.Commands;

public sealed class ScenarioCliCommand(CliRunner runner) : AsyncCommand<ScenarioCliCommand.Settings>
{
    private readonly CliRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _runner.RunScenario(settings.ToOptions(), cancellationToken);

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description("Path to the flow application configuration file.")]
        public string ConfigurationPath { get; init; } = "";

        [CommandOption("-n|--name <NAME>")]
        [Description("Scenario name to run.")]
        public string ScenarioName { get; init; } = "";

        [CommandOption("-s|--section <NAME>")]
        [DefaultValue(CliOptions.DefaultSectionName)]
        [Description("Configuration section that contains flow definitions.")]
        public string SectionName { get; init; } = CliOptions.DefaultSectionName;

        [CommandOption("-o|--output <FORMAT>")]
        [DefaultValue("text")]
        [Description("Output format: text or json.")]
        public string Output { get; init; } = "text";

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ConfigurationPath))
            {
                return ValidationResult.Error("Configuration file is required. Use --config <path>.");
            }

            if (string.IsNullOrWhiteSpace(ScenarioName))
            {
                return ValidationResult.Error("Scenario name is required. Use --name <name>.");
            }

            if (!ValidateCliCommand.TryParseOutputFormat(Output, out _))
            {
                return ValidationResult.Error($"Output format '{Output}' is not supported. Use 'text' or 'json'.");
            }

            return ValidationResult.Success();
        }

        public CliOptions ToOptions()
        {
            _ = ValidateCliCommand.TryParseOutputFormat(Output, out var format);
            return new CliOptions(
                CliOptions.ScenarioCommand,
                ConfigurationPath,
                SectionName,
                format,
                null,
                ScenarioName);
        }
    }
}
