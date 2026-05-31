using FluxFlow.Engine.Runtime;
using FluxMq.App.Definitions;

namespace FluxMq.App;

public sealed record FlowApplicationHostBuildResult(
    FluxMqApplicationDefinitionValidationResult? DefinitionValidation,
    ApplicationRuntimeBuildResult? RuntimeBuild,
    IReadOnlyList<FlowApplicationHostBuildError> Errors)
{
    public bool IsSuccess => Errors.Count == 0 && RuntimeBuild?.IsSuccess == true;

    public static FlowApplicationHostBuildResult FromRuntime(
        FluxMqApplicationDefinitionValidationResult definitionValidation,
        ApplicationRuntimeBuildResult runtimeBuild)
        => new(definitionValidation, runtimeBuild, []);

    public static FlowApplicationHostBuildResult FromDefinitionValidation(
        FluxMqApplicationDefinitionValidationResult definitionValidation)
        => new(definitionValidation, null, []);

    public static FlowApplicationHostBuildResult FromHostError(FlowApplicationHostBuildError error)
        => new(null, null, [error]);
}
