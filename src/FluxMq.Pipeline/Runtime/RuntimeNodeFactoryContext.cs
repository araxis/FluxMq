using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record RuntimeNodeFactoryContext(
    NodeName Name,
    NodeDefinition Definition,
    string? WorkflowName,
    IReadOnlyDictionary<NodeName, RuntimeNode> Resources)
{
    public bool IsResource => WorkflowName is null;

    public NodeAddress Address => WorkflowName is null
        ? new NodeAddress(WellKnownScopes.Resources, Name)
        : new NodeAddress(WorkflowName, Name);

    /// <summary>
    /// Looks up a resource node (connection, etc.) by name. Used by workflow factories
    /// that need to inject a resource handle (e.g. an MQTT client) into their component.
    /// </summary>
    public RuntimeNode GetResource(NodeName resourceName)
    {
        if (!Resources.TryGetValue(resourceName, out var node))
        {
            throw new InvalidOperationException(
                $"Resource '{resourceName}' was not found. Define it under 'resources' before referencing it.");
        }
        return node;
    }
}
