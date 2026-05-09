namespace FluxMq.Cli;

public static class CliOptionsParser
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = new CliOptions("", null, CliOptions.DefaultSectionName);
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

        options = new CliOptions(command, configurationPath, sectionName);
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
}
