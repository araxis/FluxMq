using FluxMq.Scenarios;
using FluxMq.UI.Models;
using MudBlazor;

namespace FluxMq.UI.Services;

public sealed class ScenarioStepCatalog
{
    public const string ConnectionKey = ScenarioStepDefinitionCatalog.ConnectionKey;
    public const string TopicKey = ScenarioStepDefinitionCatalog.TopicKey;
    public const string PayloadKey = ScenarioStepDefinitionCatalog.PayloadKey;
    public const string PayloadEncodingKey = ScenarioStepDefinitionCatalog.PayloadEncodingKey;
    public const string QosKey = ScenarioStepDefinitionCatalog.QosKey;
    public const string RetainKey = ScenarioStepDefinitionCatalog.RetainKey;
    public const string SubscriptionsKey = ScenarioStepDefinitionCatalog.SubscriptionsKey;
    public const string ReceiveRetainedKey = ScenarioStepDefinitionCatalog.ReceiveRetainedKey;
    public const string RetainAsPublishedKey = ScenarioStepDefinitionCatalog.RetainAsPublishedKey;
    public const string EventTypeKey = ScenarioStepDefinitionCatalog.EventTypeKey;
    public const string TopicStartsWithKey = ScenarioStepDefinitionCatalog.TopicStartsWithKey;
    public const string TopicNotStartsWithKey = ScenarioStepDefinitionCatalog.TopicNotStartsWithKey;
    public const string SubjectStartsWithKey = ScenarioStepDefinitionCatalog.SubjectStartsWithKey;
    public const string StatusKey = ScenarioStepDefinitionCatalog.StatusKey;
    public const string SourceKey = ScenarioStepDefinitionCatalog.SourceKey;
    public const string PayloadContainsKey = ScenarioStepDefinitionCatalog.PayloadContainsKey;
    public const string TimeoutMsKey = ScenarioStepDefinitionCatalog.TimeoutMsKey;
    public const string DelayMsKey = ScenarioStepDefinitionCatalog.DelayMsKey;
    public const string MetricKey = ScenarioStepDefinitionCatalog.MetricKey;
    public const string AggregationKey = ScenarioStepDefinitionCatalog.AggregationKey;
    public const string OperatorKey = ScenarioStepDefinitionCatalog.OperatorKey;
    public const string ThresholdKey = ScenarioStepDefinitionCatalog.ThresholdKey;
    public const string WindowMsKey = ScenarioStepDefinitionCatalog.WindowMsKey;

    public static readonly string QosAttributeKey = ScenarioStepDefinitionCatalog.QosAttributeKey;
    public static readonly string RetainAttributeKey = ScenarioStepDefinitionCatalog.RetainAttributeKey;
    public static readonly string SchemaIdAttributeKey = ScenarioStepDefinitionCatalog.SchemaIdAttributeKey;

    public static ScenarioStepCatalog Shared { get; } = new();

    private readonly ScenarioStepDefinitionCatalog _definitions;
    private readonly IReadOnlyList<ScenarioStepDescriptor> _steps;

    public ScenarioStepCatalog()
        : this(ScenarioStepDefinitionCatalog.Shared)
    {
    }

    public ScenarioStepCatalog(ScenarioStepDefinitionCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        _definitions = definitions;
        _steps = definitions.Steps.Select(CreateDescriptor).ToArray();
    }

    public IReadOnlyList<ScenarioStepDescriptor> Steps => _steps;

    public ScenarioStepDescriptor? Find(string? type)
        => _steps.FirstOrDefault(step => string.Equals(step.Type, type, StringComparison.Ordinal));

    public ScenarioStepDescriptor Describe(string? type)
        => Find(type) ?? CreateDescriptor(_definitions.Describe(type));

    public IReadOnlyDictionary<string, string> CreateDefaultConfiguration(
        string? type,
        string? defaultConnection = null)
        => _definitions.CreateDefaultConfiguration(type, defaultConnection);

    private static ScenarioStepDescriptor CreateDescriptor(ScenarioStepDefinitionDescriptor definition)
    {
        var metadata = UiMetadataFor(definition.Type);
        return new ScenarioStepDescriptor(
            definition.Type,
            definition.DisplayName,
            definition.Category,
            definition.Description,
            metadata.Icon,
            definition.NamePrefix,
            metadata.EditorKind,
            definition.Fields,
            definition.DefaultPhase);
    }

    private static ScenarioStepUiMetadata UiMetadataFor(string type)
        => type switch
        {
            ScenarioStepTypes.MqttPublisher => new(Icons.Material.Filled.Send, ScenarioStepEditorKind.MqttPublish),
            ScenarioStepTypes.MqttTrigger => new(Icons.Material.Filled.Sensors, ScenarioStepEditorKind.MqttTrigger),
            ScenarioStepTypes.WhenEvent => new(Icons.Material.Filled.AltRoute, ScenarioStepEditorKind.ExpectEvent),
            ScenarioStepTypes.ExpectEvent => new(Icons.Material.Filled.Rule, ScenarioStepEditorKind.ExpectEvent),
            ScenarioStepTypes.WaitForEvent => new(Icons.Material.Filled.HourglassTop, ScenarioStepEditorKind.ExpectEvent),
            ScenarioStepTypes.ConditionalEvent => new(Icons.Material.Filled.AccountTree, ScenarioStepEditorKind.ExpectEvent),
            ScenarioStepTypes.PayloadAssertion => new(Icons.Material.Filled.DataObject, ScenarioStepEditorKind.ExpectEvent),
            ScenarioStepTypes.JsonSchemaAssertion => new(Icons.Material.Filled.Schema, ScenarioStepEditorKind.ExpectEvent),
            ScenarioStepTypes.MetricThresholdAssertion => new(Icons.Material.Filled.QueryStats, ScenarioStepEditorKind.MetricThreshold),
            ScenarioStepTypes.Delay => new(Icons.Material.Filled.Timer, ScenarioStepEditorKind.Delay),
            ScenarioStepTypes.CleanupAction => new(Icons.Material.Filled.CleaningServices, ScenarioStepEditorKind.Cleanup),
            _ => new(Icons.Material.Filled.Extension, ScenarioStepEditorKind.ExpectEvent)
        };

    private sealed record ScenarioStepUiMetadata(
        string Icon,
        ScenarioStepEditorKind EditorKind);
}
