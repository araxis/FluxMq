namespace FluxMq.UI.Services;

public static class DashboardMetricValueVisualizationOptions
{
    public const string TitleKey = "metric.value.title";
    public const string SubtitleKey = "metric.value.subtitle";
    public const string ShowTitleKey = "metric.value.showTitle";
    public const string ShowSubtitleKey = "metric.value.showSubtitle";
    public const string ShowUnitKey = "metric.value.showUnit";
    public const string UnitTextKey = "metric.value.unitText";
    public const string TitleColorKey = "metric.value.titleColor";
    public const string SubtitleColorKey = "metric.value.subtitleColor";
    public const string ValueColorKey = "metric.value.valueColor";
    public const string UnitColorKey = "metric.value.unitColor";
    public const string TitleAlignKey = "metric.value.titleAlign";
    public const string ValueAlignKey = "metric.value.valueAlign";
    public const string ValuePlacementKey = "metric.value.valuePlacement";
    public const string PaddingKey = "metric.value.padding";

    public const string DefaultTitle = "Messages";
    public const string DefaultSubtitle = "Total matching events";
    public const string DefaultUnitText = "";
    public const string DefaultTitleColor = "#f3f7fb";
    public const string DefaultSubtitleColor = "#9fb0c5";
    public const string DefaultValueColor = "#f3f7fb";
    public const string DefaultUnitColor = DefaultSubtitleColor;
    public const int DefaultPadding = 14;

    public const string AlignLeft = "left";
    public const string AlignCenter = "center";
    public const string AlignRight = "right";
    public const string ValuePlacementTop = "top";
    public const string ValuePlacementMiddle = "middle";
    public const string ValuePlacementBottom = "bottom";

    public static string NormalizeHorizontalAlignment(string? value)
        => value switch
        {
            AlignCenter => AlignCenter,
            AlignRight => AlignRight,
            _ => AlignLeft
        };

    public static string NormalizeValuePlacement(string? value)
        => value switch
        {
            ValuePlacementMiddle => ValuePlacementMiddle,
            ValuePlacementBottom => ValuePlacementBottom,
            _ => ValuePlacementTop
        };
}
