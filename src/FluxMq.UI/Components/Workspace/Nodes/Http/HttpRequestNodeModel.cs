using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.Http;

public sealed class HttpRequestNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "http.request", descriptor, isResource)
{
    public const int DefaultTimeoutMilliseconds = 30_000;
    public const int DefaultMaxResponseBodyBytes = 1_048_576;
    public const int DefaultBoundedCapacity = 128;

    public string BaseUrl { get; set; } = string.Empty;
    public string DefaultHeadersText { get; set; } = string.Empty;
    public int DefaultTimeoutMillisecondsValue { get; set; } = DefaultTimeoutMilliseconds;
    public int MaxResponseBodyBytes { get; set; } = DefaultMaxResponseBodyBytes;
    public bool FollowRedirects { get; set; } = true;
    public bool TreatNonSuccessStatusAsError { get; set; }
    public int BoundedCapacity { get; set; } = DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        BaseUrl = ReadString(config, "baseUrl", string.Empty);
        DefaultHeadersText = ReadHeadersText(config);
        DefaultTimeoutMillisecondsValue = ReadPositiveInt(config, "defaultTimeoutMilliseconds", DefaultTimeoutMilliseconds);
        MaxResponseBodyBytes = ReadPositiveInt(config, "maxResponseBodyBytes", DefaultMaxResponseBodyBytes);
        FollowRedirects = ReadBool(config, "followRedirects", true);
        TreatNonSuccessStatusAsError = ReadBool(config, "treatNonSuccessStatusAsError", false);
        BoundedCapacity = ReadPositiveInt(config, "boundedCapacity", DefaultBoundedCapacity);
    }

    public override JsonObject BuildConfiguration()
    {
        var config = new JsonObject
        {
            ["defaultTimeoutMilliseconds"] = NormalizePositiveInt(DefaultTimeoutMillisecondsValue, DefaultTimeoutMilliseconds),
            ["maxResponseBodyBytes"] = NormalizePositiveInt(MaxResponseBodyBytes, DefaultMaxResponseBodyBytes),
            ["followRedirects"] = FollowRedirects,
            ["treatNonSuccessStatusAsError"] = TreatNonSuccessStatusAsError,
            ["boundedCapacity"] = NormalizePositiveInt(BoundedCapacity, DefaultBoundedCapacity)
        };

        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            config["baseUrl"] = BaseUrl.Trim();
        }

        var headers = ParseHeaders(DefaultHeadersText);
        if (headers.Count > 0)
        {
            config["defaultHeaders"] = BuildHeadersObject(headers);
        }

        return config;
    }

    public static Dictionary<string, string> ParseHeaders(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        foreach (var rawLine in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var headerValue = line[(separatorIndex + 1)..].Trim();
            if (name.Length > 0)
            {
                result[name] = headerValue;
            }
        }

        return result;
    }

    public static int NormalizePositiveInt(int value, int fallback)
        => value > 0 ? value : fallback;

    private static JsonObject BuildHeadersObject(IReadOnlyDictionary<string, string> headers)
    {
        var result = new JsonObject();
        foreach (var (name, value) in headers)
        {
            result[name] = value;
        }

        return result;
    }

    private static string ReadHeadersText(JsonObject? config)
    {
        if (config?["defaultHeaders"] is not JsonObject headers)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            headers
                .Select(header => $"{header.Key}: {ReadJsonValue(header.Value)}")
                .Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string ReadJsonValue(JsonNode? node)
        => node switch
        {
            null => string.Empty,
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => node.ToJsonString()
        };

    private static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    private static bool ReadBool(JsonObject? config, string key, bool fallback)
        => config?[key] is JsonValue value && value.TryGetValue<bool>(out var result)
            ? result
            : fallback;

    private static int ReadPositiveInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value &&
            value.TryGetValue<int>(out var result) &&
            result > 0)
        {
            return result;
        }

        if (config?[key] is JsonValue textValue &&
            textValue.TryGetValue<string>(out var text) &&
            int.TryParse(text, out result) &&
            result > 0)
        {
            return result;
        }

        return fallback;
    }
}
