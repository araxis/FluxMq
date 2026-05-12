namespace FluxMq.Pipeline.Definitions;

public sealed record LinkDefinition
{
    public required PortReference From { get; init; }
    public string? When { get; init; }
}
