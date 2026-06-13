using FluxFlow.Engine.Components;

namespace FluxMq.App.Metrics;

/// <summary>
/// Reads typed values out of a metric's stored string parameters. Each metric uses this in its
/// <c>Create</c> to turn the persisted bag into its own typed fields — no reflection, no shared base.
/// </summary>
public sealed class MetricParamReader
{
    private readonly IReadOnlyDictionary<string, string> _parameters;

    public MetricParamReader(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _parameters = parameters;
    }

    public string? GetString(string key)
        => _parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    public int GetInt(string key, int fallback)
        => GetString(key) is { } text && int.TryParse(text, out var value) ? value : fallback;

    public bool GetBool(string key, bool fallback)
        => GetString(key) is { } text && bool.TryParse(text, out var value) ? value : fallback;
}

/// <summary>Matches an event's MQTT QoS attribute against a metric's QoS parameter (missing QoS is treated as 0).</summary>
public static class MetricQos
{
    public static bool Matches(FlowEvent flowEvent, int qos)
    {
        var actual = int.TryParse(flowEvent.GetAttribute("qos"), out var value) ? value : 0;
        return actual == qos;
    }
}
