namespace FluxMq.Components.JsonSchema;

public sealed record JsonSchemaValidatorDefinition
{
    public string? SchemaJson { get; init; }
    public string? SchemaPath { get; init; }
    public string SchemaId { get; init; } = "inline";
}
