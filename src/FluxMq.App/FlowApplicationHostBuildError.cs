namespace FluxMq.App;

public sealed record FlowApplicationHostBuildError(
    FlowApplicationHostBuildErrorCode Code,
    string Message,
    Exception? Exception = null);
