namespace FluxMq.UI.Models;

public sealed record DashboardLayoutSnapshot(
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Rows,
    IReadOnlyList<double> ColumnPadding,
    IReadOnlyList<double> RowPadding,
    IReadOnlyList<DashboardCellSnapshot> Cells,
    IReadOnlyDictionary<string, DashboardWidgetSnapshot> Widgets,
    IReadOnlyDictionary<string, DashboardMetricSnapshot>? Metrics = null,
    IReadOnlyDictionary<string, DashboardWidgetBindingSnapshot>? Bindings = null)
{
    public int WidgetCount => Widgets.Count;

    public IReadOnlyDictionary<string, DashboardMetricSnapshot> Metrics { get; init; } =
        Metrics ?? new Dictionary<string, DashboardMetricSnapshot>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, DashboardWidgetBindingSnapshot> Bindings { get; init; } =
        Bindings ?? new Dictionary<string, DashboardWidgetBindingSnapshot>(StringComparer.Ordinal);
}

public sealed record DashboardWidgetSnapshot(
    string Name,
    string Type,
    IReadOnlyDictionary<string, string> Configuration)
{
    public string? ReadString(string key)
        => Configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}

public sealed record DashboardMetricSnapshot(
    string Name,
    string Source,
    string Aggregation,
    string Window,
    string? GroupBy,
    IReadOnlyDictionary<string, string> Filters,
    IReadOnlyDictionary<string, string> Format)
{
    public string? ReadFilter(string key)
        => Filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    public string? ReadFormat(string key)
        => Format.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}

public sealed record DashboardWidgetBindingSnapshot(
    string WidgetName,
    string? PrimaryMetric,
    IReadOnlyList<string> Metrics,
    IReadOnlyDictionary<string, string> Options)
{
    public bool UsesMetric(string metricName)
        => !string.IsNullOrWhiteSpace(metricName) &&
           (string.Equals(PrimaryMetric, metricName, StringComparison.Ordinal) ||
            Metrics.Contains(metricName, StringComparer.Ordinal));
}

public sealed record DashboardCellSnapshot(
    string Name,
    int Row,
    int Column,
    int RowSpan,
    int ColumnSpan,
    string? Widget = null,
    bool IsExplicit = true,
    IReadOnlyDictionary<string, string>? Style = null)
{
    public IReadOnlyDictionary<string, string> Style { get; init; } =
        Style ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsMerged => RowSpan > 1 || ColumnSpan > 1;

    public string Label => IsMerged
        ? $"{ColumnSpan} x {RowSpan}"
        : $"{Column + 1},{Row + 1}";

    public IEnumerable<(int Row, int Column)> CoveredCoordinates()
    {
        for (var row = Row; row < Row + RowSpan; row++)
        {
            for (var column = Column; column < Column + ColumnSpan; column++)
            {
                yield return (row, column);
            }
        }
    }

    public static DashboardCellSnapshot Slot(int row, int column)
        => new($"slot:{row}:{column}", row, column, 1, 1, IsExplicit: false);
}
