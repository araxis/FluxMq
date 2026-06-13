using System.Globalization;

namespace FluxMq.UI.Services;

public static class DashboardBarChartVisualOptions
{
    public const string HeaderKey = "chart.bar.header";
    public const string ShowHeaderKey = "chart.bar.showHeader";
    public const string ShowGridKey = "chart.bar.showGrid";
    public const string ShowLabelsKey = "chart.bar.showLabels";
    public const string OrientationKey = "chart.bar.orientation";
    public const string EmptyTextKey = "chart.bar.emptyText";
    public const string BarColorKey = "chart.bar.barColor";
    public const string GridColorKey = "chart.bar.gridColor";
    public const string LabelColorKey = "chart.bar.labelColor";
    public const string BarRadiusKey = "chart.bar.radius";

    public const string LegacyShowGridKey = "showGrid";
    public const string LegacyShowLabelsKey = "showLabels";
    public const string LegacyBarColorKey = "barColor";
    public const string LegacyOrientationKey = "orientation";

    public const string OrientationVertical = "vertical";
    public const string OrientationHorizontal = "horizontal";

    public const string DefaultHeader = "Bar chart";
    public const string DefaultEmptyText = "No chart data yet";
    public const string DefaultBarColor = "#2ed3c6";
    public const string DefaultGridColor = "#223042";
    public const string DefaultLabelColor = "#9fb0c5";
    public const int DefaultBarRadius = 4;
    public const int MinBarRadius = 0;
    public const int MaxBarRadius = 12;

    public static string NormalizeOrientation(string? value)
        => string.Equals(value?.Trim(), OrientationHorizontal, StringComparison.OrdinalIgnoreCase)
            ? OrientationHorizontal
            : OrientationVertical;

    public static int NormalizeBarRadius(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinBarRadius, MaxBarRadius)
            : DefaultBarRadius;
}
