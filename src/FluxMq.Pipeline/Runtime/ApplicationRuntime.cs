using FluxMq.Pipeline.Components;

namespace FluxMq.Pipeline.Runtime;

public sealed class ApplicationRuntime : IAsyncDisposable, IDisposable
{
    private readonly IReadOnlyList<RuntimeNode> _resourceEntryNodes;
    private bool _disposed;

    public ApplicationRuntime(
        IReadOnlyList<RuntimeNode> resources,
        IReadOnlyList<Workflow> workflows,
        IReadOnlyList<RuntimeNode> resourceEntryNodes)
    {
        Resources = resources;
        Workflows = workflows;
        _resourceEntryNodes = resourceEntryNodes;
    }

    public IReadOnlyList<RuntimeNode> Resources { get; }
    public IReadOnlyList<Workflow> Workflows { get; }

    public IEnumerable<RuntimeNode> Nodes => Resources.Concat(Workflows.SelectMany(wf => wf.Nodes));

    public Task Completion => Task.WhenAll(Nodes.Select(node => node.Node.Completion));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var resource in Resources)
        {
            if (resource.Node is IFlowStartable startable)
            {
                await startable.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var workflow in Workflows)
        {
            await workflow.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Complete()
    {
        foreach (var node in _resourceEntryNodes)
        {
            node.Node.Complete();
        }

        foreach (var workflow in Workflows)
        {
            workflow.Complete();
        }
    }

    public void Fault(Exception exception)
    {
        foreach (var resource in Resources)
        {
            resource.Node.Fault(exception);
        }

        foreach (var workflow in Workflows)
        {
            workflow.Fault(exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var workflow in Workflows)
        {
            workflow.Dispose();
        }

        foreach (var disposable in Resources.Select(node => node.Node).OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var workflow in Workflows)
        {
            await workflow.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var disposable in Resources.Select(node => node.Node).OfType<IAsyncDisposable>())
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
