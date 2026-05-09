using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class FlowRuntimeNodeFactoryRegistry
{
    private readonly Dictionary<FlowNodeType, FlowRuntimeNodeFactory> _factories = [];

    public IReadOnlyDictionary<FlowNodeType, FlowRuntimeNodeFactory> Factories => _factories;

    public FlowRuntimeNodeFactoryRegistry Register(FlowNodeType type, FlowRuntimeNodeFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(type, factory))
        {
            throw new InvalidOperationException($"A flow node factory is already registered for '{type}'.");
        }

        return this;
    }

    public bool TryGetFactory(FlowNodeType type, out FlowRuntimeNodeFactory factory)
        => _factories.TryGetValue(type, out factory!);
}
