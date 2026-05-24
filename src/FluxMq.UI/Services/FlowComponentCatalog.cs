using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class FlowComponentCatalog
{
    private readonly IReadOnlyDictionary<string, FlowComponentDescriptor> _hiddenComponents =
        new Dictionary<string, FlowComponentDescriptor>(StringComparer.Ordinal)
        {
            ["mqtt.publish-request"] = new(
                "mqtt.publish-request",
                "Publish Request Mapper",
                "Mapper",
                "Compatibility mapper for older definitions. Use Dynamic Mapper for new flows.",
                IsResource: false,
                [
                    new("Input", "MqttEnvelope", IsInput: true),
                    new("Output", "MqttPublishRequest", IsInput: false),
                    new("Errors", "FlowError", IsInput: false)
                ]),
            ["mqtt.recording-request"] = new(
                "mqtt.recording-request",
                "Recording Request Mapper",
                "Mapper",
                "Compatibility mapper for older definitions. Use Dynamic Mapper for new flows.",
                IsResource: false,
                [
                    new("Input", "MqttEnvelope", IsInput: true),
                    new("Output", "MqttRecordingRequest", IsInput: false),
                    new("Errors", "FlowError", IsInput: false)
                ]),
            ["file.write-request"] = new(
                "file.write-request",
                "File Write Request Mapper",
                "Mapper",
                "Compatibility mapper for older definitions. Use Dynamic Mapper for new flows.",
                IsResource: false,
                [
                    new("Input", "MqttEnvelope", IsInput: true),
                    new("Output", "FileWriteRequest", IsInput: false),
                    new("Errors", "FlowError", IsInput: false)
                ]),
            ["mqtt.metrics-sink"] = new(
                "mqtt.metrics-sink",
                "MQTT Metrics",
                "Observer",
                "Compatibility alias for older definitions. Use MQTT Metrics for new flows.",
                IsResource: false,
                [
                    new("Input", "MqttEnvelope", IsInput: true),
                    new("Snapshots", "MqttMetricsSnapshot", IsInput: false),
                    new("Errors", "FlowError", IsInput: false)
                ])
        };

    private readonly IReadOnlyList<FlowComponentDescriptor> _components =
    [
        new(
            "mqtt.trigger",
            "Live MQTT Trigger",
            "Source",
            "Subscribes to a configured broker connection and emits live MQTT envelopes.",
            IsResource: false,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "session.source",
            "Stored Session Source",
            "Source",
            "Replays messages from a stored MQTT recording session and emits MQTT envelopes.",
            IsResource: false,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "replay.source",
            "Replay Source",
            "Source",
            "Replays a selected stored session through the pipeline with configurable timing.",
            IsResource: false,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "generated.source",
            "MQTT Message List Source",
            "Source",
            "Emits configured MQTT envelopes from a fixed message list.",
            IsResource: false,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.payload-inspector",
            "Payload Inspector",
            "Mapper",
            "Maps MQTT messages into inspected payload results.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "InspectedMqttMessage", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.message-filter",
            "Message Filter",
            "Mapper",
            "Lets matching MQTT envelopes continue downstream and drops the rest.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.condition-router",
            "Condition Router",
            "Mapper",
            "Splits MQTT envelopes into true and false branches.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("WhenTrue", "MqttEnvelope", IsInput: false),
                new("WhenFalse", "MqttEnvelope", IsInput: false),
                new("Entries", "FlowLogEntry", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "flow.assertion",
            "Flow Assertion",
            "Assertion",
            "Checks a configured input stream against an expected condition and emits pass/fail result streams.",
            IsResource: false,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Result", "FlowAssertionResult", IsInput: false),
                new("Passed", "Configured input type", IsInput: false),
                new("Failed", "Configured input type", IsInput: false),
                new("Entries", "FlowLogEntry", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "json.schema-validator",
            "JSON Schema Validator",
            "Validator",
            "Validates MQTT payload JSON and splits valid/invalid envelopes into routeable branches.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Result", "JsonSchemaValidationResult", IsInput: false),
                new("Valid", "MqttEnvelope", IsInput: false),
                new("Invalid", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "flow.mapper",
            "Dynamic Mapper",
            "Mapper",
            "Explicitly maps one port type into another using user-authored mapping expressions.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "Configured output type", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.metrics",
            "MQTT Metrics",
            "Observer",
            "Observes MQTT messages and emits message counts, rates, payload sizes, topic activity, and latest-topic snapshots.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Snapshots", "MqttMetricsSnapshot", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "flow.logger",
            "Flow Logger",
            "Observer",
            "Captures MQTT envelopes and flow errors into structured log entries.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("FlowErrors", "FlowError", IsInput: true),
                new("Entries", "FlowLogEntry", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.publisher",
            "MQTT Publisher",
            "Actor",
            "Publishes MqttPublishRequest values through a broker session. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
            IsResource: false,
            [
                new("Input", "MqttPublishRequest", IsInput: true),
                new("Entries", "FlowLogEntry", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.recorder",
            "MQTT Recorder",
            "Actor",
            "Stores MqttRecordingRequest values in the local session store. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
            IsResource: false,
            [
                new("Input", "MqttRecordingRequest", IsInput: true),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "file.writer",
            "File Writer",
            "Actor",
            "Writes FileWriteRequest values to disk. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
            IsResource: false,
            [
                new("Input", "FileWriteRequest", IsInput: true),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.connection-state-trigger",
            "Connection State Trigger",
            "Source",
            "Emits events when a broker connection state changes.",
            IsResource: false,
            [
                new("Connection", "MqttConnection", IsInput: true),
                new("Output", "SessionStateChanged", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ])
    ];

    public IReadOnlyList<FlowComponentDescriptor> Components => _components;

    public FlowComponentDescriptor? Find(string type)
        => _components.FirstOrDefault(component => string.Equals(component.Type, type, StringComparison.Ordinal)) ??
           (_hiddenComponents.TryGetValue(type, out var descriptor) ? descriptor : null);
}
