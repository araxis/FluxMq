using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;

namespace FluxMq.UI.Components.Diagram;

public sealed class FlowDiagramLinkModel : LinkModel
{
    public FlowDiagramLinkModel(PortModel sourcePort, PortModel targetPort)
        : base(sourcePort, targetPort)
    {
    }

    public FlowDiagramLinkModel(Anchor sourceAnchor, Anchor targetAnchor)
        : base(sourceAnchor, targetAnchor)
    {
    }

    public string NormalColor { get; private set; } = FlowLinkVisuals.DefaultColor;

    public double NormalWidth { get; private set; } = FlowLinkVisuals.DefaultWidth;

    public void SetNormalStyle(string color, double width)
    {
        NormalColor = color;
        NormalWidth = width;
        SelectedColor = FlowLinkVisuals.SelectedColor;
        Color = color;
        Width = width;
    }

    public void ApplySelectionStyle(bool selected)
    {
        Color = NormalColor;
        Width = selected ? FlowLinkVisuals.SelectedWidth : NormalWidth;
        Refresh();
    }
}
