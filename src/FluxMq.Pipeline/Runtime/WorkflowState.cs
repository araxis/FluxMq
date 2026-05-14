namespace FluxMq.Pipeline.Runtime;

public enum WorkflowState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Stopped,
    Faulted
}
