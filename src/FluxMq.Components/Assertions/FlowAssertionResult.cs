namespace FluxMq.Components.Assertions;

public sealed record FlowAssertionResult
{
    public required string AssertionName { get; init; }
    public required string Expression { get; init; }
    public required string InputType { get; init; }
    public required bool Passed { get; init; }
    public object? Value { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}
