using System.Text.Json;
using System.Text.Json.Nodes;
using FluxMq.App.Definitions;
using FluxMq.App.Metrics;
using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    private static readonly JsonSerializerOptions MetricJsonOptions = FluxMqApplicationDefinitionJson.CreateSerializerOptions();

    public IReadOnlyList<string> GetMetricNames(string json)
        => GetNamedObjectKeys(json, "metrics");

    public IReadOnlyDictionary<string, FluxMetricResourceDefinition> GetMetricResources(string json)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = flowApplication["metrics"] as JsonObject ?? new JsonObject();
        return ReadMetricResources(metrics);
    }

    public FluxMetricResourceDefinition? GetMetricResource(string json, string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return null;
        }

        return GetMetricResources(json).TryGetValue(metricName.Trim(), out var metric)
            ? metric
            : null;
    }

    public string AddMetric(string json, string preferredName)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = GetOrCreateObject(flowApplication, "metrics");
        var metricName = MakeUniqueMetricName(metrics, FluxMetricNaming.ToArtifactId(preferredName));
        metrics[metricName] = CreateMetricResourceNode(FluxMetricResourceDefinition.CreateDefault(metricName, preferredName));
        return root.ToJsonString(Options);
    }

    public string UpdateMetric(string json, string metricName, FluxMetricResourceDefinition metric)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return json;
        }

        ArgumentNullException.ThrowIfNull(metric);

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = GetOrCreateObject(flowApplication, "metrics");
        metrics[metricName.Trim()] = CreateMetricResourceNode(metric);
        return root.ToJsonString(Options);
    }

    public string DuplicateMetric(string json, string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = GetOrCreateObject(flowApplication, "metrics");
        var sourceName = metricName.Trim();
        if (metrics[sourceName] is not JsonObject source)
        {
            return json;
        }

        var copyName = MakeUniqueMetricName(metrics, $"{sourceName}Copy");
        var resource = source.Deserialize<FluxMetricResourceDefinition>(MetricJsonOptions)
            ?? FluxMetricResourceDefinition.CreateDefault(copyName);
        metrics[copyName] = CreateMetricResourceNode(resource with
        {
            DisplayName = $"{resource.DisplayName} Copy"
        });
        return root.ToJsonString(Options);
    }

    public string RenameMetric(string json, string currentName, string nextName)
    {
        if (string.IsNullOrWhiteSpace(currentName) ||
            string.IsNullOrWhiteSpace(nextName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["metrics"] is not JsonObject metrics)
        {
            return json;
        }

        var from = currentName.Trim();
        var to = FluxMetricNaming.ToArtifactId(nextName);
        if (string.Equals(from, to, StringComparison.Ordinal) ||
            metrics[from] is not JsonObject source ||
            metrics.ContainsKey(to))
        {
            return json;
        }

        metrics[to] = source.DeepClone();
        metrics.Remove(from);
        UpdateMetricReferences(flowApplication, from, to);
        return root.ToJsonString(Options);
    }

    public string RemoveMetric(string json, string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["metrics"] is JsonObject metrics)
        {
            metrics.Remove(metricName.Trim());
        }

        return root.ToJsonString(Options);
    }

    public int CountMetricReferences(string json, string metricName)
    {
        return GetMetricReferenceSummaries(json, metricName).Count;
    }

    public IReadOnlyList<MetricReferenceSummary> GetMetricReferenceSummaries(string json, string metricName)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return [];
        }

        if (flowApplication["dashboards"] is not JsonObject dashboards)
        {
            return [];
        }

        var summaries = new List<MetricReferenceSummary>();
        var normalizedMetricName = metricName.Trim();
        foreach (var dashboard in dashboards)
        {
            if (dashboard.Value is not JsonObject dashboardObject ||
                dashboardObject["bindings"] is not JsonObject bindings)
            {
                continue;
            }

            var widgets = dashboardObject["widgets"] as JsonObject ?? new JsonObject();
            foreach (var binding in bindings)
            {
                if (binding.Value is not JsonObject bindingObject ||
                    !BindingUsesMetric(bindingObject, normalizedMetricName))
                {
                    continue;
                }

                var widgetName = binding.Key;
                var widgetType = widgets[widgetName] is JsonObject widget
                    ? ReadString(widget, "type") ?? string.Empty
                    : string.Empty;
                var cell = FindDashboardCellForWidget(dashboardObject, widgetName);
                summaries.Add(new MetricReferenceSummary(
                    dashboard.Key,
                    widgetName,
                    widgetType,
                    string.Equals(ReadString(bindingObject, "primaryMetric"), normalizedMetricName, StringComparison.Ordinal),
                    cell.HasValue ? cell.Value.Name : null,
                    cell.HasValue ? cell.Value.Label : null));
            }
        }

        return summaries;
    }

    private static (string Name, string Label)? FindDashboardCellForWidget(JsonObject dashboard, string widgetName)
    {
        if (string.IsNullOrWhiteSpace(widgetName) ||
            dashboard["layout"] is not JsonObject layout ||
            layout["cells"] is not JsonObject cells)
        {
            return null;
        }

        foreach (var cell in ReadDashboardCells(cells))
        {
            if (string.Equals(cell.Widget, widgetName, StringComparison.Ordinal))
            {
                return (cell.Name, DashboardCellLocationLabel(cell));
            }
        }

        return null;
    }

    private static string DashboardCellLocationLabel(DashboardCellSnapshot cell)
    {
        var origin = $"R{cell.Row + 1} C{cell.Column + 1}";
        return cell.IsMerged
            ? $"{origin} / {cell.ColumnSpan} x {cell.RowSpan}"
            : origin;
    }

    internal static IReadOnlyDictionary<string, FluxMetricResourceDefinition> ReadMetricResources(JsonObject metrics)
    {
        var result = new Dictionary<string, FluxMetricResourceDefinition>(StringComparer.Ordinal);
        foreach (var metric in metrics)
        {
            if (metric.Value is not JsonObject metricObject)
            {
                continue;
            }

            try
            {
                var resource = metricObject.Deserialize<FluxMetricResourceDefinition>(MetricJsonOptions);
                if (resource is not null)
                {
                    result[metric.Key] = resource with { Id = metric.Key };
                }
            }
            catch (JsonException)
            {
                // Validation reports invalid app metrics; layout reads skip malformed resources.
            }
        }

        return result;
    }

    private static JsonObject CreateMetricResourceNode(FluxMetricResourceDefinition metric)
        => JsonSerializer.SerializeToNode(metric, MetricJsonOptions) as JsonObject ?? new JsonObject();

    private static string MakeUniqueMetricName(JsonObject metrics, string preferred)
    {
        var normalized = FluxMetricNaming.ToArtifactId(preferred);
        if (!metrics.ContainsKey(normalized))
        {
            return normalized;
        }

        var index = 2;
        while (metrics.ContainsKey($"{normalized}{index}"))
        {
            index++;
        }

        return $"{normalized}{index}";
    }

    private static void UpdateMetricReferences(JsonObject flowApplication, string from, string to)
    {
        if (flowApplication["dashboards"] is not JsonObject dashboards)
        {
            return;
        }

        foreach (var dashboard in dashboards)
        {
            if (dashboard.Value is not JsonObject dashboardObject ||
                dashboardObject["bindings"] is not JsonObject bindings)
            {
                continue;
            }

            foreach (var binding in bindings)
            {
                if (binding.Value is not JsonObject bindingObject)
                {
                    continue;
                }

                if (string.Equals(ReadString(bindingObject, "primaryMetric"), from, StringComparison.Ordinal))
                {
                    bindingObject["primaryMetric"] = to;
                }

                if (bindingObject["metrics"] is not JsonArray metrics)
                {
                    continue;
                }

                for (var index = 0; index < metrics.Count; index++)
                {
                    if (metrics[index] is JsonValue value &&
                        value.TryGetValue<string>(out var name) &&
                        string.Equals(name, from, StringComparison.Ordinal))
                    {
                        metrics[index] = to;
                    }
                }
            }
        }
    }
}
