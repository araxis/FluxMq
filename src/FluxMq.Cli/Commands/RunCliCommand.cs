using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace FluxMq.Cli.Commands;

public sealed class RunCliCommand(CliRunner runner) : AsyncCommand<RunCliCommand.Settings>
{
    private readonly CliRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => _runner.RunFlow(settings.ToOptions(), cancellationToken);

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-c|--config <PATH>")]
        [Description("Path to the flow application configuration file.")]
        public string ConfigurationPath { get; init; } = "";

        [CommandOption("-s|--section <NAME>")]
        [DefaultValue(CliOptions.DefaultSectionName)]
        [Description("Configuration section that contains flow definitions.")]
        public string SectionName { get; init; } = CliOptions.DefaultSectionName;

        [CommandOption("-o|--output <FORMAT>")]
        [DefaultValue("text")]
        [Description("Output format: text or json.")]
        public string Output { get; init; } = "text";

        [CommandOption("--duration-ms <MILLISECONDS>")]
        [Description("Run duration in milliseconds.")]
        public int? DurationMilliseconds { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ConfigurationPath))
            {
                return ValidationResult.Error("Configuration file is required. Use --config <path>.");
            }

            if (!ValidateCliCommand.TryParseOutputFormat(Output, out _))
            {
                return ValidationResult.Error($"Output format '{Output}' is not supported. Use 'text' or 'json'.");
            }

            if (DurationMilliseconds is <= 0)
            {
                return ValidationResult.Error("Duration must be a positive whole number of milliseconds.");
            }

            return ValidationResult.Success();
        }

        public CliOptions ToOptions()
        {
            _ = ValidateCliCommand.TryParseOutputFormat(Output, out var format);
            TimeSpan? duration = DurationMilliseconds is null ? null : TimeSpan.FromMilliseconds(DurationMilliseconds.Value);
            return new CliOptions(CliOptions.RunCommand, ConfigurationPath, SectionName, format, duration);
        }
    }
}
