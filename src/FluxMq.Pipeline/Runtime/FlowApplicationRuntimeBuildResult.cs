using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record FlowApplicationRuntimeBuildResult(
    FlowApplicationRuntime? Runtime,
    FlowApplicationDefinitionValidationResult Validation,
    IReadOnlyList<FlowApplicationRuntimeBuildError> Errors)
{
    public bool IsSuccess => Runtime is not null && Validation.IsValid && Errors.Count == 0;

    public static FlowApplicationRuntimeBuildResult Succeeded(
        FlowApplicationRuntime runtime,
        FlowApplicationDefinitionValidationResult validation)
        => new(runtime, validation, []);

    public static FlowApplicationRuntimeBuildResult Failed(
        FlowApplicationDefinitionValidationResult validation,
        IReadOnlyList<FlowApplicationRuntimeBuildError> errors)
        => new(null, validation, errors);
}
