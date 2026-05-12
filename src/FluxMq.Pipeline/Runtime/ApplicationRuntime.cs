using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class ApplicationRuntime : IAsyncDisposable, IDisposable
{
    private readonly IReadOnlyList<IDisposable> _links;
    private readonly IReadOnlyList<RuntimeNode> _entryNodes;
    private bool _disposed;

    public ApplicationRuntime(
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> workflows,
        IReadOnlyList<IDisposable> links,
        IReadOnlyList<RuntimeNode>? entryNodes = null)
    {
        Resources = resources;
        Workflows = workflows;
        _links = links;
        _entryNodes = entryNodes ?? Nodes.ToArray();
    }

    public IReadOnlyDictionary<NodeName, RuntimeNode> Resources { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> Workflows { get; }

    public IEnumerable<RuntimeNode> Nodes => Resources.Values.Concat(Workflows.Values.SelectMany(workflow => workflow.Values));

    public Task Completion => Task.WhenAll(Nodes.Select(node => node.Node.Completion));

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var resource in Resources.Values)
        {
            await StartNodeAsync(resource, cancellationToken).ConfigureAwait(false);
        }

        foreach (var node in WorkflowNodes())
        {
            await StartNodeAsync(node, cancellationToken).ConfigureAwait(false);
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

        foreach (var link in _links)
        {
            link.Dispose();
        }

        foreach (var disposable in WorkflowNodes().Select(node => node.Node).OfType<IDisposable>())
        {
            disposable.Dispose();
        }

        foreach (var disposable in Resources.Values.Select(node => node.Node).OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();

        foreach (var disposable in WorkflowNodes().Select(node => node.Node).OfType<IAsyncDisposable>())
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var disposable in Resources.Values.Select(node => node.Node).OfType<IAsyncDisposable>())
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    private IEnumerable<RuntimeNode> WorkflowNodes()
        => Workflows.Values.SelectMany(workflow => workflow.Values);

    private static async Task StartNodeAsync(RuntimeNode node, CancellationToken cancellationToken)
    {
        if (node.Node is IFlowStartable startable)
        {
            await startable.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
