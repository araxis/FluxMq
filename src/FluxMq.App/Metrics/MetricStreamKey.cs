using System.Globalization;
using System.Text;

namespace FluxMq.App.Metrics;

/// <summary>
/// Identity for a cached metric source: a resource id plus its resolved parameter values. Two requests with the
/// same id and the same effective parameters share one running stream.
/// </summary>
public sealed class MetricStreamKey : IEquatable<MetricStreamKey>
{
    private readonly string _identity;

    private MetricStreamKey(string metricId, IReadOnlyDictionary<string, string> parameters)
    {
        MetricId = metricId;
        Parameters = parameters;

        // Length-prefix each key/value so the flattened identity is unambiguous: {"a":"bc"} and {"ab":"c"}
        // produce different strings ("1:a2:bc" versus "2:ab1:c").
        var builder = new StringBuilder();
        foreach (var (key, value) in parameters)
        {
            Append(builder, key);
            Append(builder, value);
        }

        _identity = builder.ToString();
    }

    public string MetricId { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public static MetricStreamKey Create(string metricId, IReadOnlyDictionary<string, string>? parameters)
    {
        var id = metricId.Trim();
        var values = parameters is null || parameters.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : parameters
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    static pair => pair.Key.Trim(),
                    static pair => pair.Value.Trim(),
                    StringComparer.Ordinal);

        return new MetricStreamKey(id, values);
    }

    public bool Equals(MetricStreamKey? other)
        => other is not null &&
           string.Equals(MetricId, other.MetricId, StringComparison.Ordinal) &&
           string.Equals(_identity, other._identity, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is MetricStreamKey other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.Ordinal.GetHashCode(MetricId), StringComparer.Ordinal.GetHashCode(_identity));

    private static void Append(StringBuilder builder, string value)
        => builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
}
