using System.Text.Json;
using System.Text.Json.Nodes;
using FluxMq.App.Definitions;
using FluxMq.App.Metrics;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    private static readonly JsonSerializerOptions MetricJsonOptions = FluxMqApplicationDefinitionJson.CreateSerializerOptions();

    public IReadOnlyList<string> GetMetricNames(string json)
        => GetNamedObjectKeys(json, "metrics");

    public IReadOnlyDictionary<string, FluxMetricArtifactDefinition> GetMetricArtifacts(string json)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = flowApplication["metrics"] as JsonObject ?? new JsonObject();
        return ReadMetricArtifacts(metrics);
    }

    public FluxMetricArtifactDefinition? GetMetricArtifact(string json, string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return null;
        }

        return GetMetricArtifacts(json).TryGetValue(metricName.Trim(), out var metric)
            ? metric
            : null;
    }

    public string AddMetric(string json, string preferredName)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = GetOrCreateObject(flowApplication, "metrics");
        var metricName = MakeUniqueMetricName(metrics, FluxMetricNaming.ToArtifactId(preferredName));
        metrics[metricName] = CreateMetricArtifactNode(FluxMetricArtifactDefinition.CreateDefault(metricName, preferredName));
        return root.ToJsonString(Options);
    }

    public string UpdateMetric(string json, string metricName, FluxMetricArtifactDefinition metric)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return json;
        }

        ArgumentNullException.ThrowIfNull(metric);

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var metrics = GetOrCreateObject(flowApplication, "metrics");
        metrics[metricName.Trim()] = CreateMetricArtifactNode(metric);
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
        var artifact = source.Deserialize<FluxMetricArtifactDefinition>(MetricJsonOptions)
            ?? FluxMetricArtifactDefinition.CreateDefault(copyName);
        metrics[copyName] = CreateMetricArtifactNode(artifact with
        {
            DisplayName = $"{artifact.DisplayName} Copy",
            Definition = artifact.Definition with { Name = $"{artifact.Definition.Name} Copy" }
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
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return 0;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["dashboards"] is not JsonObject dashboards)
        {
            return 0;
        }

        var count = 0;
        foreach (var dashboard in dashboards)
        {
            if (dashboard.Value is not JsonObject dashboardObject ||
                dashboardObject["bindings"] is not JsonObject bindings)
            {
                continue;
            }

            count += bindings
                .Select(static binding => binding.Value as JsonObject)
                .Where(static binding => binding is not null)
                .Cast<JsonObject>()
                .Count(binding => BindingUsesMetric(binding, metricName.Trim()));
        }

        return count;
    }

    private static IReadOnlyDictionary<string, FluxMetricArtifactDefinition> ReadMetricArtifacts(JsonObject metrics)
    {
        var result = new Dictionary<string, FluxMetricArtifactDefinition>(StringComparer.Ordinal);
        foreach (var metric in metrics)
        {
            if (metric.Value is not JsonObject metricObject)
            {
                continue;
            }

            try
            {
                var artifact = metricObject.Deserialize<FluxMetricArtifactDefinition>(MetricJsonOptions);
                if (artifact is not null)
                {
                    result[metric.Key] = artifact;
                }
            }
            catch (JsonException)
            {
                // Validation reports invalid app metrics; layout reads skip malformed artifacts.
            }
        }

        return result;
    }

    private static JsonObject CreateMetricArtifactNode(FluxMetricArtifactDefinition metric)
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
