using FluxMq.Pipeline.Runtime;
using FluxMq.Pipeline.Scenarios;
using System.Text.Json;

namespace FluxMq.Pipeline.Definitions;

internal static class ScenarioStepDefinitionValidator
{
    public static void Validate(
        string scenarioName,
        string stepName,
        ScenarioStepDefinition step,
        ApplicationDefinition definition,
        List<ApplicationDefinitionValidationError> errors)
    {
        switch (step.Type)
        {
            case ScenarioStepTypes.MqttPublisher:
                ValidateMqttPublisherStep(scenarioName, stepName, step.Configuration, definition, errors);
                break;
            case ScenarioStepTypes.ExpectEvent:
                ValidateExpectEventStep(scenarioName, stepName, step.Configuration, errors);
                break;
        }
    }

    private static void ValidateMqttPublisherStep(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        ApplicationDefinition definition,
        List<ApplicationDefinitionValidationError> errors)
    {
        var connection = ReadRequiredString(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Connection,
            errors);

        ReadRequiredString(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Topic,
            errors);

        if (!string.IsNullOrWhiteSpace(connection))
        {
            if (!definition.Resources.TryGetValue(connection, out var resource))
            {
                errors.Add(new(
                    ApplicationDefinitionValidationErrorCode.MissingScenarioStepResource,
                    $"Test scenario '{scenarioName}' step '{stepName}' references missing app resource '{connection}'."));
            }
            else if (resource.Type != PipelineFlowNodeTypes.Connection)
            {
                errors.Add(new(
                    ApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration,
                    $"Test scenario '{scenarioName}' step '{stepName}' connection '{connection}' must reference an app resource of type '{PipelineFlowNodeTypes.Connection.Value}'."));
            }
        }

        if (TryReadOptionalString(
                scenarioName,
                stepName,
                configuration,
                ScenarioStepConfigurationKeys.PayloadEncoding,
                errors,
                out var encoding) &&
            !string.IsNullOrWhiteSpace(encoding))
        {
            ValidatePayloadEncoding(scenarioName, stepName, configuration, encoding, errors);
        }

        ValidateOptionalInt(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Qos,
            0,
            2,
            errors);

        ValidateOptionalBool(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Retain,
            errors);
    }

    private static void ValidatePayloadEncoding(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string encoding,
        List<ApplicationDefinitionValidationError> errors)
    {
        switch (encoding.Trim().ToLowerInvariant())
        {
            case "utf8":
            case "text":
            case "json":
                return;
            case "base64":
                ValidateBase64Payload(scenarioName, stepName, configuration, errors);
                return;
            case "bytes":
                ValidateBytePayload(scenarioName, stepName, configuration, errors);
                return;
            default:
                errors.Add(InvalidScenarioConfiguration(
                    scenarioName,
                    stepName,
                    ScenarioStepConfigurationKeys.PayloadEncoding,
                    "must be utf8, text, json, base64, or bytes."));
                return;
        }
    }

    private static void ValidateBase64Payload(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(ScenarioStepConfigurationKeys.Payload, out var payload) ||
            payload.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (payload.ValueKind != JsonValueKind.String)
        {
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                ScenarioStepConfigurationKeys.Payload,
                "must be a base64 string when payloadEncoding is base64."));
            return;
        }

        try
        {
            Convert.FromBase64String(payload.GetString() ?? string.Empty);
        }
        catch (FormatException)
        {
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                ScenarioStepConfigurationKeys.Payload,
                "must be valid base64 when payloadEncoding is base64."));
        }
    }

    private static void ValidateBytePayload(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(ScenarioStepConfigurationKeys.Payload, out var payload) ||
            payload.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (payload.ValueKind != JsonValueKind.Array)
        {
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                ScenarioStepConfigurationKeys.Payload,
                "must be an array when payloadEncoding is bytes."));
            return;
        }

        foreach (var value in payload.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetByte(out _))
            {
                errors.Add(InvalidScenarioConfiguration(
                    scenarioName,
                    stepName,
                    ScenarioStepConfigurationKeys.Payload,
                    "byte payload values must be integers from 0 to 255."));
                return;
            }
        }
    }

    private static void ValidateExpectEventStep(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<ApplicationDefinitionValidationError> errors)
    {
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.EventType, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.TopicStartsWith, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.SubjectStartsWith, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.Status, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.Source, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.PayloadContains, errors);
        ValidateOptionalInt(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.TimeoutMs, 1, int.MaxValue, errors);
        ValidateAttributes(scenarioName, stepName, configuration, errors);
    }

    private static string? ReadRequiredString(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (!TryReadOptionalString(scenarioName, stepName, configuration, key, errors, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            errors.Add(InvalidScenarioConfiguration(scenarioName, stepName, key, "is required and must be a string."));
            return null;
        }

        return value;
    }

    private static void ValidateOptionalString(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        List<ApplicationDefinitionValidationError> errors)
        => TryReadOptionalString(scenarioName, stepName, configuration, key, errors, out _);

    private static bool TryReadOptionalString(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        List<ApplicationDefinitionValidationError> errors,
        out string? value)
    {
        value = null;
        if (!configuration.TryGetValue(key, out var element) ||
            element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            errors.Add(InvalidScenarioConfiguration(scenarioName, stepName, key, "must be a string."));
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static void ValidateOptionalBool(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(key, out var element))
        {
            return;
        }

        if (element.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            errors.Add(InvalidScenarioConfiguration(scenarioName, stepName, key, "must be a boolean."));
        }
    }

    private static void ValidateOptionalInt(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        int minValue,
        int maxValue,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(key, out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out var value) ||
            value < minValue ||
            value > maxValue)
        {
            var range = maxValue == int.MaxValue
                ? $"greater than or equal to {minValue}"
                : $"between {minValue} and {maxValue}";
            errors.Add(InvalidScenarioConfiguration(scenarioName, stepName, key, $"must be an integer {range}."));
        }
    }

    private static void ValidateAttributes(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(ScenarioStepConfigurationKeys.Attributes, out var attributes) ||
            attributes.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (attributes.ValueKind != JsonValueKind.Object)
        {
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                ScenarioStepConfigurationKeys.Attributes,
                "must be an object."));
            return;
        }

        foreach (var attribute in attributes.EnumerateObject())
        {
            if (attribute.Value.ValueKind is JsonValueKind.String or
                JsonValueKind.True or
                JsonValueKind.False or
                JsonValueKind.Number)
            {
                continue;
            }

            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                $"{ScenarioStepConfigurationKeys.Attributes}.{attribute.Name}",
                "must be a string, boolean, or number."));
        }
    }

    private static ApplicationDefinitionValidationError InvalidScenarioConfiguration(
        string scenarioName,
        string stepName,
        string key,
        string rule)
        => new(
            ApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration,
            $"Test scenario '{scenarioName}' step '{stepName}' configuration value '{key}' {rule}");
}
