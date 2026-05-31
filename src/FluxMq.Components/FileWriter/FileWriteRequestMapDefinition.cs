namespace FluxMq.Components.FileWriter;

public sealed record FileWriteRequestMapDefinition
{
    public required string Expression { get; init; }
}
