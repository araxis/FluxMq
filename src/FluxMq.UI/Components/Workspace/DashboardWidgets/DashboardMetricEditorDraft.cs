using FluxMq.UI.Models;
using FluxMq.UI.Services;

namespace FluxMq.UI.Components.Workspace;

public sealed class DashboardMetricEditorDraft
{
    private const string AnyValue = "";

    public string Name { get; private set; }

    public string Source { get; set; } = "runtimeEvents";

    public string Aggregation { get; set; } = "count";

    public string Window { get; set; } = "60s";

    public string EventType { get; private set; } = AnyValue;

    public string TopicStartsWith { get; set; } = AnyValue;

    public string TopicNotStartsWith { get; set; } = AnyValue;

    public string SubjectStartsWith { get; set; } = AnyValue;

    public string Status { get; set; } = AnyValue;

    public string Qos { get; set; } = AnyValue;

    public string Retain { get; set; } = AnyValue;

    public string SchemaId { get; set; } = AnyValue;

    public string GroupBy { get; set; } = AnyValue;

    public string Format { get; set; } = "number";

    public DashboardEventTypeDescriptor CurrentEventType { get; private set; }

    private readonly DashboardEventFilterCatalog _eventFilters;

    private DashboardMetricEditorDraft(
        string name,
        DashboardEventFilterCatalog eventFilters)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "metric" : name.Trim();
        _eventFilters = eventFilters;
        CurrentEventType = eventFilters.Find(AnyValue);
    }

    public static DashboardMetricEditorDraft Create(
        string metricName,
        DashboardMetricSnapshot? metric,
        DashboardEventFilterCatalog eventFilters,
        DashboardMetricRegistry metricRegistry,
        string widgetType)
    {
        ArgumentNullException.ThrowIfNull(eventFilters);
        ArgumentNullException.ThrowIfNull(metricRegistry);

        var draft = new DashboardMetricEditorDraft(metricName, eventFilters);
        if (metric is null)
        {
            var defaultQuery = metricRegistry.CreateDefaultQuery(widgetType);
            draft.Source = defaultQuery.Source;
            draft.Aggregation = defaultQuery.Aggregation;
            draft.Window = defaultQuery.Window;
            draft.GroupBy = defaultQuery.GroupBy ?? AnyValue;
            draft.Format = defaultQuery.Format;
            draft.SetEventType(defaultQuery.EventType);
            draft.TopicStartsWith = Normalize(defaultQuery.TopicStartsWith);
            draft.TopicNotStartsWith = Normalize(defaultQuery.TopicNotStartsWith);
            draft.Status = Normalize(defaultQuery.Status);
            return draft;
        }

        draft.Source = Normalize(metric.Source, "runtimeEvents");
        draft.Aggregation = Normalize(metric.Aggregation, "count");
        draft.Window = Normalize(metric.Window, "60s");
        draft.GroupBy = Normalize(metric.GroupBy);
        draft.Format = Normalize(metric.ReadFormat("unit"), "number");
        draft.SetEventType(metric.ReadFilter(DashboardEventFilterCatalog.EventTypeKey));
        draft.TopicStartsWith = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.TopicStartsWithKey));
        draft.TopicNotStartsWith = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.TopicNotStartsWithKey));
        draft.SubjectStartsWith = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.SubjectStartsWithKey));
        draft.Status = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.StatusKey));
        draft.Qos = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.AttributeFilterKey("qos")));
        draft.Retain = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.AttributeFilterKey("retain")));
        draft.SchemaId = Normalize(metric.ReadFilter(DashboardEventFilterCatalog.AttributeFilterKey("schemaId")));
        return draft;
    }

    public void SetEventType(string? value)
    {
        EventType = Normalize(value);
        CurrentEventType = _eventFilters.Find(EventType);
        if (!CurrentEventType.StatusOptions.Any(option => string.Equals(option.Value, Status, StringComparison.Ordinal)))
        {
            Status = AnyValue;
        }
    }

    public DashboardMetricQueryDefinition BuildQuery()
        => new(
            Normalize(Source, "runtimeEvents"),
            Normalize(Aggregation, "count"),
            Normalize(Window, "60s"),
            EventType: Normalize(EventType),
            TopicStartsWith: Normalize(TopicStartsWith),
            TopicNotStartsWith: Normalize(TopicNotStartsWith),
            Status: Normalize(Status),
            GroupBy: Normalize(GroupBy),
            Format: Normalize(Format, "number"),
            AdditionalFilters: BuildAdditionalFilters());

    public IReadOnlyDictionary<string, string> BuildAdditionalFilters()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(result, DashboardEventFilterCatalog.SubjectStartsWithKey, SubjectStartsWith);
        AddIfPresent(result, DashboardEventFilterCatalog.AttributeFilterKey("qos"), Qos);
        AddIfPresent(result, DashboardEventFilterCatalog.AttributeFilterKey("retain"), Retain);
        AddIfPresent(result, DashboardEventFilterCatalog.AttributeFilterKey("schemaId"), SchemaId);
        return result;
    }

    private static void AddIfPresent(Dictionary<string, string> target, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static string Normalize(string? value, string fallback = AnyValue)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
