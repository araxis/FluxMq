namespace FluxMq.Pipeline.Definitions;

public sealed record ApplicationDefinitionValidationError(
    ApplicationDefinitionValidationErrorCode Code,
    string Message);
