namespace FluxMq.Components.JsonSchema;

public sealed record JsonSchemaValidationIssue(
    string Path,
    string Message);
