namespace FluxMq.UI.Models;

public sealed record DashboardWidgetModule(
    DashboardWidgetDescriptor Descriptor,
    Type EditCellComponent,
    Type LiveComponent,
    IReadOnlyDictionary<string, string> DefaultConfiguration,
    IReadOnlyList<DashboardWidgetPropertyGroupDefinition> PropertyGroups,
    DashboardWidgetStyleDefinition DefaultStyle,
    DashboardWidgetLayoutContract Layout,
    IReadOnlyList<string>? CompatibilityTypeIds = null,
    string MetricVisualizationId = DashboardMetricVisualizationIds.Value)
{
    public string Type => Descriptor.Type;

    public IReadOnlyList<string> CompatibilityTypeIds { get; init; } = CompatibilityTypeIds ?? [];
}

public static class DashboardMetricVisualizationIds
{
    public const string Value = "metric.value";
    public const string Digital = "metric.digital";
    public const string RadialGauge = "metric.radialGauge";
    public const string ArcMeter = "metric.arcMeter";
    public const string LinearMeter = "metric.linearMeter";
}

public static class DashboardMetricValueKinds
{
    public const string Number = "number";
    public const string Rate = "rate";
    public const string Bytes = "bytes";
    public const string Percent = "percent";
    public const string Status = "status";
}

public sealed record DashboardMetricVisualizationModule(
    string Id,
    string DisplayName,
    string Icon,
    IReadOnlySet<string> SupportedValueKinds,
    IReadOnlyDictionary<string, string> DefaultConfiguration,
    IReadOnlyList<DashboardWidgetPropertyGroupDefinition> PropertyGroups,
    Type EditCellComponent,
    Type LiveComponent,
    string Description);

public sealed record DashboardWidgetPropertyGroupDefinition(
    string Id,
    string Title,
    IReadOnlyList<DashboardWidgetPropertyDefinition> Properties,
    bool StartCollapsed = false);

public sealed record DashboardWidgetPropertyDefinition(
    string Key,
    string Label,
    DashboardWidgetPropertyEditorKind Editor,
    string? Unit = null,
    string? HelpText = null,
    string? DefaultValue = null,
    IReadOnlyList<DashboardWidgetPropertyOption>? Options = null)
{
    public IReadOnlyList<DashboardWidgetPropertyOption> Options { get; init; } = Options ?? [];
}

public sealed record DashboardWidgetPropertyOption(string Value, string Label);

public enum DashboardWidgetPropertyEditorKind
{
    Text,
    Select,
    Number,
    Toggle,
    Color,
    Metric,
    TopicFilter,
    Segmented,
    ReadOnly
}

public sealed record DashboardWidgetStyleDefinition(
    string Background = "#101720",
    string Surface = "#141c26",
    string Accent = "#2ed3c6",
    string Text = "#f3f7fb",
    string MutedText = "#9fb0c5",
    string Border = "#223042",
    int BorderWidth = 1,
    int CornerRadius = 8,
    int Padding = 16,
    string ValueSize = "large",
    string LabelSize = "compact",
    string Density = "comfortable");

public sealed record DashboardWidgetLayoutContract(
    int MinimumColumnSpan = 1,
    int MinimumRowSpan = 1,
    int PreferredColumnSpan = 1,
    int PreferredRowSpan = 1,
    bool AllowCompact = true,
    string OverflowPolicy = "clip");
