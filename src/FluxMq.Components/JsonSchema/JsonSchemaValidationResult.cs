using FluxMq.Core.Models;

namespace FluxMq.Components.JsonSchema;

public sealed record JsonSchemaValidationResult
{
    public required string SchemaId { get; init; }
    public required bool IsValid { get; init; }
    public required MqttEnvelope Envelope { get; init; }
    public IReadOnlyList<JsonSchemaValidationIssue> Issues { get; init; } = [];
}
