namespace FluxMq.Cli;

public sealed record CliOptions(
    string Command,
    string? ConfigurationPath,
    string SectionName,
    CliOutputFormat OutputFormat)
{
    public const string ValidateCommand = "validate";
    public const string DefaultSectionName = "FluxMq:FlowApplication";
}
