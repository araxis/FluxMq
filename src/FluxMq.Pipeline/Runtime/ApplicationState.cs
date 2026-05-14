namespace FluxMq.Pipeline.Runtime;

public enum ApplicationState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Stopped,
    Faulted
}
