using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MetricNode;

public sealed class MqttMetricsNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "mqtt.metrics", descriptor, isResource)
{
    public const int DefaultBoundedCapacity = 1000;
    public const double DefaultRateWindowSeconds = 60;
    public const int DefaultMetricCardColumns = 4;
    public const int MinMetricCardColumns = 1;
    public const int MaxMetricCardColumns = 4;
    public const string MetricMessages = "messages";
    public const string MetricCurrentRate = "currentRate";
    public const string MetricAverageRate = "averageRate";
    public const string MetricPayloadBytes = "payloadBytes";
    public const string MetricTopics = "topics";
    public const string MetricRetained = "retained";
    public const string MetricAveragePayload = "averagePayload";

    public static readonly IReadOnlyList<string> DefaultDisplayMetrics =
    [
        MetricMessages,
        MetricCurrentRate,
        MetricAverageRate,
        MetricPayloadBytes
    ];

    public static readonly IReadOnlySet<string> KnownDisplayMetrics = new HashSet<string>(
    [
        MetricMessages,
        MetricCurrentRate,
        MetricAverageRate,
        MetricPayloadBytes,
        MetricTopics,
        MetricRetained,
        MetricAveragePayload
    ], StringComparer.Ordinal);

    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;
    public double RateWindowSeconds { get; set; } = DefaultRateWindowSeconds;
    public int MetricCardColumns { get; set; } = DefaultMetricCardColumns;
    public List<string> DisplayMetrics { get; set; } = [.. DefaultDisplayMetrics];

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        BoundedCapacity = NormalizeBoundedCapacity(ReadInt(config, "boundedCapacity", DefaultBoundedCapacity));
        RateWindowSeconds = NormalizeRateWindowSeconds(ReadDouble(config, "rateWindowSeconds", DefaultRateWindowSeconds));
        DisplayMetrics = [.. NormalizeDisplayMetrics(ReadDisplayMetrics(config?["displayMetrics"]))];
        MetricCardColumns = NormalizeMetricCardColumns(ReadInt(config, "metricCardColumns", DefaultMetricCardColumns));
    }

    public override JsonObject BuildConfiguration()
    {
        var displayMetrics = NormalizeDisplayMetrics(DisplayMetrics);
        var cardColumns = NormalizeMetricCardColumns(MetricCardColumns);

        return new JsonObject
        {
            ["boundedCapacity"] = NormalizeBoundedCapacity(BoundedCapacity),
            ["rateWindowSeconds"] = NormalizeRateWindowSeconds(RateWindowSeconds),
            ["metricCardColumns"] = cardColumns,
            ["displayMetrics"] = BuildDisplayMetrics(displayMetrics)
        };
    }

    public static int NormalizeBoundedCapacity(int boundedCapacity)
        => boundedCapacity > 0 ? boundedCapacity : DefaultBoundedCapacity;

    public static double NormalizeRateWindowSeconds(double seconds)
        => seconds > 0 && double.IsFinite(seconds) ? seconds : DefaultRateWindowSeconds;

    public static int NormalizeMetricCardColumns(int columns)
        => Math.Clamp(columns, MinMetricCardColumns, MaxMetricCardColumns);

    public static IReadOnlyList<string> NormalizeDisplayMetrics(IEnumerable<string>? displayMetrics)
    {
        var normalized = (displayMetrics ?? [])
            .Where(metric => !string.IsNullOrWhiteSpace(metric))
            .Select(static metric => metric.Trim())
            .Where(KnownDisplayMetrics.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0 ? DefaultDisplayMetrics : normalized;
    }

    public static JsonArray BuildDisplayMetrics(IEnumerable<string>? displayMetrics)
    {
        var array = new JsonArray();
        foreach (var metric in NormalizeDisplayMetrics(displayMetrics))
        {
            array.Add(metric);
        }

        return array;
    }

    private static int ReadInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<int>(out var result))
        {
            return result;
        }

        return fallback;
    }

    private static double ReadDouble(JsonObject? config, string key, double fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<double>(out var result))
        {
            return result;
        }

        if (config?[key] is JsonValue intValue && intValue.TryGetValue<int>(out var intResult))
        {
            return intResult;
        }

        return fallback;
    }

    private static IReadOnlyList<string> ReadDisplayMetrics(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return DefaultDisplayMetrics;
        }

        return array
            .OfType<JsonValue>()
            .Select(static value => value.TryGetValue<string?>(out var metric) ? metric : null)
            .Where(static metric => !string.IsNullOrWhiteSpace(metric))
            .Select(static metric => metric!)
            .ToArray();
    }
}
