using FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Components.Workspace.Nodes.Sources;
using FluxMq.UI.Components.Workspace.Nodes.StateReducer;
using FluxMq.UI.Components.Workspace.Nodes.Timers;
using FluxMq.UI.Models;
using FluxFlow.Components.Routing;
using FluxFlow.Components.Timers;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed record FlowComponentDefaultConfigurationContext(
    string DefaultInputType,
    string? ConnectionName)
{
    public static FlowComponentDefaultConfigurationContext Empty { get; } =
        new("MqttEnvelope", null);
}

public enum FlowComponentDefaultInputLink
{
    PreferredSource,
    None,
    PreferredMapper,
    StateReducerMapper
}

public sealed record FlowComponentMetadata(
    FlowComponentDescriptor Descriptor,
    string PreferredNodeName,
    bool MakePreferredNodeNameUnique,
    FlowComponentDefaultInputLink DefaultInputLink,
    Func<FlowComponentDefaultConfigurationContext, JsonObject?>? CreateDefaultConfiguration);

public static class FlowComponentMetadataRegistry
{
    private static readonly IReadOnlyList<FlowComponentMetadata> Metadata =
    [
        Component(
            "mqtt.trigger",
            "Live MQTT Trigger",
            "Source",
            "Subscribes to a configured broker connection and emits live MQTT envelopes.",
            "trigger",
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None),
        Component(
            "session.source",
            "Stored Session Source",
            "Source",
            "Replays messages from a stored MQTT recording session and emits MQTT envelopes.",
            FlowDefinitionComposer.StoredSourceNodeName,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateStoredSessionSourceConfiguration()),
        Component(
            "replay.source",
            "Replay Source",
            "Source",
            "Replays a selected stored session through the pipeline with configurable timing.",
            FlowDefinitionComposer.ReplayNodeName,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateReplaySourceConfiguration()),
        Component(
            "generated.source",
            "MQTT Message List Source",
            "Source",
            "Emits configured MQTT envelopes from a fixed message list.",
            FlowDefinitionComposer.GeneratedNodeName,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateGeneratedSourceConfiguration()),
        Component(
            "payload.inspect",
            "Payload Inspect",
            "Mapper",
            "Classifies byte or text payload requests and emits preview metadata.",
            FlowDefinitionComposer.PayloadInspectNodeName,
            [
                new("Input", "PayloadInspectionRequest", IsInput: true),
                new("Output", "PayloadInspectionResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.PreferredMapper,
            createDefaultConfiguration: _ => CreatePayloadInspectConfiguration()),
        Component(
            TimerNodeTypes.Interval,
            "Timer Interval",
            "Source",
            "Emits timer ticks at a fixed interval.",
            FlowDefinitionComposer.TimerIntervalNodeName,
            [
                new("Output", "TimerTick", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateTimerIntervalConfiguration()),
        Component(
            TimerNodeTypes.Schedule,
            "Scheduled Timer",
            "Source",
            "Emits schedule ticks from a cron expression.",
            FlowDefinitionComposer.TimerScheduleNodeName,
            [
                new("Output", "ScheduleTick", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateTimerScheduleConfiguration()),
        Component(
            TimerNodeTypes.Delay,
            "Delay",
            "Control",
            "Delays inputs and emits them unchanged.",
            FlowDefinitionComposer.TimerDelayNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "Configured input type", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateTimerDelayConfiguration(context.DefaultInputType)),
        Component(
            TimerNodeTypes.Debounce,
            "Debounce",
            "Control",
            "Emits the latest input after a quiet period.",
            FlowDefinitionComposer.TimerDebounceNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "Configured input type", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateTimerDebounceConfiguration(context.DefaultInputType)),
        Component(
            TimerNodeTypes.Throttle,
            "Throttle",
            "Control",
            "Limits input emissions to a fixed interval.",
            FlowDefinitionComposer.TimerThrottleNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "Configured input type", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateTimerThrottleConfiguration(context.DefaultInputType)),
        Component(
            "mqtt.payload-inspector",
            "Payload Inspector",
            "Mapper",
            "Maps MQTT messages into inspected payload results.",
            FlowDefinitionComposer.InspectorNodeName,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "InspectedMqttMessage", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        Component(
            "flow.filter",
            "Flow Filter",
            "Control",
            "Lets matching inputs continue downstream and drops the rest.",
            FlowDefinitionComposer.FilterNodeName,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        Component(
            "flow.when",
            "When",
            "Control",
            "Splits inputs into true and false branches.",
            FlowDefinitionComposer.RouterNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("WhenTrue", "Configured input type", IsInput: false),
                new("WhenFalse", "Configured input type", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateConditionRouterConfiguration(context.DefaultInputType)),
        Component(
            RoutingNodeTypes.Switch,
            "Switch",
            "Control",
            "Routes input values by expression result into named branches.",
            FlowDefinitionComposer.SwitchNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Result", "FlowSwitchResult", IsInput: false),
                new("Matched", "Configured input type", IsInput: false),
                new("WhenTrue", "Configured input type", IsInput: false),
                new("WhenFalse", "Configured input type", IsInput: false),
                new("Default", "Configured input type", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateRoutingSwitchConfiguration(context.DefaultInputType)),
        Component(
            RoutingNodeTypes.Correlation,
            "Correlation",
            "Control",
            "Pairs request and response values by key and side expressions.",
            FlowDefinitionComposer.CorrelationNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Matched", "FlowCorrelationMatch", IsInput: false),
                new("Timeouts", "FlowCorrelationTimeout", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateRoutingCorrelationConfiguration(context.DefaultInputType)),
        Component(
            RoutingNodeTypes.Window,
            "Window",
            "Control",
            "Groups input values into count or time based windows.",
            FlowDefinitionComposer.WindowNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "FlowWindow", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateRoutingWindowConfiguration(context.DefaultInputType)),
        Component(
            RoutingNodeTypes.Join,
            "Join",
            "Control",
            "Pairs left and right streams by matching key expressions.",
            FlowDefinitionComposer.JoinNodeName,
            [
                new("Left", "Configured left input type", IsInput: true),
                new("Right", "Configured right input type", IsInput: true),
                new("Output", "FlowJoinResult", IsInput: false),
                new("Timeouts", "FlowJoinTimeout", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateRoutingJoinConfiguration()),
        Component(
            RoutingNodeTypes.Fork,
            "Fork",
            "Control",
            "Copies every input value to each configured output branch.",
            FlowDefinitionComposer.ForkNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("A", "Configured input type", IsInput: false),
                new("B", "Configured input type", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: context => CreateRoutingForkConfiguration(context.DefaultInputType)),
        Component(
            RoutingNodeTypes.Merge,
            "Merge",
            "Control",
            "Combines same-type input branches into a source-tagged output stream.",
            FlowDefinitionComposer.MergeNodeName,
            [
                new("Left", "Configured input type", IsInput: true),
                new("Right", "Configured input type", IsInput: true),
                new("Output", "FlowMergeItem", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: _ => CreateRoutingMergeConfiguration()),
        Component(
            "flow.assert",
            "Flow Assertion",
            "Assertion",
            "Checks a configured input stream against an expected condition and emits pass/fail result streams.",
            FlowDefinitionComposer.AssertionNodeName,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Result", "FlowAssertionResult", IsInput: false),
                new("Passed", "Configured input type", IsInput: false),
                new("Failed", "Configured input type", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: _ => CreateAssertionConfiguration()),
        Component(
            "json.schema-validator",
            "JSON Schema Validator",
            "Validator",
            "Validates MQTT payload JSON and splits valid/invalid envelopes into routeable branches.",
            "jsonSchemaValidator",
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Result", "JsonSchemaValidationResult", IsInput: false),
                new("Valid", "MqttEnvelope", IsInput: false),
                new("Invalid", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: _ => CreateJsonSchemaValidatorConfiguration()),
        Component(
            "flow.mapper",
            "Dynamic Mapper",
            "Mapper",
            "Explicitly maps one port type into another using user-authored mapping expressions.",
            FlowDefinitionComposer.MapperNodeName,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "Configured output type", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            makePreferredNodeNameUnique: true,
            createDefaultConfiguration: context => CreateDynamicMapperConfiguration("MqttPublishRequest", context.DefaultInputType)),
        Component(
            "state.reducer",
            "State Reducer",
            "State",
            "Keeps per-key state from reducer inputs and emits updated state snapshots.",
            FlowDefinitionComposer.StateReducerNodeName,
            [
                new("Input", "StateReducerInput", IsInput: true),
                new("Output", "StateReducerResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.StateReducerMapper,
            createDefaultConfiguration: _ => CreateStateReducerConfiguration()),
        Component(
            "mqtt.metrics",
            "MQTT Metrics",
            "Observer",
            "Observes MQTT messages and emits message counts, rates, payload sizes, topic activity, and latest-topic snapshots.",
            FlowDefinitionComposer.MetricsNodeName,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Snapshots", "MqttMetricsSnapshot", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            createDefaultConfiguration: _ => CreateMetricsConfiguration()),
        Component(
            "flow.logger",
            "Flow Logger",
            "Observer",
            "Captures MQTT envelopes and flow errors into structured log entries.",
            FlowDefinitionComposer.LoggerNodeName,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("FlowErrors", "FlowError", IsInput: true),
                new("Entries", "FlowLogEntry", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            makePreferredNodeNameUnique: true,
            createDefaultConfiguration: _ => CreateLoggerConfiguration()),
        Component(
            "http.request",
            "HTTP Request",
            "Actor",
            "Sends typed HTTP requests and emits typed responses or request errors.",
            FlowDefinitionComposer.HttpRequestNodeName,
            [
                new("Input", "HttpRequestInput", IsInput: true),
                new("Output", "HttpResponseOutput", IsInput: false),
                new("Errors", "HttpErrorOutput", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.PreferredMapper,
            createDefaultConfiguration: _ => CreateHttpRequestConfiguration()),
        Component(
            "mqtt.publisher",
            "MQTT Publisher",
            "Actor",
            "Publishes MqttPublishRequest values through a broker client. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
            FlowDefinitionComposer.PublisherNodeName,
            [
                new("Input", "MqttPublishRequest", IsInput: true),
                new("Entries", "FlowLogEntry", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.PreferredMapper,
            createDefaultConfiguration: context => CreateMqttPublisherConfiguration(context.ConnectionName)),
        Component(
            "mqtt.recorder",
            "MQTT Recorder",
            "Actor",
            "Stores MqttRecordingRequest values in the local session store. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
            FlowDefinitionComposer.RecorderNodeName,
            [
                new("Input", "MqttRecordingRequest", IsInput: true),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.PreferredMapper,
            createDefaultConfiguration: _ => CreateActorCapacityConfiguration()),
        Component(
            "file.writer",
            "File Writer",
            "Actor",
            "Writes FileWriteRequest values to disk. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
            "fileWriter",
            [
                new("Input", "FileWriteRequest", IsInput: true),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.PreferredMapper,
            createDefaultConfiguration: _ => CreateActorCapacityConfiguration()),
        Component(
            "mqtt.connection-state-trigger",
            "Connection State Trigger",
            "Source",
            "Emits events when a broker connection state changes.",
            FlowDefinitionComposer.StateSourceNodeName,
            [
                new("Output", "MqttClientStateChanged", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ],
            defaultInputLink: FlowComponentDefaultInputLink.None,
            createDefaultConfiguration: context => CreateConnectionReferenceConfiguration(context.ConnectionName))
    ];

    private static readonly IReadOnlyList<FlowComponentDescriptor> ComponentDescriptors =
        Metadata.Select(component => component.Descriptor).ToArray();

    public static IReadOnlyList<FlowComponentMetadata> Components => Metadata;

    public static IReadOnlyList<FlowComponentDescriptor> Descriptors => ComponentDescriptors;

    public static IReadOnlyDictionary<string, FlowComponentBehavior> PackageComponentBehaviors { get; } =
        new Dictionary<string, FlowComponentBehavior>(StringComparer.Ordinal)
        {
            ["json.parse"] = TransformBehavior("jsonParser"),
            ["json.stringify"] = TransformBehavior("jsonStringifier"),
            ["text.encode"] = TransformBehavior("textEncoder"),
            ["text.decode"] = TransformBehavior("textDecoder"),
            ["base64.encode"] = TransformBehavior("base64Encoder"),
            ["base64.decode"] = TransformBehavior("base64Decoder")
        };

    public static FlowComponentMetadata? Find(string componentType)
        => Metadata.FirstOrDefault(component => string.Equals(component.Descriptor.Type, componentType, StringComparison.Ordinal));

    public static JsonObject? CreateDefaultConfiguration(
        string componentType,
        FlowComponentDefaultConfigurationContext context)
        => Find(componentType)?.CreateDefaultConfiguration?.Invoke(context);

    private static FlowComponentMetadata Component(
        string type,
        string displayName,
        string category,
        string summary,
        string preferredNodeName,
        IReadOnlyList<ComponentPortDescriptor> ports,
        bool makePreferredNodeNameUnique = false,
        FlowComponentDefaultInputLink defaultInputLink = FlowComponentDefaultInputLink.PreferredSource,
        Func<FlowComponentDefaultConfigurationContext, JsonObject?>? createDefaultConfiguration = null)
        => new(
            new FlowComponentDescriptor(type, displayName, category, summary, IsResource: false, ports),
            preferredNodeName,
            makePreferredNodeNameUnique,
            defaultInputLink,
            createDefaultConfiguration);

    private static FlowComponentBehavior TransformBehavior(string preferredNodeName)
        => new(
            preferredNodeName,
            DefaultInputLink: FlowComponentDefaultInputLink.None,
            CreateDefaultConfiguration: _ => CreateTransformCapacityConfiguration());

    private static JsonObject CreateDynamicMapperConfiguration(string outputType, string inputType = "MqttEnvelope")
        => new()
        {
            ["engine"] = "jsonata",
            ["inputType"] = inputType,
            ["outputType"] = outputType,
            ["outputContract"] = "typed",
            ["expression"] = DynamicMapperNodeModel.DefaultExpression(outputType, "jsonata", inputType)
        };

    private static JsonObject CreateJsonSchemaValidatorConfiguration()
        => new()
        {
            ["schemaId"] = "payload-object",
            ["schema"] = """
            {
              "type": "object"
            }
            """
        };

    private static JsonObject CreateConditionRouterConfiguration(string inputType = "MqttEnvelope")
    {
        var normalizedInputType = ConditionRouterNodeModel.NormalizeInputType(inputType);
        return new()
        {
            ["inputType"] = normalizedInputType,
            ["expression"] = ConditionRouterNodeModel.DefaultExpression(normalizedInputType),
            ["boundedCapacity"] = 1000
        };
    }

    private static JsonObject CreateRoutingSwitchConfiguration(string inputType = RoutingSwitchNodeModel.DefaultInputType)
    {
        var normalizedInputType = RoutingNodeConfiguration.NormalizeInputType(inputType);
        return new()
        {
            ["inputType"] = normalizedInputType,
            ["expression"] = DefaultRoutingSwitchExpression(normalizedInputType),
            ["routes"] = new JsonArray("True", "False"),
            ["routeOutputs"] = new JsonObject
            {
                ["True"] = "WhenTrue",
                ["False"] = "WhenFalse"
            },
            ["emitRouteEnvelope"] = false,
            ["boundedCapacity"] = RoutingSwitchNodeModel.DefaultBoundedCapacity
        };
    }

    private static string DefaultRoutingSwitchExpression(string inputType)
        => inputType is FlowContractTypeNames.MqttEnvelope
            or FlowContractTypeNames.MqttPublishRequest
            or FlowContractTypeNames.MqttRecordingRequest
            or FlowContractTypeNames.InspectedMqttMessage
            or FlowContractTypeNames.JsonSchemaValidationResult
            ? RoutingSwitchNodeModel.DefaultExpression
            : "input != null";

    private static JsonObject CreateRoutingCorrelationConfiguration(string inputType = RoutingCorrelationNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(inputType),
            ["keyExpression"] = RoutingCorrelationNodeModel.DefaultKeyExpression,
            ["sideExpression"] = RoutingCorrelationNodeModel.DefaultSideExpression,
            ["requestSide"] = RoutingCorrelationNodeModel.DefaultRequestSide,
            ["responseSide"] = RoutingCorrelationNodeModel.DefaultResponseSide,
            ["caseSensitive"] = RoutingCorrelationNodeModel.DefaultCaseSensitive,
            ["timeoutMilliseconds"] = RoutingCorrelationNodeModel.DefaultTimeoutMilliseconds,
            ["maxPending"] = RoutingCorrelationNodeModel.DefaultMaxPending,
            ["boundedCapacity"] = RoutingCorrelationNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateRoutingForkConfiguration(string inputType = RoutingForkNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(inputType),
            ["outputs"] = new JsonArray("A", "B"),
            ["boundedCapacity"] = RoutingForkNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateRoutingWindowConfiguration(string inputType = RoutingWindowNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(inputType),
            ["maxItems"] = RoutingWindowNodeModel.DefaultMaxItems,
            ["timeMilliseconds"] = RoutingWindowNodeModel.DefaultTimeMilliseconds,
            ["emitPartialOnCompletion"] = RoutingWindowNodeModel.DefaultEmitPartialOnCompletion,
            ["boundedCapacity"] = RoutingWindowNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateRoutingJoinConfiguration(string inputType = RoutingJoinNodeModel.DefaultLeftInputType)
    {
        var normalizedInputType = RoutingNodeConfiguration.NormalizeInputType(inputType);
        return new()
        {
            ["leftInputType"] = normalizedInputType,
            ["rightInputType"] = normalizedInputType,
            ["leftKeyExpression"] = RoutingJoinNodeModel.DefaultLeftKeyExpression,
            ["rightKeyExpression"] = RoutingJoinNodeModel.DefaultRightKeyExpression,
            ["caseSensitive"] = RoutingJoinNodeModel.DefaultCaseSensitive,
            ["timeoutMilliseconds"] = RoutingJoinNodeModel.DefaultTimeoutMilliseconds,
            ["maxPending"] = RoutingJoinNodeModel.DefaultMaxPending,
            ["boundedCapacity"] = RoutingJoinNodeModel.DefaultBoundedCapacity
        };
    }

    private static JsonObject CreateRoutingMergeConfiguration(string inputType = RoutingMergeNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = RoutingNodeConfiguration.NormalizeInputType(inputType),
            ["inputs"] = new JsonArray("Left", "Right"),
            ["boundedCapacity"] = RoutingMergeNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateAssertionConfiguration()
        => new()
        {
            ["assertionName"] = FlowAssertionNodeModel.DefaultAssertionName,
            ["inputType"] = FlowAssertionNodeModel.DefaultInputType,
            ["expression"] = FlowAssertionNodeModel.DefaultExpression,
            ["failureMessage"] = FlowAssertionNodeModel.DefaultFailureMessage,
            ["boundedCapacity"] = FlowAssertionNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateStateReducerConfiguration()
        => new()
        {
            ["engine"] = StateReducerNodeModel.DefaultEngine,
            ["reducer"] = StateReducerNodeModel.DefaultReducer,
            ["boundedCapacity"] = StateReducerNodeModel.DefaultBoundedCapacity,
            ["maxKeys"] = StateReducerNodeModel.DefaultMaxKeys
        };

    private static JsonObject CreateMqttPublisherConfiguration(string? connectionName = null)
        => new()
        {
            ["connection"] = string.IsNullOrWhiteSpace(connectionName) ? FlowDefinitionComposer.BrokerResourceName : connectionName,
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateConnectionReferenceConfiguration(string? connectionName = null)
        => new()
        {
            ["connection"] = string.IsNullOrWhiteSpace(connectionName) ? FlowDefinitionComposer.BrokerResourceName : connectionName
        };

    private static JsonObject CreateGeneratedSourceConfiguration()
        => new()
        {
            ["messages"] = SourceNodeConfiguration.BuildGeneratedMessages(
            [
                new GeneratedMessageDraft
                {
                    Topic = "factory/sample",
                    Payload = """{"value":21.7,"unit":"c","status":"ok"}"""
                }
            ]),
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateReplaySourceConfiguration()
        => new()
        {
            ["sessionId"] = string.Empty,
            ["speed"] = 1,
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateStoredSessionSourceConfiguration()
        => new()
        {
            ["sessionId"] = string.Empty,
            ["preserveTiming"] = false,
            ["speed"] = 1,
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateTimerIntervalConfiguration()
        => new()
        {
            ["intervalMilliseconds"] = TimerIntervalNodeModel.DefaultIntervalMilliseconds,
            ["initialDelayMilliseconds"] = TimerIntervalNodeModel.DefaultInitialDelayMilliseconds,
            ["emitImmediately"] = true,
            ["boundedCapacity"] = TimerIntervalNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerScheduleConfiguration()
        => new()
        {
            ["cron"] = TimerScheduleNodeModel.DefaultCron,
            ["timeZoneId"] = TimerScheduleNodeModel.DefaultTimeZoneId,
            ["boundedCapacity"] = TimerScheduleNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerDelayConfiguration(string inputType = TimerDelayNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = TimerDelayNodeModel.NormalizeInputType(inputType),
            ["delayMilliseconds"] = TimerDelayNodeModel.DefaultDelayMilliseconds,
            ["boundedCapacity"] = TimerDelayNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerDebounceConfiguration(string inputType = TimerDelayNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = TimerDelayNodeModel.NormalizeInputType(inputType),
            ["quietPeriodMilliseconds"] = TimerDebounceNodeModel.DefaultQuietPeriodMilliseconds,
            ["boundedCapacity"] = TimerDebounceNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateTimerThrottleConfiguration(string inputType = TimerDelayNodeModel.DefaultInputType)
        => new()
        {
            ["inputType"] = TimerDelayNodeModel.NormalizeInputType(inputType),
            ["intervalMilliseconds"] = TimerThrottleNodeModel.DefaultIntervalMilliseconds,
            ["emitFirstImmediately"] = true,
            ["boundedCapacity"] = TimerThrottleNodeModel.DefaultBoundedCapacity
        };

    private static JsonObject CreateLoggerConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000,
            ["maxEntries"] = 500,
            ["includePayloadPreview"] = true,
            ["maxPayloadPreviewChars"] = 512
        };

    private static JsonObject CreateMetricsConfiguration()
        => new()
        {
            ["boundedCapacity"] = MqttMetricsNodeModel.DefaultBoundedCapacity,
            ["rateWindowSeconds"] = MqttMetricsNodeModel.DefaultRateWindowSeconds,
            ["metricCardColumns"] = MqttMetricsNodeModel.DefaultMetricCardColumns,
            ["displayMetrics"] = MqttMetricsNodeModel.BuildDisplayMetrics(MqttMetricsNodeModel.DefaultDisplayMetrics)
        };

    private static JsonObject CreateHttpRequestConfiguration()
        => new()
        {
            ["defaultTimeoutMilliseconds"] = 30000,
            ["maxResponseBodyBytes"] = 1048576,
            ["followRedirects"] = true,
            ["treatNonSuccessStatusAsError"] = false,
            ["boundedCapacity"] = 128
        };

    private static JsonObject CreatePayloadInspectConfiguration()
        => new()
        {
            ["maxPreviewBytes"] = 1024,
            ["maxFormattedChars"] = 4096,
            ["detectBase64"] = true,
            ["formatJson"] = true,
            ["formatXml"] = true,
            ["boundedCapacity"] = 128
        };

    private static JsonObject CreateActorCapacityConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000
        };

    private static JsonObject CreateTransformCapacityConfiguration()
        => new()
        {
            ["boundedCapacity"] = 1000
        };
}
