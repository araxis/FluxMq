namespace FluxMq.UI.Models;

public sealed record FlowComponentDragItem(
    string Id,
    FlowComponentDescriptor Component)
{
    public static FlowComponentDragItem FromPalette(FlowComponentDescriptor component)
        => new($"palette:{component.Type}", component);
}
