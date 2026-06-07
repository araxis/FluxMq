using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FluxMq.Cli.Commands;

public sealed class ValidateCliCommand(CliRunner runner) : AsyncCommand<ValidateCliCommand.Settings>
{
    private readonly CliRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        => Task.FromResult(_runner.Validate(settings.ToOptions(CliOptions.ValidateCommand)));

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

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ConfigurationPath))
            {
                return ValidationResult.Error("Configuration file is required. Use --config <path>.");
            }

            if (!TryParseOutputFormat(Output, out _))
            {
                return ValidationResult.Error($"Output format '{Output}' is not supported. Use 'text' or 'json'.");
            }

            return ValidationResult.Success();
        }

        public CliOptions ToOptions(string command)
        {
            _ = TryParseOutputFormat(Output, out var format);
            return new CliOptions(command, ConfigurationPath, SectionName, format, null);
        }
    }

    internal static bool TryParseOutputFormat(string value, out CliOutputFormat outputFormat)
    {
        if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = CliOutputFormat.Text;
            return true;
        }

        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            outputFormat = CliOutputFormat.Json;
            return true;
        }

        outputFormat = CliOutputFormat.Text;
        return false;
    }
}
