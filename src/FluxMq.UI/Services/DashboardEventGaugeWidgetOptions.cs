namespace FluxMq.UI.Services;

public static class DashboardEventGaugeWidgetOptions
{
    public const string StyleKey = "gaugeStyle";
    public const string MinKey = "gauge.min";
    public const string MaxKey = "gauge.max";
    public const string TargetKey = "gauge.target";
    public const string WarningKey = "gauge.warning";
    public const string CriticalKey = "gauge.critical";
    public const string NormalColorKey = "gauge.normalColor";
    public const string WarningColorKey = "gauge.warningColor";
    public const string CriticalColorKey = "gauge.criticalColor";

    public const string StyleRing = "ring";
    public const string StyleMeter = "meter";
    public const string DefaultMin = "0";
    public const string DefaultMax = "100";
    public const string DefaultTarget = "80";
    public const string DefaultWarning = "70";
    public const string DefaultCritical = "90";
    public const string DefaultNormalColor = "#2ed3c6";
    public const string DefaultWarningColor = "#f4b642";
    public const string DefaultCriticalColor = "#ff5f6d";

    public static string NormalizeStyle(string? value)
        => value switch
        {
            StyleMeter => StyleMeter,
            _ => StyleRing
        };
}
