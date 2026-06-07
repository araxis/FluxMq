namespace FluxMq.App.Metrics;

public sealed record FluxMetricValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class FluxMetricValidator
{
    private readonly FluxMetricCatalog _catalog;

    public FluxMetricValidator(FluxMetricCatalog? catalog = null)
    {
        _catalog = catalog ?? FluxMetricCatalog.Shared;
    }

    public FluxMetricValidationResult Validate(FluxMetricDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();
        if (!_catalog.IsSourceCompatible(definition.Measure, definition.Source))
        {
            errors.Add($"Source '{definition.Source}' is not compatible with measure '{definition.Measure}'.");
        }

        if (!FluxMetricQueryDraft.TryNormalizeWindow(definition.Window, out _))
        {
            errors.Add("Window must be a positive duration between 1s and 24h.");
        }

        if (definition.Mode == MetricDefinitionMode.Expression)
        {
            errors.Add("Expression metrics are reserved and are not enabled yet.");
        }

        var eventType = _catalog.FindEventType(definition.EventType);
        if (!string.IsNullOrWhiteSpace(definition.Status) &&
            !eventType.StatusOptions.Any(option => string.Equals(option.Value, definition.Status, StringComparison.Ordinal)))
        {
            errors.Add($"Status '{definition.Status}' is not supported by event type '{eventType.Label}'.");
        }

        return new FluxMetricValidationResult(errors);
    }
}
