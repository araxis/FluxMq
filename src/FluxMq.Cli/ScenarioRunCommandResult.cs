namespace FluxMq.Cli;

public sealed record ScenarioRunCommandResult(
    string Name,
    bool IsSuccess,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    double DurationMilliseconds,
    IReadOnlyList<ScenarioStepCommandResult> Steps);

public sealed record ScenarioStepCommandResult(
    string Name,
    string Type,
    string Status,
    string? Message,
    string? MatchedEventType,
    string? MatchedEventTopic,
    string? MatchedEventStatus);
