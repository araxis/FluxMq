using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed record FlowRuntimeNodeFactoryContext(
    FlowNodeName Name,
    FlowNodeDefinition Definition,
    string? WorkflowName,
    IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> Resources)
{
    public bool IsResource => WorkflowName is null;

    /// <summary>
    /// Looks up a resource node (connection, etc.) by name. Used by workflow factories
    /// that need to inject a resource handle (e.g. an MQTT session) into their component.
    /// </summary>
    public FlowRuntimeNode GetResource(FlowNodeName resourceName)
    {
        if (!Resources.TryGetValue(resourceName, out var node))
        {
            throw new InvalidOperationException(
                $"Resource '{resourceName}' was not found. Define it under 'resources' before referencing it.");
        }
        return node;
    }
}
