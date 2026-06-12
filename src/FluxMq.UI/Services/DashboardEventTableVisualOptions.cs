namespace FluxMq.UI.Services;

public static class DashboardEventTableVisualOptions
{
    public const string HeaderKey = "table.header";
    public const string ShowHeaderKey = "table.showHeader";
    public const string RowCountKey = "table.rowCount";
    public const string DensityKey = "table.density";
    public const string ShowTimeKey = "table.showTime";
    public const string ShowEventKey = "table.showEvent";
    public const string ShowTopicKey = "table.showTopic";
    public const string ShowStatusKey = "table.showStatus";
    public const string ShowPayloadKey = "table.showPayload";
    public const string EmptyTextKey = "table.emptyText";
    public const string HeaderColorKey = "table.headerColor";
    public const string TextColorKey = "table.textColor";
    public const string MutedColorKey = "table.mutedColor";

    public const string LegacyRowCountKey = "rowCount";
    public const string LegacyDensityKey = "density";
    public const string LegacyPayloadPreviewKey = "payloadPreview";

    public const string DensityCompact = "compact";
    public const string DensityComfortable = "comfortable";

    public const string DefaultHeader = "Event table";
    public const string DefaultEmptyText = "No events yet";
    public const string DefaultHeaderColor = "#9fb0c5";
    public const string DefaultTextColor = "#f3f7fb";
    public const string DefaultMutedColor = "#9fb0c5";
    public const int DefaultRowCount = 6;
    public const int MinRowCount = 1;
    public const int MaxRowCount = 25;

    public static string NormalizeDensity(string? value)
        => string.Equals(value, DensityComfortable, StringComparison.Ordinal)
            ? DensityComfortable
            : DensityCompact;

    public static int NormalizeRowCount(string? value)
        => int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, MinRowCount, MaxRowCount)
            : DefaultRowCount;
}
