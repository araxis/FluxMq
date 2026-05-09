using FluxMq.Pipeline.Runtime;

namespace FluxMq.App;

public sealed record FlowApplicationHostBuildResult(
    FlowApplicationRuntimeBuildResult? RuntimeBuild,
    IReadOnlyList<FlowApplicationHostBuildError> Errors)
{
    public bool IsSuccess => Errors.Count == 0 && RuntimeBuild?.IsSuccess == true;

    public static FlowApplicationHostBuildResult FromRuntime(FlowApplicationRuntimeBuildResult runtimeBuild)
        => new(runtimeBuild, []);

    public static FlowApplicationHostBuildResult FromHostError(FlowApplicationHostBuildError error)
        => new(null, [error]);
}
