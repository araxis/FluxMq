namespace FluxMq.Pipeline.Definitions;

public sealed record FlowLinkDefinition
{
    public required FlowPortReference From { get; init; }
    public string? When { get; init; }
}
