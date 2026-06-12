namespace FluxMq.UI.Services;

public static class DashboardMetricGaugeVisualizationOptions
{
    public const string ShapeKey = "metric.gauge.shape";
    public const string LabelKey = "metric.gauge.label";
    public const string ShowLabelKey = "metric.gauge.showLabel";
    public const string MinKey = "metric.gauge.min";
    public const string MaxKey = "metric.gauge.max";
    public const string TargetKey = "metric.gauge.target";
    public const string WarningKey = "metric.gauge.warning";
    public const string CriticalKey = "metric.gauge.critical";
    public const string NormalColorKey = "metric.gauge.normalColor";
    public const string WarningColorKey = "metric.gauge.warningColor";
    public const string CriticalColorKey = "metric.gauge.criticalColor";

    public const string ShapeRing = "ring";
    public const string ShapeMeter = "meter";
    public const string DefaultLabel = "Event gauge";
    public const string DefaultMin = "0";
    public const string DefaultMax = "100";
    public const string DefaultTarget = "80";
    public const string DefaultWarning = "70";
    public const string DefaultCritical = "90";
    public const string DefaultNormalColor = "#2ed3c6";
    public const string DefaultWarningColor = "#f4b642";
    public const string DefaultCriticalColor = "#ff5f6d";

    public static string NormalizeShape(string? value)
        => value switch
        {
            ShapeMeter => ShapeMeter,
            _ => ShapeRing
        };
}
