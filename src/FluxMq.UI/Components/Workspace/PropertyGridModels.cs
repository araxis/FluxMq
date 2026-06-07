using Microsoft.AspNetCore.Components;

namespace FluxMq.UI.Components.Workspace;

public sealed record PropertyGridGroup(
    string Id,
    string Title,
    int SettingCount,
    RenderFragment Content,
    bool StartCollapsed = false);

public sealed record PropertyGridSelectOption(
    string Value,
    string Label,
    bool Disabled = false);

public sealed record PropertyGridIconSegmentOption(
    string Value,
    string Icon,
    string Label,
    bool Disabled = false);
