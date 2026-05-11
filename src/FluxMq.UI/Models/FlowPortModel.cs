using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Models.Base;

namespace FluxMq.UI.Models;

public sealed class FlowPortModel : PortModel
{
    public FlowPortModel(NodeModel parent, PortAlignment alignment, string portName, bool singleLink = false)
        : base(parent, alignment)
    {
        PortName = portName;
        SingleLink = singleLink;
    }

    public string PortName { get; }
    public bool SingleLink { get; }

    public override bool CanAttachTo(ILinkable linkable)
    {
        if (SingleLink && Links.Count > 0) return false;
        if (linkable is FlowPortModel { SingleLink: true } fp && fp.Links.Count > 0) return false;
        return base.CanAttachTo(linkable);
    }
}
