using Blazor.Diagrams.Core.Models;

namespace FluxMq.UI.Models;

public sealed class FlowPortModel : PortModel
{
    public FlowPortModel(NodeModel parent, PortAlignment alignment, string portName)
        : base(parent, alignment)
    {
        PortName = portName;
    }

    public string PortName { get; }
}
