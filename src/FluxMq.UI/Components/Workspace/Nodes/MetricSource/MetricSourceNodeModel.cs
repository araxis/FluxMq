using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.MetricSource;

public sealed class MetricSourceNodeModel(string id, DiagramPoint position, string nodeName, FlowComponentDescriptor? descriptor, bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "metric.source", descriptor, isResource)
{
    private readonly Dictionary<string, string> _parameters = new(StringComparer.Ordinal);

    public string MetricId { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> Parameters => _parameters;

    public bool EmitLatestOnStart { get; set; } = true;

    public int BoundedCapacity { get; set; } = 1000;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        MetricId = ReadString(config, "metricId", string.Empty);
        EmitLatestOnStart = ReadBool(config, "emitLatestOnStart", true);
        BoundedCapacity = Math.Max(1, ReadInt(config, "boundedCapacity", 1000));
        _parameters.Clear();
        if (config?["parameters"] is JsonObject parameters)
        {
            foreach (var parameter in parameters)
            {
                if (TryReadString(parameter.Value, out var value))
                {
                    _parameters[parameter.Key] = value;
                }
            }
        }
    }

    public void SetParameters(IReadOnlyDictionary<string, string> parameters)
    {
        _parameters.Clear();
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                _parameters[key.Trim()] = value.Trim();
            }
        }
    }

    public override JsonObject BuildConfiguration()
    {
        var parameters = new JsonObject();
        foreach (var (key, value) in _parameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            parameters[key] = value;
        }

        return new JsonObject
        {
            ["metricId"] = MetricId.Trim(),
            ["parameters"] = parameters,
            ["emitLatestOnStart"] = EmitLatestOnStart,
            ["boundedCapacity"] = Math.Max(1, BoundedCapacity)
        };
    }

    public override string ResolvePortValueType(ComponentPortDescriptor descriptor)
        => !descriptor.IsInput && string.Equals(descriptor.Name, "Output", StringComparison.OrdinalIgnoreCase)
            ? "NumberMetricReading"
            : base.ResolvePortValueType(descriptor);

    private static string ReadString(JsonObject? config, string key, string fallback)
        => TryReadString(config?[key], out var value) ? value : fallback;

    private static bool ReadBool(JsonObject? config, string key, bool fallback)
    {
        if (config?[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        return value.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsed)
            ? parsed
            : fallback;
    }

    private static int ReadInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<int>(out var integer))
        {
            return integer;
        }

        return value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool TryReadString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text.Trim();
            return value.Length > 0;
        }

        if (jsonValue.TryGetValue<bool>(out var boolean))
        {
            value = boolean ? "true" : "false";
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var integer))
        {
            value = integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (jsonValue.TryGetValue<double>(out var number))
        {
            value = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }
}
