namespace FluxMq.Pipeline.Definitions;

public sealed record FlowApplicationDefinitionValidationError(
    FlowApplicationDefinitionValidationErrorCode Code,
    string Message);
