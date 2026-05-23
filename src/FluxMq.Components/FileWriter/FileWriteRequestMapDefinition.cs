namespace FluxMq.Components.FileWriter;

public sealed record FileWriteRequestMapDefinition
{
    public required string PathExpression { get; init; }
    public string? ContentExpression { get; init; }
    public string? ModeExpression { get; init; }
    public string? CreateDirectoryExpression { get; init; }
}
