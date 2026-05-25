namespace FluxMq.Cli;

public enum CliExitCode
{
    Success = 0,
    UsageError = 2,
    ValidationError = 3,
    ScenarioFailed = 4,
    UnexpectedError = 10
}
