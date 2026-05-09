namespace FluxMq.Pipeline.Definitions;

public sealed record FlowApplicationDefinitionValidationResult(IReadOnlyList<FlowApplicationDefinitionValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
