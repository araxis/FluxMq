namespace FluxMq.Cli;

public sealed record CliOptions(
    string Command,
    string? ConfigurationPath,
    string SectionName,
    CliOutputFormat OutputFormat,
    TimeSpan? RunDuration)
{
    public const string ValidateCommand = "validate";
    public const string RunCommand = "run";
    public const string DefaultSectionName = "FluxMq:FlowApplication";
}
