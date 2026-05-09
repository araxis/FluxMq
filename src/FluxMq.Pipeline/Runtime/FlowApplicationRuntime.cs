using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class FlowApplicationRuntime : IAsyncDisposable, IDisposable
{
    private readonly IReadOnlyList<IDisposable> _links;
    private readonly IReadOnlyList<FlowRuntimeNode> _entryNodes;
    private bool _disposed;

    public FlowApplicationRuntime(
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>> workflows,
        IReadOnlyList<IDisposable> links,
        IReadOnlyList<FlowRuntimeNode>? entryNodes = null)
    {
        Resources = resources;
        Workflows = workflows;
        _links = links;
        _entryNodes = entryNodes ?? Nodes.ToArray();
    }

    public IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> Resources { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>> Workflows { get; }

    public IEnumerable<FlowRuntimeNode> Nodes => Resources.Values.Concat(Workflows.Values.SelectMany(workflow => workflow.Values));

    public Task Completion => Task.WhenAll(Nodes.Select(node => node.Node.Completion));

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
