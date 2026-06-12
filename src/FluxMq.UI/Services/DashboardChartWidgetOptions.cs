namespace FluxMq.UI.Services;

public static class DashboardChartWidgetOptions
{
    public const string TypeKey = "chartType";

    public const string TypeBars = "bars";
    public const string TypeLine = "line";
    public const string TypeArea = "area";
    public const string TypeTopics = "topics";

    public static string NormalizeType(string? value)
        => value switch
        {
            TypeLine => TypeLine,
            TypeArea => TypeArea,
            TypeTopics => TypeTopics,
            _ => TypeBars
        };
}
