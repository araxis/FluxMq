using System.Globalization;

namespace FluxMq.UI.Services;

public static class DashboardLineChartVisualOptions
{
    public const string HeaderKey = "chart.line.header";
    public const string ShowHeaderKey = "chart.line.showHeader";
    public const string ShowGridKey = "chart.line.showGrid";
    public const string ShowLabelsKey = "chart.line.showLabels";
    public const string ShowPointsKey = "chart.line.showPoints";
    public const string EmptyTextKey = "chart.line.emptyText";
    public const string LineColorKey = "chart.line.lineColor";
    public const string GridColorKey = "chart.line.gridColor";
    public const string LabelColorKey = "chart.line.labelColor";
    public const string LineWidthKey = "chart.line.lineWidth";

    public const string LegacyShowGridKey = "showGrid";
    public const string LegacyShowLabelsKey = "showLabels";
    public const string LegacyShowPointsKey = "showPoints";
    public const string LegacyLineColorKey = "lineColor";

    public const string DefaultHeader = "Line chart";
    public const string DefaultEmptyText = "No chart data yet";
    public const string DefaultLineColor = "#2ed3c6";
    public const string DefaultGridColor = "#223042";
    public const string DefaultLabelColor = "#9fb0c5";
    public const int DefaultLineWidth = 3;
    public const int MinLineWidth = 1;
    public const int MaxLineWidth = 8;

    public static int NormalizeLineWidth(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinLineWidth, MaxLineWidth)
            : DefaultLineWidth;
}
