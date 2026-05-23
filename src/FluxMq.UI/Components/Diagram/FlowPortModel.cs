using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace FluxMq.UI.Components.Diagram;

public sealed class FlowPortModel(NodeModel parent, PortAlignment alignment, string portName, bool singleLink = false)
    : PortModel(parent, alignment)
{
    public string PortName { get; } = portName;
    public bool SingleLink { get; } = singleLink;
    public bool IsPulsing { get; set; }

    public override bool CanAttachTo(ILinkable linkable)
    {
        if (SingleLink && Links.Count > 0) return false;
        return linkable is not FlowPortModel { SingleLink: true, Links.Count: > 0 } && base.CanAttachTo(linkable);
    }
}
