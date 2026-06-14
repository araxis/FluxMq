using System.Globalization;
using FluxMq.App.Metrics;

namespace FluxMq.UI.Components.Workspace;

public sealed record MetricDesignerLatestValue(
    string FormattedValue,
    string Unit,
    DateTimeOffset Timestamp);

public sealed record MetricDesignerRow(
    string Id,
    string DisplayName,
    string TypeId,
    string TypeName,
    string Summary,
    string Unit,
    int ReferenceCount,
    MetricDesignerLatestValue? Latest);

public sealed class MetricDesignerDraft
{
    private readonly Dictionary<string, string> _originalParameters;
    private readonly string _originalTypeId;
    private readonly string _originalDisplayName;
    private readonly string _originalDescription;

    private MetricDesignerDraft(
        string originalId,
        string typeId,
        string displayName,
        string description,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string> labels,
        FluxMetricExportPolicy exportPolicy)
    {
        OriginalId = originalId;
        Id = originalId;
        TypeId = typeId;
        DisplayName = displayName;
        Description = description;
        Parameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        Labels = new Dictionary<string, string>(labels, StringComparer.Ordinal);
        ExportPolicy = exportPolicy;

        _originalTypeId = typeId;
        _originalDisplayName = displayName;
        _originalDescription = description;
        _originalParameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
    }

    public string OriginalId { get; }

    public string Id { get; set; }

    public string TypeId { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public Dictionary<string, string> Parameters { get; }

    public Dictionary<string, string> Labels { get; }

    public FluxMetricExportPolicy ExportPolicy { get; }

    public bool IsDirty =>
        !string.Equals(Id, OriginalId, StringComparison.Ordinal) ||
        !string.Equals(TypeId, _originalTypeId, StringComparison.Ordinal) ||
        !string.Equals(DisplayName, _originalDisplayName, StringComparison.Ordinal) ||
        !string.Equals(Description, _originalDescription, StringComparison.Ordinal) ||
        !DictionaryEquals(Parameters, _originalParameters);

    public static MetricDesignerDraft FromResource(string id, FluxMetricResourceDefinition resource)
        => new(
            id,
            resource.TypeId,
            resource.DisplayName,
            resource.Description,
            resource.Parameters,
            resource.Labels,
            resource.ExportPolicy);

    public string ParameterValue(MetricParamSpec parameter)
        => Parameters.TryGetValue(parameter.Key, out var value)
            ? value
            : parameter.DefaultValue ?? string.Empty;

    public void SetType(MetricDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        TypeId = descriptor.TypeId;
        Parameters.Clear();
        foreach (var (key, value) in MetricDesignerState.DefaultParameters(descriptor))
        {
            Parameters[key] = value;
        }
    }

    public FluxMetricResourceDefinition ToResource(IFluxMetricCatalog catalog)
    {
        var parameters = new Dictionary<string, string>(Parameters, StringComparer.Ordinal);
        if (catalog.Describe(TypeId) is { } descriptor)
        {
            foreach (var parameter in descriptor.Parameters)
            {
                var value = NormalizeParameterValue(parameter, ParameterValue(parameter));
                if (string.IsNullOrWhiteSpace(value))
                {
                    parameters.Remove(parameter.Key);
                }
                else
                {
                    parameters[parameter.Key] = value;
                }
            }
        }

        return new FluxMetricResourceDefinition
        {
            Id = Id.Trim(),
            TypeId = TypeId.Trim(),
            DisplayName = DisplayName.Trim(),
            Description = Description.Trim(),
            Parameters = parameters,
            Labels = new Dictionary<string, string>(Labels, StringComparer.Ordinal),
            ExportPolicy = ExportPolicy
        };
    }

    private static string NormalizeParameterValue(MetricParamSpec parameter, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return parameter.Kind == MetricParamKind.Duration &&
               MetricWindow.TryNormalize(trimmed, out var normalized)
            ? normalized
            : trimmed;
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) ||
                !string.Equals(value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

public static class MetricDesignerState
{
    public delegate bool TryGetLatestMetricValue(
        string metricId,
        FluxMetricResourceDefinition resource,
        out MetricDesignerLatestValue latest);

    public static IReadOnlyList<MetricDesignerRow> BuildRows(
        IReadOnlyDictionary<string, FluxMetricResourceDefinition> metrics,
        IFluxMetricCatalog catalog,
        Func<string, int> referenceCounter,
        TryGetLatestMetricValue? latestResolver = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(referenceCounter);

        return
        [
            .. metrics
                .Select(metric => BuildRow(metric.Key, metric.Value, catalog, referenceCounter, latestResolver))
                .OrderBy(static row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.Id, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public static IReadOnlyList<MetricDesignerRow> ApplyFilter(
        IEnumerable<MetricDesignerRow> rows,
        string? search,
        string? typeId)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var normalizedSearch = search?.Trim();
        var normalizedType = typeId?.Trim();
        return
        [
            .. rows.Where(row =>
                (string.IsNullOrWhiteSpace(normalizedType) ||
                 string.Equals(row.TypeId, normalizedType, StringComparison.Ordinal)) &&
                (string.IsNullOrWhiteSpace(normalizedSearch) ||
                 Contains(row.Id, normalizedSearch) ||
                 Contains(row.DisplayName, normalizedSearch) ||
                 Contains(row.TypeName, normalizedSearch) ||
                 Contains(row.Summary, normalizedSearch)))
        ];
    }

    public static string MetricCountText(int total, int filtered, bool hasActiveFilters)
    {
        if (total <= 0)
        {
            return "No metrics";
        }

        var totalText = Pluralize(total, "metric");
        if (!hasActiveFilters || filtered == total)
        {
            return totalText;
        }

        return $"{Math.Max(0, filtered)} of {totalText}";
    }

    public static string FilterEmptyDescription(string? search, string? typeName)
    {
        var searchText = search?.Trim();
        var typeText = typeName?.Trim();
        var hasSearch = !string.IsNullOrWhiteSpace(searchText);
        var hasType = !string.IsNullOrWhiteSpace(typeText);

        return (hasSearch, hasType) switch
        {
            (true, true) => $"No metrics use {typeText} and match \"{searchText}\".",
            (true, false) => $"No metrics match \"{searchText}\".",
            (false, true) => $"No metrics use {typeText}.",
            _ => "No metrics match the current filters."
        };
    }

    public static IReadOnlyDictionary<string, string> DefaultParameters(MetricDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in descriptor.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
            {
                parameters[parameter.Key] = parameter.DefaultValue.Trim();
            }
        }

        return parameters;
    }

    public static string UniqueMetricId(string preferred, IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(existingIds);

        var existing = existingIds.ToHashSet(StringComparer.Ordinal);
        var id = FluxMetricNaming.ToArtifactId(preferred);
        if (!existing.Contains(id))
        {
            return id;
        }

        var index = 2;
        while (existing.Contains($"{id}{index}"))
        {
            index++;
        }

        return $"{id}{index}";
    }

    public static IReadOnlyList<string> ValidateDraft(
        MetricDesignerDraft draft,
        IFluxMetricCatalog catalog,
        IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(existingIds);

        var errors = new List<string>();
        if (ValidateMetricId(draft, existingIds) is { } idError)
        {
            errors.Add(idError);
        }

        if (ValidateDisplayName(draft) is { } displayNameError)
        {
            errors.Add(displayNameError);
        }

        if (catalog.Describe(draft.TypeId) is not { } descriptor)
        {
            errors.Add("Metric type is not registered.");
            return errors;
        }

        foreach (var parameter in descriptor.Parameters)
        {
            AddParameterValidationError(draft, parameter, errors);
        }

        return errors;
    }

    public static string? ValidateMetricId(MetricDesignerDraft draft, IEnumerable<string> existingIds)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(existingIds);

        var id = draft.Id.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "Metric id is required.";
        }

        if (!string.Equals(id, FluxMetricNaming.ToArtifactId(id), StringComparison.Ordinal))
        {
            return "Metric id can only use letters, numbers, dots, dashes, and underscores.";
        }

        return !string.Equals(id, draft.OriginalId, StringComparison.Ordinal) &&
               existingIds.Contains(id, StringComparer.Ordinal)
            ? $"Metric id '{id}' already exists."
            : null;
    }

    public static string? ValidateDisplayName(MetricDesignerDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return string.IsNullOrWhiteSpace(draft.DisplayName)
            ? "Display name is required."
            : null;
    }

    public static string? ValidateParameter(MetricDesignerDraft draft, MetricParamSpec parameter)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(parameter);

        var errors = new List<string>(capacity: 1);
        AddParameterValidationError(draft, parameter, errors);
        return errors.Count == 0 ? null : errors[0];
    }

    public static string ParameterSummary(
        MetricDescriptor? descriptor,
        IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (descriptor is null)
        {
            return "Unknown metric type";
        }

        var parts = new List<string>();
        foreach (var parameter in descriptor.Parameters)
        {
            var value = parameters.TryGetValue(parameter.Key, out var stored) && !string.IsNullOrWhiteSpace(stored)
                ? stored.Trim()
                : parameter.DefaultValue?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            parts.Add(ParameterPart(parameter, value));
        }

        return parts.Count == 0 ? "No parameters" : string.Join(" · ", parts);
    }

    private static MetricDesignerRow BuildRow(
        string id,
        FluxMetricResourceDefinition resource,
        IFluxMetricCatalog catalog,
        Func<string, int> referenceCounter,
        TryGetLatestMetricValue? latestResolver)
    {
        var descriptor = catalog.Describe(resource.TypeId);
        var latest = latestResolver is not null &&
                     latestResolver(id, resource, out var reading)
            ? reading
            : null;

        return new MetricDesignerRow(
            id,
            string.IsNullOrWhiteSpace(resource.DisplayName) ? FluxMetricNaming.ToDisplayName(id) : resource.DisplayName,
            resource.TypeId,
            descriptor?.DisplayName ?? (string.IsNullOrWhiteSpace(resource.TypeId) ? "Unknown type" : resource.TypeId),
            ParameterSummary(descriptor, resource.Parameters),
            descriptor?.Unit ?? string.Empty,
            referenceCounter(id),
            latest);
    }

    private static void AddParameterValidationError(
        MetricDesignerDraft draft,
        MetricParamSpec parameter,
        ICollection<string> errors)
    {
        var value = draft.ParameterValue(parameter).Trim();
        if (parameter.Required && string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{parameter.DisplayName} is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (parameter.Kind == MetricParamKind.Integer)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                errors.Add($"{parameter.DisplayName} must be a whole number.");
                return;
            }

            ValidateRange(parameter, parsed, errors);
        }
        else if (parameter.Kind == MetricParamKind.Number)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                errors.Add($"{parameter.DisplayName} must be a number.");
                return;
            }

            ValidateRange(parameter, parsed, errors);
        }
        else if (parameter.Kind == MetricParamKind.Duration &&
                 !MetricWindow.TryNormalize(value, out _))
        {
            errors.Add($"{parameter.DisplayName} must be between 1s and 24h, for example 30s, 1m, or 2h.");
        }
        else if (parameter.Kind == MetricParamKind.Select &&
                 parameter.Options is { Count: > 0 } options &&
                 !options.Any(option => string.Equals(option.Value, value, StringComparison.Ordinal)))
        {
            errors.Add($"{parameter.DisplayName} must be one of the listed values.");
        }
        else if (parameter.Kind == MetricParamKind.Toggle &&
                 value is not ("true" or "false"))
        {
            errors.Add($"{parameter.DisplayName} must be Default, On, or Off.");
        }
    }

    private static void ValidateRange(MetricParamSpec parameter, double value, ICollection<string> errors)
    {
        if (parameter.Min is { } min && value < min)
        {
            errors.Add($"{parameter.DisplayName} must be at least {min.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (parameter.Max is { } max && value > max)
        {
            errors.Add($"{parameter.DisplayName} must be at most {max.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static string ParameterPart(MetricParamSpec parameter, string value)
        => parameter.Kind switch
        {
            MetricParamKind.Topic => value,
            MetricParamKind.Duration => $"Window {value}",
            MetricParamKind.Integer when parameter.Key.Contains("qos", StringComparison.OrdinalIgnoreCase) => $"QoS {value}",
            MetricParamKind.Toggle => $"{parameter.DisplayName} {ToggleLabel(value)}",
            _ => $"{parameter.DisplayName} {value}"
        };

    private static string ToggleLabel(string value)
        => value switch
        {
            "true" => "On",
            "false" => "Off",
            _ => "Default"
        };

    private static bool Contains(string value, string search)
        => value.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static string Pluralize(int count, string singular)
        => count == 1 ? $"1 {singular}" : $"{count} {singular}s";
}
