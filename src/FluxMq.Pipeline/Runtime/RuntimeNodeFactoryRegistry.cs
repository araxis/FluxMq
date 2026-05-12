using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class RuntimeNodeFactoryRegistry
{
    private readonly Dictionary<NodeType, RuntimeNodeFactory> _factories = [];

    public IReadOnlyDictionary<NodeType, RuntimeNodeFactory> Factories => _factories;

    public RuntimeNodeFactoryRegistry Register(NodeType type, RuntimeNodeFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(type, factory))
        {
            throw new InvalidOperationException($"A flow node factory is already registered for '{type}'.");
        }

        return this;
    }

    public RuntimeNodeFactoryRegistry Register(
        NodeType type,
        Func<NodeName, NodeDefinition, RuntimeNode> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return Register(type, context => factory(context.Name, context.Definition));
    }

    public bool TryGetFactory(NodeType type, out RuntimeNodeFactory factory)
        => _factories.TryGetValue(type, out factory!);
}
