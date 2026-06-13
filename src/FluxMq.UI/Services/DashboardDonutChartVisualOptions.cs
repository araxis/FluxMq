using System.Globalization;

namespace FluxMq.UI.Services;

public static class DashboardDonutChartVisualOptions
{
    public const string HeaderKey = "chart.donut.header";
    public const string ShowHeaderKey = "chart.donut.showHeader";
    public const string ShowLegendKey = "chart.donut.showLegend";
    public const string ShowTotalKey = "chart.donut.showTotal";
    public const string LimitKey = "chart.donut.limit";
    public const string InnerRadiusKey = "chart.donut.innerRadius";
    public const string EmptyTextKey = "chart.donut.emptyText";
    public const string SegmentColor1Key = "chart.donut.segmentColor1";
    public const string SegmentColor2Key = "chart.donut.segmentColor2";
    public const string SegmentColor3Key = "chart.donut.segmentColor3";
    public const string SegmentColor4Key = "chart.donut.segmentColor4";
    public const string SegmentColor5Key = "chart.donut.segmentColor5";
    public const string LabelColorKey = "chart.donut.labelColor";
    public const string MutedColorKey = "chart.donut.mutedColor";

    public const string LegacyLimitKey = "limit";
    public const string LegacyPaletteKey = "palette";
    public const string LegacyGroupByKey = "groupBy";

    public const string DefaultHeader = "Donut chart";
    public const string DefaultEmptyText = "No categories yet";
    public const string DefaultSegmentColor1 = "#2ed3c6";
    public const string DefaultSegmentColor2 = "#60a5fa";
    public const string DefaultSegmentColor3 = "#f7c948";
    public const string DefaultSegmentColor4 = "#f97373";
    public const string DefaultSegmentColor5 = "#a78bfa";
    public const string DefaultLabelColor = "#f3f7fb";
    public const string DefaultMutedColor = "#9fb0c5";
    public const int DefaultLimit = 6;
    public const int MinLimit = 2;
    public const int MaxLimit = 10;
    public const int DefaultInnerRadius = 58;
    public const int MinInnerRadius = 35;
    public const int MaxInnerRadius = 75;

    public static int NormalizeLimit(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinLimit, MaxLimit)
            : DefaultLimit;

    public static int NormalizeInnerRadius(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinInnerRadius, MaxInnerRadius)
            : DefaultInnerRadius;
}
