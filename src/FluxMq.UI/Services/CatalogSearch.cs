namespace FluxMq.UI.Services;

public static class CatalogSearch
{
    public static CatalogSearchResult<TItem> Filter<TItem>(
        IEnumerable<TItem> items,
        string? searchText,
        Func<TItem, CatalogSearchFields> fields)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(fields);

        var allItems = items as IReadOnlyList<TItem> ?? items.ToArray();
        var text = searchText?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return new CatalogSearchResult<TItem>(allItems, allItems.Count, text);
        }

        var visibleItems = allItems
            .Where(item => Matches(fields(item), text))
            .ToArray();

        return new CatalogSearchResult<TItem>(visibleItems, allItems.Count, text);
    }

    private static bool Matches(CatalogSearchFields fields, string searchText)
        => ContainsSearch(fields.DisplayName, searchText) ||
           ContainsSearch(fields.Type, searchText) ||
           ContainsSearch(fields.Category, searchText) ||
           ContainsSearch(fields.Description, searchText);

    private static bool ContainsSearch(string value, string searchText)
        => value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
}

public sealed record CatalogSearchFields(
    string Type,
    string DisplayName,
    string Category,
    string Description);

public sealed record CatalogSearchResult<TItem>(
    IReadOnlyList<TItem> Items,
    int TotalCount,
    string SearchText)
{
    public int VisibleCount => Items.Count;

    public bool HasSearch => SearchText.Length > 0;
}
