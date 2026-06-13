using System.Globalization;

namespace FluxMq.UI.Services;

public static class DashboardAreaChartVisualOptions
{
    public const string HeaderKey = "chart.area.header";
    public const string ShowHeaderKey = "chart.area.showHeader";
    public const string ShowGridKey = "chart.area.showGrid";
    public const string ShowLabelsKey = "chart.area.showLabels";
    public const string ShowPointsKey = "chart.area.showPoints";
    public const string EmptyTextKey = "chart.area.emptyText";
    public const string LineColorKey = "chart.area.lineColor";
    public const string FillColorKey = "chart.area.fillColor";
    public const string FillOpacityKey = "chart.area.fillOpacity";
    public const string GridColorKey = "chart.area.gridColor";
    public const string LabelColorKey = "chart.area.labelColor";
    public const string LineWidthKey = "chart.area.lineWidth";

    public const string LegacyShowGridKey = "showGrid";
    public const string LegacyShowLabelsKey = "showLabels";
    public const string LegacyShowPointsKey = "showPoints";
    public const string LegacyLineColorKey = "lineColor";
    public const string LegacyFillColorKey = "fillColor";
    public const string LegacyFillOpacityKey = "fillOpacity";

    public const string DefaultHeader = "Area chart";
    public const string DefaultEmptyText = "No chart data yet";
    public const string DefaultLineColor = "#2ed3c6";
    public const string DefaultFillColor = "#2ed3c6";
    public const string DefaultGridColor = "#223042";
    public const string DefaultLabelColor = "#9fb0c5";
    public const int DefaultLineWidth = 2;
    public const int MinLineWidth = 1;
    public const int MaxLineWidth = 8;
    public const int DefaultFillOpacity = 26;
    public const int MinFillOpacity = 0;
    public const int MaxFillOpacity = 100;

    public static int NormalizeLineWidth(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinLineWidth, MaxLineWidth)
            : DefaultLineWidth;

    public static int NormalizeFillOpacity(string? value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed))
        {
            return DefaultFillOpacity;
        }

        var percentage = parsed is >= 0 and <= 1
            ? parsed * 100d
            : parsed;
        return Math.Clamp((int)Math.Round(percentage), MinFillOpacity, MaxFillOpacity);
    }
}
