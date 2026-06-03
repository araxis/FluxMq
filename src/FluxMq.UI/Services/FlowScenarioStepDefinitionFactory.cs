using FluxMq.Scenarios;
using FluxMq.UI.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public static class FlowScenarioStepDefinitionFactory
{
    public static JsonObject CreateStep(JsonObject flowApplication, string stepType)
        => new()
        {
            ["type"] = stepType,
            ["configuration"] = CreateConfiguration(
                stepType,
                CreateDefaultConfiguration(flowApplication, stepType))
        };

    public static JsonObject CreateConfiguration(
        string stepType,
        IReadOnlyDictionary<string, string> configuration)
    {
        var result = new JsonObject();
        if (IsMqttPublishStep(stepType))
        {
            AddString(result, configuration, ScenarioStepCatalog.ConnectionKey);
            AddString(result, configuration, ScenarioStepCatalog.TopicKey);
            AddPayload(result, configuration);
            AddString(result, configuration, ScenarioStepCatalog.PayloadEncodingKey);
            AddInt(result, configuration, ScenarioStepCatalog.QosKey, 0);
            AddBool(result, configuration, ScenarioStepCatalog.RetainKey, false);
            return result;
        }

        if (IsMqttTriggerStep(stepType))
        {
            AddString(result, configuration, ScenarioStepCatalog.ConnectionKey);
            AddString(result, configuration, ScenarioStepCatalog.SubscriptionsKey);
            AddInt(result, configuration, ScenarioStepCatalog.QosKey, 1);
            AddBool(result, configuration, ScenarioStepCatalog.ReceiveRetainedKey, false);
            AddBool(result, configuration, ScenarioStepCatalog.RetainAsPublishedKey, true);
            return result;
        }

        AddString(result, configuration, ScenarioStepCatalog.EventTypeKey);
        AddString(result, configuration, ScenarioStepCatalog.TopicStartsWithKey);
        AddString(result, configuration, ScenarioStepCatalog.TopicNotStartsWithKey);
        AddString(result, configuration, ScenarioStepCatalog.SubjectStartsWithKey);
        AddString(result, configuration, ScenarioStepCatalog.StatusKey);
        AddString(result, configuration, ScenarioStepCatalog.SourceKey);
        AddString(result, configuration, ScenarioStepCatalog.PayloadContainsKey);
        AddInt(result, configuration, ScenarioStepCatalog.TimeoutMsKey, 5000);
        AddAttributes(result, configuration);
        return result;
    }

    public static string NamePrefix(string stepType)
        => ScenarioStepCatalog.Shared.Find(stepType)?.NamePrefix ?? "step";

    private static IReadOnlyDictionary<string, string> CreateDefaultConfiguration(
        JsonObject flowApplication,
        string stepType)
        => ScenarioStepCatalog.Shared.CreateDefaultConfiguration(
            stepType,
            ReadFirstConnectionResourceName(flowApplication));

    private static bool IsMqttPublishStep(string stepType)
        => ScenarioStepCatalog.Shared.Find(stepType)?.EditorKind == ScenarioStepEditorKind.MqttPublish;

    private static bool IsMqttTriggerStep(string stepType)
        => ScenarioStepCatalog.Shared.Find(stepType)?.EditorKind == ScenarioStepEditorKind.MqttTrigger;

    private static void AddString(JsonObject target, IReadOnlyDictionary<string, string> configuration, string key)
        => target[key] = configuration.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;

    private static void AddInt(
        JsonObject target,
        IReadOnlyDictionary<string, string> configuration,
        string key,
        int fallback)
    {
        target[key] = configuration.TryGetValue(key, out var value) &&
                      int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static void AddBool(
        JsonObject target,
        IReadOnlyDictionary<string, string> configuration,
        string key,
        bool fallback)
    {
        target[key] = configuration.TryGetValue(key, out var value) &&
                      bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static void AddAttributes(JsonObject target, IReadOnlyDictionary<string, string> configuration)
    {
        var attributes = new JsonObject();
        foreach (var (key, value) in configuration.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (DashboardEventFilterCatalog.TryGetAttributeName(key, out var attributeName) &&
                !string.IsNullOrWhiteSpace(value))
            {
                attributes[attributeName] = value.Trim();
            }
        }

        if (attributes.Count > 0)
        {
            target["attributes"] = attributes;
        }
    }

    private static void AddPayload(JsonObject target, IReadOnlyDictionary<string, string> configuration)
    {
        var payload = configuration.TryGetValue(ScenarioStepCatalog.PayloadKey, out var value)
            ? value ?? string.Empty
            : string.Empty;
        var encoding = configuration.TryGetValue(ScenarioStepCatalog.PayloadEncodingKey, out var configuredEncoding)
            ? configuredEncoding
            : string.Empty;
        if (string.Equals(encoding, "json", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                target[ScenarioStepCatalog.PayloadKey] = JsonNode.Parse(payload);
                return;
            }
            catch (JsonException)
            {
                // Preserve the raw value so it can be corrected without losing input.
            }
        }

        target[ScenarioStepCatalog.PayloadKey] = payload;
    }

    private static string ReadFirstConnectionResourceName(JsonObject flowApplication)
    {
        if (flowApplication["resources"] is not JsonObject resources)
        {
            return string.Empty;
        }

        foreach (var resource in resources)
        {
            if (resource.Value is JsonObject resourceObject &&
                string.Equals(ReadString(resourceObject, "type"), "mqtt.connection", StringComparison.Ordinal))
            {
                return resource.Key;
            }
        }

        return string.Empty;
    }

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<string>(out var text) &&
           !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
