namespace FluxMq.UI.Models;

public sealed record ComponentPortDescriptor(
    string Name,
    string ValueType,
    bool IsInput);
