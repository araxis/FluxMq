using FluxMq.Pipeline.Components;

namespace FluxMq.UI.Models;

public sealed record DashboardEventFilterOption(string Value, string Label);

public sealed record DashboardEventFilterFieldDescriptor(
    string Key,
    string Label,
    string? Placeholder,
    string HelperText,
    Func<FlowEvent, string?> ReadValue,
    string? AttributeName = null);

public sealed record DashboardEventTypeDescriptor(
    string Value,
    string Label,
    IReadOnlyList<DashboardEventFilterFieldDescriptor> Fields,
    IReadOnlyList<DashboardEventFilterOption> StatusOptions);
