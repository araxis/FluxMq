using FluxFlow.Engine.Definitions;
using FluxMq.App.Metrics;
using FluxMq.Scenarios;
using System.Text.Json;
using EngineApplicationDefinitionValidationError = FluxFlow.Engine.Definitions.ApplicationDefinitionValidationError;
using EngineApplicationDefinitionValidationErrorCode = FluxFlow.Engine.Definitions.ApplicationDefinitionValidationErrorCode;
using EngineApplicationDefinitionValidator = FluxFlow.Engine.Definitions.ApplicationDefinitionValidator;

namespace FluxMq.App.Definitions;

public sealed record FluxMqApplicationDefinitionValidationResult(
    IReadOnlyList<FluxMqApplicationDefinitionValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record FluxMqApplicationDefinitionValidationError(
    FluxMqApplicationDefinitionValidationErrorCode Code,
    string Message,
    string? WorkflowName = null,
    string? NodeName = null,
    string? PortName = null);

public enum FluxMqApplicationDefinitionValidationErrorCode
{
    EmptyDefinition = 1,
    EmptyWorkflowName = 2,
    EmptyWorkflow = 3,
    EmptyNodeName = 4,
    EmptyResourceName = 5,
    EmptyNodeType = 6,
    InvalidLink = 7,
    MissingSourceNode = 8,
    EmptySourcePort = 9,
    EmptyTargetPort = 10,
    DuplicateLink = 11,
    EmptyDashboardName = 12,
    InvalidDashboardLayout = 13,
    EmptyDashboardWidgetName = 14,
    EmptyDashboardWidgetType = 15,
    EmptyDashboardCellName = 16,
    InvalidDashboardCell = 17,
    MissingDashboardWidget = 18,
    EmptyScenarioName = 19,
    EmptyScenarioStepName = 20,
    EmptyScenarioStepType = 21,
    UnknownScenarioStepType = 22,
    MissingScenarioStepResource = 23,
    InvalidScenarioStepConfiguration = 24,
    EmptyMetricName = 25,
    InvalidMetricDefinition = 26,
    InvalidMetricParameter = 27,
    MissingDashboardMetric = 28,
    EmptyExplorerName = 29,
    EmptyExplorerType = 30,
    InvalidExplorerConfiguration = 31,
    MissingExplorerConnectionResource = 32
}

public sealed class FluxMqApplicationDefinitionValidator
{
    private readonly EngineApplicationDefinitionValidator _engineValidator = new();
    private readonly ScenarioStepDefinitionCatalog _scenarioStepDefinitions;
    private readonly IFluxMetricCatalog _metrics;

    public FluxMqApplicationDefinitionValidator()
        : this(ScenarioStepDefinitionCatalog.Shared)
    {
    }

    public FluxMqApplicationDefinitionValidator(ScenarioStepDefinitionCatalog scenarioStepDefinitions)
        : this(scenarioStepDefinitions, FluxMetricCatalog.CreateDefault())
    {
    }

    public FluxMqApplicationDefinitionValidator(
        ScenarioStepDefinitionCatalog scenarioStepDefinitions,
        IFluxMetricCatalog metrics)
    {
        ArgumentNullException.ThrowIfNull(scenarioStepDefinitions);
        ArgumentNullException.ThrowIfNull(metrics);

        _scenarioStepDefinitions = scenarioStepDefinitions;
        _metrics = metrics;
    }

    public FluxMqApplicationDefinitionValidationResult Validate(FluxMqApplicationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = _engineValidator
            .Validate(definition.ToEngineDefinition())
            .Errors
            .Select(FromEngineError)
            .ToList();

        foreach (var metric in definition.Metrics)
        {
            ValidateMetric(metric.Key, metric.Value, errors);
        }


        foreach (var dashboard in definition.Dashboards)
        {
            ValidateDashboard(dashboard.Key, dashboard.Value, definition, errors);
        }

        foreach (var test in definition.Tests)
        {
            ValidateScenario(test.Key, test.Value, definition, errors);
        }

        foreach (var explorer in definition.Explorers)
        {
            ValidateExplorer(explorer.Key, explorer.Value, definition, errors);
        }

        return new FluxMqApplicationDefinitionValidationResult(errors);
    }

    private static FluxMqApplicationDefinitionValidationError FromEngineError(
        EngineApplicationDefinitionValidationError error)
        => new(
            ToApplicationCode(error.Code),
            error.Message,
            error.WorkflowName,
            error.NodeName,
            error.PortName);

    private static FluxMqApplicationDefinitionValidationErrorCode ToApplicationCode(
        EngineApplicationDefinitionValidationErrorCode code)
        => (FluxMqApplicationDefinitionValidationErrorCode)(int)code;

    private static void ValidateDashboard(
        string dashboardName,
        DashboardDefinition dashboard,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.EmptyDashboardName,
                "Dashboard name cannot be empty."));
        }

        if (dashboard.Layout.Columns.Count == 0)
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardLayout,
                $"Dashboard '{dashboardName}' must define at least one column track."));
        }

        if (dashboard.Layout.Rows.Count == 0)
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardLayout,
                $"Dashboard '{dashboardName}' must define at least one row track."));
        }

        ValidateTracks(dashboardName, "column", dashboard.Layout.Columns, errors);
        ValidateTracks(dashboardName, "row", dashboard.Layout.Rows, errors);
        ValidateTrackPadding(dashboardName, "column", dashboard.Layout.ColumnPadding, errors);
        ValidateTrackPadding(dashboardName, "row", dashboard.Layout.RowPadding, errors);

        foreach (var widget in dashboard.Widgets)
        {
            if (string.IsNullOrWhiteSpace(widget.Key))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.EmptyDashboardWidgetName,
                    $"Dashboard '{dashboardName}' has an empty widget name."));
            }

            if (string.IsNullOrWhiteSpace(widget.Value.Type))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.EmptyDashboardWidgetType,
                    $"Dashboard '{dashboardName}' widget '{widget.Key}' has an empty type."));
            }
        }

        foreach (var cell in dashboard.Layout.Cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Key))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.EmptyDashboardCellName,
                    $"Dashboard '{dashboardName}' has an empty cell name."));
            }

            if (cell.Value.Row < 0 ||
                cell.Value.Column < 0 ||
                cell.Value.RowSpan <= 0 ||
                cell.Value.ColumnSpan <= 0)
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardCell,
                    $"Dashboard '{dashboardName}' cell '{cell.Key}' has an invalid grid position or span."));
            }

            if (cell.Value.Row >= 0 &&
                cell.Value.RowSpan > 0 &&
                dashboard.Layout.Rows.Count > 0 &&
                cell.Value.Row + cell.Value.RowSpan > dashboard.Layout.Rows.Count)
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardCell,
                    $"Dashboard '{dashboardName}' cell '{cell.Key}' extends beyond the defined row tracks."));
            }

            if (cell.Value.Column >= 0 &&
                cell.Value.ColumnSpan > 0 &&
                dashboard.Layout.Columns.Count > 0 &&
                cell.Value.Column + cell.Value.ColumnSpan > dashboard.Layout.Columns.Count)
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardCell,
                    $"Dashboard '{dashboardName}' cell '{cell.Key}' extends beyond the defined column tracks."));
            }

            if (!string.IsNullOrWhiteSpace(cell.Value.Widget) &&
                !dashboard.Widgets.ContainsKey(cell.Value.Widget))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.MissingDashboardWidget,
                    $"Dashboard '{dashboardName}' cell '{cell.Key}' references missing widget '{cell.Value.Widget}'."));
            }
        }

        foreach (var binding in dashboard.Bindings)
        {
            var metricNames = binding.Value.Metrics
                .Append(binding.Value.PrimaryMetric ?? string.Empty)
                .Where(static metric => !string.IsNullOrWhiteSpace(metric))
                .Distinct(StringComparer.Ordinal);
            foreach (var metricName in metricNames)
            {
                if (!dashboard.Metrics.ContainsKey(metricName) &&
                    !definition.Metrics.ContainsKey(metricName))
                {
                    errors.Add(new(
                        FluxMqApplicationDefinitionValidationErrorCode.MissingDashboardMetric,
                        $"Dashboard '{dashboardName}' widget '{binding.Key}' references missing metric '{metricName}'."));
                }
            }
        }
    }

    private void ValidateMetric(
        string metricName,
        FluxMetricResourceDefinition resource,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.EmptyMetricName,
                "Metric name cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.EmptyMetricName,
                $"Metric '{metricName}' must define a display name."));
        }

        if (_metrics.Describe(resource.TypeId) is not { } descriptor)
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidMetricDefinition,
                $"Metric '{metricName}' has unknown metric type '{resource.TypeId}'."));
            return;
        }

        foreach (var parameter in descriptor.Parameters)
        {
            if (parameter.Required &&
                string.IsNullOrWhiteSpace(resource.GetParameter(parameter.Key, parameter.DefaultValue ?? string.Empty)))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidMetricParameter,
                    $"Metric '{metricName}' is missing required parameter '{parameter.DisplayName}'."));
            }
        }
    }

    private static void ValidateTracks(
        string dashboardName,
        string axis,
        IReadOnlyList<DashboardGridTrackDefinition> tracks,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track.Value <= 0 ||
                double.IsNaN(track.Value) ||
                double.IsInfinity(track.Value))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardLayout,
                    $"Dashboard '{dashboardName}' {axis} track {i} must be a positive finite size."));
            }

            if (track.Unit == DashboardGridTrackUnit.Percent && track.Value > 100)
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardLayout,
                    $"Dashboard '{dashboardName}' {axis} track {i} percent size cannot exceed 100."));
            }
        }
    }

    private static void ValidateTrackPadding(
        string dashboardName,
        string axis,
        IReadOnlyList<double> padding,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        for (var i = 0; i < padding.Count; i++)
        {
            if (padding[i] < 0 || double.IsNaN(padding[i]) || double.IsInfinity(padding[i]))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardLayout,
                    $"Dashboard '{dashboardName}' {axis} track {i} padding must be a non-negative finite size."));
            }
        }
    }

    private void ValidateScenario(
        string scenarioName,
        ScenarioDefinition scenario,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.EmptyScenarioName,
                "Test scenario name cannot be empty."));
        }

        foreach (var step in scenario.EnumerateSteps())
        {
            if (string.IsNullOrWhiteSpace(step.Key))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.EmptyScenarioStepName,
                    $"Test scenario '{scenarioName}' has an empty step name."));
            }

            if (string.IsNullOrWhiteSpace(step.Value.Type))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.EmptyScenarioStepType,
                    $"Test scenario '{scenarioName}' step '{step.Key}' has an empty type."));
            }
            else if (_scenarioStepDefinitions.Find(step.Value.Type) is not { } stepDefinition)
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.UnknownScenarioStepType,
                    $"Test scenario '{scenarioName}' step '{step.Key}' has unknown type '{step.Value.Type}'."));
            }
            else
            {
                FluxMqScenarioStepDefinitionValidator.Validate(
                    scenarioName,
                    step.Key,
                    step.Value,
                    stepDefinition,
                    definition,
                    errors);
            }
        }
    }

    private static void ValidateExplorer(
        string explorerName,
        ExplorerDefinition explorer,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(explorerName))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.EmptyExplorerName,
                "Explorer name cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(explorer.Type))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.EmptyExplorerType,
                $"Explorer '{explorerName}' must define a type."));
            return;
        }

        if (!string.Equals(explorer.Type, ExplorerDefinition.MqttTopicsType, StringComparison.Ordinal))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidExplorerConfiguration,
                $"Explorer '{explorerName}' has unknown type '{explorer.Type}'."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(explorer.ConnectionResource))
        {
            if (!definition.Resources.TryGetValue(explorer.ConnectionResource, out var resource))
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.MissingExplorerConnectionResource,
                    $"Explorer '{explorerName}' references missing app resource '{explorer.ConnectionResource}'."));
            }
            else if (resource.Type != FluxMqNodeTypes.Connection)
            {
                errors.Add(new(
                    FluxMqApplicationDefinitionValidationErrorCode.InvalidExplorerConfiguration,
                    $"Explorer '{explorerName}' connectionResource '{explorer.ConnectionResource}' must reference an app resource of type '{FluxMqNodeTypes.Connection.Value}'."));
            }
        }
        else if (string.IsNullOrWhiteSpace(explorer.Connection?.Host))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidExplorerConfiguration,
                $"Explorer '{explorerName}' must define connectionResource or connection.host."));
        }

        if (explorer.Connection?.Port is { } port && port <= 0)
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidExplorerConfiguration,
                $"Explorer '{explorerName}' connection.port must be greater than zero."));
        }

        if (explorer.Connection?.KeepAliveSeconds is { } keepAliveSeconds && keepAliveSeconds <= 0)
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidExplorerConfiguration,
                $"Explorer '{explorerName}' connection.keepAliveSeconds must be greater than zero."));
        }

        if (explorer.Subscriptions.Any(static subscription => string.IsNullOrWhiteSpace(subscription)))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidExplorerConfiguration,
                $"Explorer '{explorerName}' subscriptions cannot contain empty topic filters."));
        }
    }
}

internal static class FluxMqScenarioStepDefinitionValidator
{
    public static void Validate(
        string scenarioName,
        string stepName,
        ScenarioStepDefinition step,
        ScenarioStepDefinitionDescriptor stepDefinition,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(stepDefinition);

        switch (step.Type)
        {
            case ScenarioStepTypes.MqttPublisher:
                ValidateMqttPublisherStep(scenarioName, stepName, step.Configuration, stepDefinition, definition, errors);
                break;
            case ScenarioStepTypes.MqttTrigger:
                ValidateMqttTriggerStep(scenarioName, stepName, step.Configuration, stepDefinition, definition, errors);
                break;
            case ScenarioStepTypes.WhenEvent:
            case ScenarioStepTypes.ExpectEvent:
            case ScenarioStepTypes.WaitForEvent:
            case ScenarioStepTypes.ConditionalEvent:
            case ScenarioStepTypes.PayloadAssertion:
            case ScenarioStepTypes.JsonSchemaAssertion:
                ValidateExpectEventStep(scenarioName, stepName, step.Configuration, errors);
                break;
            case ScenarioStepTypes.MetricThresholdAssertion:
                ValidateMetricThresholdStep(scenarioName, stepName, step.Configuration, stepDefinition, errors);
                break;
            case ScenarioStepTypes.Delay:
            case ScenarioStepTypes.CleanupAction:
                ValidateDelayStep(scenarioName, stepName, step.Configuration, errors);
                break;
        }
    }

    private static void ValidateMqttPublisherStep(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        ScenarioStepDefinitionDescriptor stepDefinition,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
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

        ValidateConnectionResource(scenarioName, stepName, connection, definition, errors);

        if (TryReadOptionalString(
                scenarioName,
                stepName,
                configuration,
                ScenarioStepConfigurationKeys.PayloadEncoding,
                errors,
                out var encoding) &&
            !string.IsNullOrWhiteSpace(encoding))
        {
            ValidatePayloadEncoding(scenarioName, stepName, configuration, stepDefinition, encoding, errors);
        }

        ValidateOptionalIntOption(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Qos,
            stepDefinition,
            errors);

        ValidateOptionalBool(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Retain,
            errors);
    }

    private static void ValidateMqttTriggerStep(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        ScenarioStepDefinitionDescriptor stepDefinition,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        var connection = ReadRequiredString(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Connection,
            errors);

        ValidateConnectionResource(scenarioName, stepName, connection, definition, errors);

        ReadRequiredString(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Subscriptions,
            errors);

        ValidateOptionalIntOption(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Qos,
            stepDefinition,
            errors);

        ValidateOptionalBool(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.ReceiveRetained,
            errors);

        ValidateOptionalBool(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.RetainAsPublished,
            errors);
    }

    private static void ValidateConnectionResource(
        string scenarioName,
        string stepName,
        string? connection,
        FluxMqApplicationDefinition definition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        if (!definition.Resources.TryGetValue(connection, out var resource))
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.MissingScenarioStepResource,
                $"Test scenario '{scenarioName}' step '{stepName}' references missing app resource '{connection}'."));
        }
        else if (resource.Type != FluxMqNodeTypes.Connection)
        {
            errors.Add(new(
                FluxMqApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration,
                $"Test scenario '{scenarioName}' step '{stepName}' connection '{connection}' must reference an app resource of type '{FluxMqNodeTypes.Connection.Value}'."));
        }
    }

    private static void ValidatePayloadEncoding(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        ScenarioStepDefinitionDescriptor stepDefinition,
        string encoding,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        var normalizedEncoding = encoding.Trim().ToLowerInvariant();
        if (!FieldOptionValues(stepDefinition, ScenarioStepConfigurationKeys.PayloadEncoding)
                .Contains(normalizedEncoding, StringComparer.Ordinal))
        {
            var allowed = string.Join(", ", FieldOptionValues(stepDefinition, ScenarioStepConfigurationKeys.PayloadEncoding));
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                ScenarioStepConfigurationKeys.PayloadEncoding,
                $"must be one of: {allowed}."));
            return;
        }

        switch (normalizedEncoding)
        {
            case "text":
            case "json":
                return;
            case "base64":
                ValidateBase64Payload(scenarioName, stepName, configuration, errors);
                return;
            case "bytes":
                ValidateBytePayload(scenarioName, stepName, configuration, errors);
                return;
        }
    }

    private static void ValidateBase64Payload(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<FluxMqApplicationDefinitionValidationError> errors)
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
        List<FluxMqApplicationDefinitionValidationError> errors)
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
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.EventType, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.TopicStartsWith, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.TopicNotStartsWith, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.SubjectStartsWith, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.Status, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.Source, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.PayloadContains, errors);
        ValidateOptionalInt(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.TimeoutMs, 1, int.MaxValue, errors);
        ValidateAttributes(scenarioName, stepName, configuration, errors);
    }

    private static void ValidateMetricThresholdStep(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        ScenarioStepDefinitionDescriptor stepDefinition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.Metric, errors);
        ValidateOptionalStringOption(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Aggregation,
            stepDefinition,
            errors);
        ValidateOptionalStringOption(
            scenarioName,
            stepName,
            configuration,
            ScenarioStepConfigurationKeys.Operator,
            stepDefinition,
            errors);
        ValidateOptionalThreshold(scenarioName, stepName, configuration, errors);
        ValidateOptionalInt(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.WindowMs, 1, int.MaxValue, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.EventType, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.TopicStartsWith, errors);
        ValidateOptionalString(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.Status, errors);
    }

    private static void ValidateDelayStep(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<FluxMqApplicationDefinitionValidationError> errors)
        => ValidateOptionalInt(scenarioName, stepName, configuration, ScenarioStepConfigurationKeys.DelayMs, 0, int.MaxValue, errors);

    private static string? ReadRequiredString(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        List<FluxMqApplicationDefinitionValidationError> errors)
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
        List<FluxMqApplicationDefinitionValidationError> errors)
        => TryReadOptionalString(scenarioName, stepName, configuration, key, errors, out _);

    private static void ValidateOptionalStringOption(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        ScenarioStepDefinitionDescriptor stepDefinition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (!TryReadOptionalString(scenarioName, stepName, configuration, key, errors, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var options = FieldOptionValues(stepDefinition, key);
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                key,
                $"must be one of: {string.Join(", ", options)}."));
        }
    }

    private static void ValidateOptionalThreshold(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(ScenarioStepConfigurationKeys.Threshold, out var element))
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out _))
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(
                element.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _))
        {
            return;
        }

        errors.Add(InvalidScenarioConfiguration(
            scenarioName,
            stepName,
            ScenarioStepConfigurationKeys.Threshold,
            "must be a number."));
    }

    private static bool TryReadOptionalString(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        List<FluxMqApplicationDefinitionValidationError> errors,
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
        List<FluxMqApplicationDefinitionValidationError> errors)
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
        List<FluxMqApplicationDefinitionValidationError> errors)
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

    private static void ValidateOptionalIntOption(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        string key,
        ScenarioStepDefinitionDescriptor stepDefinition,
        List<FluxMqApplicationDefinitionValidationError> errors)
    {
        if (!configuration.TryGetValue(key, out var element))
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out var value))
        {
            errors.Add(InvalidScenarioConfiguration(scenarioName, stepName, key, "must be an integer."));
            return;
        }

        var options = FieldOptionValues(stepDefinition, key);
        if (!options.Contains(value.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal))
        {
            errors.Add(InvalidScenarioConfiguration(
                scenarioName,
                stepName,
                key,
                $"must be one of: {string.Join(", ", options)}."));
        }
    }

    private static IReadOnlyList<string> FieldOptionValues(ScenarioStepDefinitionDescriptor stepDefinition, string key)
        => stepDefinition.Fields
            .FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal))
            ?.Options
            .Select(option => option.Value)
            .ToArray() ?? [];

    private static void ValidateAttributes(
        string scenarioName,
        string stepName,
        IReadOnlyDictionary<string, JsonElement> configuration,
        List<FluxMqApplicationDefinitionValidationError> errors)
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

    private static FluxMqApplicationDefinitionValidationError InvalidScenarioConfiguration(
        string scenarioName,
        string stepName,
        string key,
        string rule)
        => new(
            FluxMqApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration,
            $"Test scenario '{scenarioName}' step '{stepName}' configuration value '{key}' {rule}");
}
