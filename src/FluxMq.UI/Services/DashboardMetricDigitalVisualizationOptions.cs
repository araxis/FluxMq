namespace FluxMq.UI.Services;

public static class DashboardMetricDigitalVisualizationOptions
{
    public const string LabelKey = "metric.digital.label";
    public const string ShowLabelKey = "metric.digital.showLabel";
    public const string LabelPlacementKey = "metric.digital.labelPlacement";
    public const string StyleKey = "metric.digital.style";
    public const string GlowKey = "metric.digital.glow";
    public const string BackgroundColorKey = "metric.digital.backgroundColor";
    public const string SegmentColorKey = "metric.digital.segmentColor";
    public const string InactiveSegmentColorKey = "metric.digital.inactiveSegmentColor";
    public const string LabelColorKey = "metric.digital.labelColor";
    public const string DigitsKey = "metric.digital.digits";
    public const string BorderColorKey = "metric.digital.borderColor";
    public const string BorderWidthKey = "metric.digital.borderWidth";
    public const string RadiusKey = "metric.digital.radius";
    public const string PaddingKey = "metric.digital.padding";
    public const string FitModeKey = "metric.digital.fitMode";
    public const string AlignKey = "metric.digital.align";
    public const string PlacementKey = "metric.digital.placement";

    public const string DefaultLabel = "Messages";
    public const string DefaultBackgroundColor = "#040609";
    public const string DefaultSegmentColor = "#db8b98";
    public const string DefaultInactiveSegmentColor = "#351820";
    public const string DefaultLabelColor = "#7f928b";
    public const string DefaultBorderColor = "#1d4850";
    public const int DefaultBorderWidth = 1;
    public const int DefaultRadius = 7;
    public const int DefaultPadding = 10;
    public const int DefaultDigits = 4;
    public const int MinDigits = 1;
    public const int MaxDigits = 8;

    public const string LabelPlacementTop = "top";
    public const string LabelPlacementBottom = "bottom";
    public const string LabelPlacementHidden = "hidden";
    public const string StylePanel = "panel";
    public const string StyleSegment = "segment";
    public const string StyleTerminal = "terminal";
    public const string GlowOff = "off";
    public const string GlowSoft = "soft";
    public const string GlowStrong = "strong";
    public const string FitCompact = "compact";
    public const string FitFill = "fill";
    public const string AlignLeft = DashboardMetricValueVisualizationOptions.AlignLeft;
    public const string AlignCenter = DashboardMetricValueVisualizationOptions.AlignCenter;
    public const string AlignRight = DashboardMetricValueVisualizationOptions.AlignRight;
    public const string PlacementTop = DashboardMetricValueVisualizationOptions.ValuePlacementTop;
    public const string PlacementMiddle = DashboardMetricValueVisualizationOptions.ValuePlacementMiddle;
    public const string PlacementBottom = DashboardMetricValueVisualizationOptions.ValuePlacementBottom;

    public static string NormalizeStyle(string? value)
        => value switch
        {
            StyleSegment => StyleSegment,
            StyleTerminal => StyleTerminal,
            _ => StylePanel
        };

    public static string NormalizeGlow(string? value)
        => value switch
        {
            GlowOff => GlowOff,
            GlowStrong => GlowStrong,
            _ => GlowSoft
        };

    public static string NormalizeLabelPlacement(string? value)
        => value switch
        {
            LabelPlacementTop => LabelPlacementTop,
            LabelPlacementHidden => LabelPlacementHidden,
            _ => LabelPlacementBottom
        };

    public static string NormalizeFitMode(string? value)
        => value switch
        {
            FitFill => FitFill,
            _ => FitCompact
        };

    public static string NormalizeAlignment(string? value)
        => value switch
        {
            AlignLeft => AlignLeft,
            AlignRight => AlignRight,
            _ => AlignCenter
        };

    public static string NormalizePlacement(string? value)
        => value switch
        {
            PlacementTop => PlacementTop,
            PlacementBottom => PlacementBottom,
            _ => PlacementMiddle
        };

    public static int NormalizeDigits(string? value)
        => int.TryParse(value, out var digits)
            ? NormalizeDigits(digits)
            : DefaultDigits;

    public static int NormalizeDigits(int value)
        => Math.Clamp(value, MinDigits, MaxDigits);
}
