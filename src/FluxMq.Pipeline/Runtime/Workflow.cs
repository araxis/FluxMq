using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class Workflow : IAsyncDisposable, IDisposable
{
    private readonly IReadOnlyList<RuntimeNode> _entryNodes;
    private bool _disposed;

    public Workflow(
        WorkflowName name,
        IReadOnlyList<RuntimeNode> nodes,
        IReadOnlyList<IDisposable> links,
        IReadOnlyList<RuntimeNode> entryNodes)
    {
        Name = name;
        Nodes = nodes;
        Links = links;
        _entryNodes = entryNodes;
    }

    public WorkflowName Name { get; }
    public IReadOnlyList<RuntimeNode> Nodes { get; }
    public IReadOnlyList<IDisposable> Links { get; }

    public Task Completion => Task.WhenAll(Nodes.Select(node => node.Node.Completion));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var group in Nodes.GroupBy(n => n.Phase).OrderBy(g => g.Key))
        {
            foreach (var node in group)
            {
                await node.Node.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Complete()
    {
        foreach (var node in _entryNodes)
        {
            node.Node.Complete();
        }
    }

    public void Fault(Exception exception)
    {
        foreach (var node in Nodes)
        {
            node.Node.Fault(exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var link in Links)
        {
            link.Dispose();
        }

        foreach (var disposable in Nodes.Select(node => node.Node).OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();

        foreach (var disposable in Nodes.Select(node => node.Node).OfType<IAsyncDisposable>())
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
