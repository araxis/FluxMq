namespace FluxMq.Pipeline.Runtime;

public sealed class ApplicationRuntime(
    IReadOnlyList<RuntimeNode> resources,
    IReadOnlyList<Workflow> workflows,
    IReadOnlyList<RuntimeNode> resourceEntryNodes)
    : IAsyncDisposable, IDisposable
{
    private readonly IReadOnlyList<RuntimeNode> _resourceEntryNodes = resourceEntryNodes ?? throw new ArgumentNullException(nameof(resourceEntryNodes));
    private bool _disposed;

    public IReadOnlyList<RuntimeNode> Resources { get; } = resources ?? throw new ArgumentNullException(nameof(resources));
    public IReadOnlyList<Workflow> Workflows { get; } = workflows ?? throw new ArgumentNullException(nameof(workflows));

    public IEnumerable<RuntimeNode> Nodes => Resources.Concat(Workflows.SelectMany(wf => wf.Nodes));

    public Task Completion => Task.WhenAll(Nodes.Select(node => node.Node.Completion));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var all = Resources.Concat(Workflows.SelectMany(wf => wf.Nodes));
        foreach (var group in all.GroupBy(n => n.Phase).OrderBy(g => g.Key))
        {
            foreach (var node in group)
            {
                await node.Node.StartAsync(cancellationToken).ConfigureAwait(false);
            }
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
