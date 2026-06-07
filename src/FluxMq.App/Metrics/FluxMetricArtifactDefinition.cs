using System.Collections.ObjectModel;

namespace FluxMq.App.Metrics;

public static class FluxMetricParameterTargets
{
    public const string Source = "source";
    public const string Measure = "measure";
    public const string Window = "window";
    public const string EventType = FluxMetricCatalog.EventTypeKey;
    public const string TopicStartsWith = FluxMetricCatalog.TopicStartsWithKey;
    public const string TopicNotStartsWith = FluxMetricCatalog.TopicNotStartsWithKey;
    public const string SubjectStartsWith = FluxMetricCatalog.SubjectStartsWithKey;
    public const string Status = FluxMetricCatalog.StatusKey;
    public const string Format = "format";
    public const string GroupBy = "groupBy";

    public static string Attribute(string name)
        => FluxMetricCatalog.AttributeFilterKey(name);
}

public static class FluxMetricParameterKinds
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Duration = "duration";
    public const string Select = "select";
    public const string Toggle = "toggle";
    public const string TopicPrefix = "topicPrefix";
}

public sealed record FluxMetricArtifactDefinition
{
    private List<FluxMetricParameterDefinition>? _parameters = [];
    private Dictionary<string, string>? _labels = [];

    public int Version { get; init; } = 1;

    public string DisplayName { get; init; } = "Metric";

    public string Description { get; init; } = string.Empty;

    public FluxMetricDefinition Definition { get; init; } =
        FluxMetricCatalog.Shared.CreateDefaultDefinition("Metric");

    public List<FluxMetricParameterDefinition> Parameters
    {
        get => _parameters ??= [];
        init => _parameters = value ?? [];
    }

    public Dictionary<string, string> Labels
    {
        get => _labels ??= [];
        init => _labels = value ?? [];
    }

    public FluxMetricExportPolicy ExportPolicy { get; init; } = FluxMetricExportPolicy.Disabled;

    public static FluxMetricArtifactDefinition CreateDefault(string id, string? displayName = null)
    {
        var name = string.IsNullOrWhiteSpace(displayName)
            ? FluxMetricNaming.ToDisplayName(id)
            : displayName.Trim();
        return new FluxMetricArtifactDefinition
        {
            DisplayName = name,
            Definition = FluxMetricCatalog.Shared.CreateDefaultDefinition(name)
        };
    }
}

public sealed record FluxMetricParameterDefinition
{
    private List<string>? _allowedValues = [];

    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Kind { get; init; } = FluxMetricParameterKinds.Text;

    public string Target { get; init; } = FluxMetricParameterTargets.TopicStartsWith;

    public string DefaultValue { get; init; } = string.Empty;

    public bool Required { get; init; }

    public List<string> AllowedValues
    {
        get => _allowedValues ??= [];
        init => _allowedValues = value ?? [];
    }
}

public sealed record FluxMetricParameterValue(string Id, string Value);

public sealed record FluxMetricReference
{
    private Dictionary<string, string>? _parameters = [];

    public string MetricId { get; init; } = string.Empty;

    public Dictionary<string, string> Parameters
    {
        get => _parameters ??= [];
        init => _parameters = value ?? [];
    }
}

public sealed class FluxMetricResolver
{
    public FluxMetricDefinition Resolve(
        FluxMetricArtifactDefinition artifact,
        IReadOnlyDictionary<string, string>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        if (artifact.Parameters.Count == 0)
        {
            return artifact.Definition;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in artifact.Parameters)
        {
            var value = parameterValues is not null &&
                parameterValues.TryGetValue(parameter.Id, out var provided)
                    ? provided
                    : parameter.DefaultValue;
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[parameter.Id] = value.Trim();
            }
        }

        var additional = new Dictionary<string, string>(artifact.Definition.AdditionalFilters, StringComparer.Ordinal);
        var definition = artifact.Definition;
        foreach (var parameter in artifact.Parameters)
        {
            values.TryGetValue(parameter.Id, out var value);
            definition = ApplyParameter(definition, additional, parameter.Target, value);
        }

        return definition with { AdditionalFilters = new ReadOnlyDictionary<string, string>(additional) };
    }

    public FluxMetricValidationResult ValidateReference(
        FluxMetricArtifactDefinition artifact,
        IReadOnlyDictionary<string, string>? parameterValues = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var errors = new List<string>();
        foreach (var parameter in artifact.Parameters)
        {
            var value = parameterValues is not null &&
                parameterValues.TryGetValue(parameter.Id, out var provided)
                    ? provided
                    : parameter.DefaultValue;
            if (parameter.Required && string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Metric parameter '{parameter.LabelOrId()}' is required.");
            }

            if (!string.IsNullOrWhiteSpace(value) &&
                parameter.AllowedValues.Count > 0 &&
                !parameter.AllowedValues.Contains(value.Trim(), StringComparer.Ordinal))
            {
                errors.Add($"Metric parameter '{parameter.LabelOrId()}' has unsupported value '{value}'.");
            }
        }

        return new FluxMetricValidationResult(errors);
    }

    private static FluxMetricDefinition ApplyParameter(
        FluxMetricDefinition definition,
        Dictionary<string, string> additional,
        string target,
        string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        switch (target)
        {
            case FluxMetricParameterTargets.Source:
                return normalized is null ? definition : definition with { Source = normalized };
            case FluxMetricParameterTargets.Measure:
                return normalized is null ? definition : definition with { Measure = normalized };
            case FluxMetricParameterTargets.Window:
                return normalized is null ? definition : definition with { Window = normalized };
            case FluxMetricParameterTargets.EventType:
                return definition with { EventType = normalized };
            case FluxMetricParameterTargets.TopicStartsWith:
                return definition with { TopicStartsWith = normalized };
            case FluxMetricParameterTargets.TopicNotStartsWith:
                return definition with { TopicNotStartsWith = normalized };
            case FluxMetricParameterTargets.SubjectStartsWith:
                SetOrRemove(additional, FluxMetricCatalog.SubjectStartsWithKey, normalized);
                return definition;
            case FluxMetricParameterTargets.Status:
                return definition with { Status = normalized };
            case FluxMetricParameterTargets.Format:
                return normalized is null ? definition : definition with { Format = normalized };
            case FluxMetricParameterTargets.GroupBy:
                return definition with { GroupBy = normalized };
            default:
                SetOrRemove(additional, target, normalized);
                return definition;
        }
    }

    private static void SetOrRemove(Dictionary<string, string> target, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            target.Remove(key);
        }
        else
        {
            target[key] = value.Trim();
        }
    }
}

public static class FluxMetricParameterDefinitionExtensions
{
    public static string LabelOrId(this FluxMetricParameterDefinition parameter)
        => string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Id : parameter.Label;
}

public static class FluxMetricNaming
{
    public static string ToArtifactId(string value)
    {
        var normalized = new string((value ?? string.Empty)
            .Trim()
            .Select(static c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')
            .ToArray())
            .Trim('.', '_', '-');
        return string.IsNullOrWhiteSpace(normalized) ? "metric" : normalized;
    }

    public static string ToDashboardScopedId(string dashboardName, string metricName)
    {
        var dashboardId = ToArtifactId(dashboardName);
        var metricId = ToArtifactId(metricName);
        var prefix = $"{dashboardId}.";
        return metricId.StartsWith(prefix, StringComparison.Ordinal)
            ? metricId
            : ToArtifactId($"{dashboardId}.{metricId}");
    }

    public static string RemoveDashboardScope(string dashboardName, string metricName)
    {
        var dashboardId = ToArtifactId(dashboardName);
        var metricId = ToArtifactId(metricName);
        var prefix = $"{dashboardId}.";
        return metricId.StartsWith(prefix, StringComparison.Ordinal) && metricId.Length > prefix.Length
            ? metricId[prefix.Length..]
            : metricId;
    }

    public static bool HasDashboardScope(string dashboardName, string metricName)
    {
        var dashboardId = ToArtifactId(dashboardName);
        var metricId = ToArtifactId(metricName);
        return metricId.StartsWith($"{dashboardId}.", StringComparison.Ordinal);
    }

    public static string ToDisplayName(string value)
    {
        var text = (value ?? string.Empty)
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim();
        return string.IsNullOrWhiteSpace(text) ? "Metric" : text;
    }
}
