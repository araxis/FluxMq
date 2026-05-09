namespace FluxMq.Cli;

public static class CliOptionsParser
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = new CliOptions("", null, CliOptions.DefaultSectionName, CliOutputFormat.Text);
        error = null;

        if (args.Length == 0)
        {
            error = "Command is required.";
            return false;
        }

        var command = args[0];
        if (command is "-h" or "--help")
        {
            options = options with { Command = command };
            return true;
        }

        if (!string.Equals(command, CliOptions.ValidateCommand, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Unknown command '{command}'.";
            return false;
        }

        string? configurationPath = null;
        var sectionName = CliOptions.DefaultSectionName;
        var outputFormat = CliOutputFormat.Text;

        for (var index = 1; index < args.Length; index++)
        {
            var current = args[index];
            switch (current)
            {
                case "-c":
                case "--config":
                    if (!TryReadValue(args, ref index, current, out configurationPath, out error))
                    {
                        return false;
                    }

                    break;

                case "-s":
                case "--section":
                    if (!TryReadValue(args, ref index, current, out sectionName, out error))
                    {
                        return false;
                    }

                    break;

                case "-o":
                case "--output":
                    if (!TryReadValue(args, ref index, current, out var outputValue, out error))
                    {
                        return false;
                    }

                    if (!TryParseOutputFormat(outputValue, out outputFormat))
                    {
                        error = $"Output format '{outputValue}' is not supported. Use 'text' or 'json'.";
                        return false;
                    }

                    break;

                default:
                    error = $"Unknown option '{current}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(configurationPath))
        {
            error = "Configuration file is required. Use --config <path>.";
            return false;
        }

        options = new CliOptions(command, configurationPath, sectionName, outputFormat);
        return true;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string optionName,
        out string value,
        out string? error)
    {
        value = "";
        error = null;

        if (index + 1 >= args.Length)
        {
            error = $"Option '{optionName}' requires a value.";
            return false;
        }

        value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"Option '{optionName}' requires a value.";
            return false;
        }

        return true;
    }

    private static bool TryParseOutputFormat(string value, out CliOutputFormat outputFormat)
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
