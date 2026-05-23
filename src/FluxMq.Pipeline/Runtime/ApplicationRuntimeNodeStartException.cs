using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class ApplicationRuntimeNodeStartException(NodeAddress nodeAddress, Exception innerException)
    : Exception($"Node '{nodeAddress}' failed to start: {innerException.Message}", innerException)
{
    public NodeAddress NodeAddress { get; } = nodeAddress;
}
