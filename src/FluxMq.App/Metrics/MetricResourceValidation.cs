using System.Text;

namespace FluxMq.App.Metrics;

/// <summary>
/// Generic validation shared by metric types: required-parameter presence, allowed-value membership, and
/// duration parsing. A type calls this from its <see cref="IFluxMetricType{TValue}.Validate"/> and may add
/// type-specific checks on top.
/// </summary>
internal static class MetricResourceValidation
{
    public static FluxMetricValidationResult Validate(
        IReadOnlyList<FluxMetricParameterDescriptor> parameters,
        FluxMetricResourceDefinition resource)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(resource);

        var errors = new List<string>();
        foreach (var descriptor in parameters)
        {
            var value = resource.GetParameter(descriptor.Key, descriptor.DefaultValue);
            if (descriptor.Required && string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Metric parameter '{descriptor.Label}' is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (descriptor.Options.Count > 0 &&
                !descriptor.Options.Any(option => string.Equals(option.Value, value.Trim(), StringComparison.Ordinal)))
            {
                errors.Add($"Metric parameter '{descriptor.Label}' has unsupported value '{value}'.");
            }

            if (string.Equals(descriptor.Kind, FluxMetricParameterKinds.Duration, StringComparison.Ordinal) &&
                !MetricWindow.TryNormalize(value, out _))
            {
                errors.Add("Window must be a positive duration between 1s and 24h.");
            }
        }

        return new FluxMetricValidationResult(errors);
    }
}

/// <summary>
/// Produces a short human-readable description of a metric resource from its type and active parameters.
/// </summary>
internal static class MetricResourceSummary
{
    public static string Describe(IFluxMetricType<double> type, FluxMetricResourceDefinition resource)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resource);

        var builder = new StringBuilder(type.DisplayName);
        var filters = new List<string>();
        foreach (var descriptor in type.Parameters)
        {
            var value = resource.GetParameter(descriptor.Key);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (string.Equals(descriptor.Key, MetricParameterKeys.Window, StringComparison.Ordinal))
            {
                builder.Append(" over ").Append(value);
                continue;
            }

            filters.Add($"{descriptor.Label.ToLowerInvariant()} {value}");
        }

        if (filters.Count > 0)
        {
            builder.Append(" where ").Append(string.Join(", ", filters));
        }

        return builder.ToString();
    }
}
