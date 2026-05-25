namespace FluxMq.UI.Models;

public sealed record DashboardLayoutSnapshot(
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Rows,
    IReadOnlyList<double> ColumnPadding,
    IReadOnlyList<double> RowPadding,
    IReadOnlyList<DashboardCellSnapshot> Cells,
    IReadOnlyDictionary<string, DashboardWidgetSnapshot> Widgets)
{
    public int WidgetCount => Widgets.Count;
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

public sealed record DashboardCellSnapshot(
    string Name,
    int Row,
    int Column,
    int RowSpan,
    int ColumnSpan,
    string? Widget = null,
    bool IsExplicit = true)
{
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
