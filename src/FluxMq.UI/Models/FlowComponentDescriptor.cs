namespace FluxMq.UI.Models;

public sealed record FlowComponentDescriptor(
    string Type,
    string DisplayName,
    string Category,
    string Summary,
    bool IsResource,
    IReadOnlyList<ComponentPortDescriptor> Ports);
