using System.Globalization;

namespace FluxMq.UI.Services;

public static class DashboardTopicActivityVisualOptions
{
    public const string HeaderKey = "topic.activity.header";
    public const string ShowHeaderKey = "topic.activity.showHeader";
    public const string LimitKey = "topic.activity.limit";
    public const string ShowCountsKey = "topic.activity.showCounts";
    public const string EmptyTextKey = "topic.activity.emptyText";
    public const string HeaderColorKey = "topic.activity.headerColor";
    public const string TextColorKey = "topic.activity.textColor";
    public const string MutedColorKey = "topic.activity.mutedColor";
    public const string AccentColorKey = "topic.activity.accentColor";

    public const string DefaultHeader = "Topic activity";
    public const string DefaultEmptyText = "No topic activity yet";
    public const string DefaultHeaderColor = "#f3f7fb";
    public const string DefaultTextColor = "#f3f7fb";
    public const string DefaultMutedColor = "#9fb0c5";
    public const string DefaultAccentColor = "#2ed3c6";
    public const int DefaultLimit = 8;
    public const int MinLimit = 1;
    public const int MaxLimit = 24;

    public static int NormalizeLimit(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, MinLimit, MaxLimit)
            : DefaultLimit;
}
