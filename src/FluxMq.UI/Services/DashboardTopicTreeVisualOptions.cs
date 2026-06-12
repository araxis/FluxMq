namespace FluxMq.UI.Services;

public static class DashboardTopicTreeVisualOptions
{
    public const string HeaderKey = "topic.tree.header";
    public const string ShowHeaderKey = "topic.tree.showHeader";
    public const string ShowSummaryKey = "topic.tree.showSummary";
    public const string ShowTopicCountKey = "topic.tree.showTopicCount";
    public const string ShowMessageCountKey = "topic.tree.showMessageCount";
    public const string EmptyTextKey = "topic.tree.emptyText";
    public const string HeaderColorKey = "topic.tree.headerColor";
    public const string TextColorKey = "topic.tree.textColor";
    public const string MutedColorKey = "topic.tree.mutedColor";
    public const string AccentColorKey = "topic.tree.accentColor";
    public const string ExcludeSystemTopicsKey = DashboardWidgetCatalog.ExcludeSystemTopicsKey;

    public const string DefaultHeader = "Topic tree";
    public const string DefaultEmptyText = "No topic traffic yet";
    public const string DefaultHeaderColor = "#f3f7fb";
    public const string DefaultTextColor = "#f3f7fb";
    public const string DefaultMutedColor = "#9fb0c5";
    public const string DefaultAccentColor = "#2ed3c6";
}
