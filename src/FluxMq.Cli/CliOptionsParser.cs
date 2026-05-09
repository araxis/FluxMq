namespace FluxMq.Cli;

public static class CliOptionsParser
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = new CliOptions("", null, CliOptions.DefaultSectionName, CliOutputFormat.Text, null);
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

        var isValidate = string.Equals(command, CliOptions.ValidateCommand, StringComparison.OrdinalIgnoreCase);
        var isRun = string.Equals(command, CliOptions.RunCommand, StringComparison.OrdinalIgnoreCase);
        if (!isValidate && !isRun)
        {
            error = $"Unknown command '{command}'.";
            return false;
        }

        string? configurationPath = null;
        var sectionName = CliOptions.DefaultSectionName;
        var outputFormat = CliOutputFormat.Text;
        TimeSpan? runDuration = null;

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

                case "--duration-ms":
                    if (!TryReadValue(args, ref index, current, out var durationValue, out error))
                    {
                        return false;
                    }

                    if (!TryParseDuration(durationValue, out runDuration))
                    {
                        error = $"Duration '{durationValue}' is not supported. Use a positive whole number of milliseconds.";
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

        if (runDuration is not null && !isRun)
        {
            error = "Option '--duration-ms' is only supported by the run command.";
            return false;
        }

        options = new CliOptions(command, configurationPath, sectionName, outputFormat, runDuration);
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

    private static bool TryParseDuration(string value, out TimeSpan? duration)
    {
        if (int.TryParse(value, out var milliseconds) && milliseconds > 0)
        {
            duration = TimeSpan.FromMilliseconds(milliseconds);
            return true;
        }

        duration = null;
        return false;
    }
}
