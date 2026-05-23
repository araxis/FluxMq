namespace FluxMq.Components.JsonSchema;

public sealed record JsonSchemaValidatorDefinition
{
    public required string SchemaJson { get; init; }
    public string SchemaId { get; init; } = "inline";
}
