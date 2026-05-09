namespace FluxMq.Pipeline.Components;

public interface IFlowStartable
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
