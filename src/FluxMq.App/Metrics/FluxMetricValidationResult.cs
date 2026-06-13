namespace FluxMq.App.Metrics;

/// <summary>
/// The result of validating a metric resource against its type.
/// </summary>
public sealed record FluxMetricValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
