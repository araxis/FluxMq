using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class FlowComponentCatalog
{
    private readonly IReadOnlyList<FlowComponentDescriptor> _components =
    [
        new(
            "mqtt.trigger",
            "Live MQTT Trigger",
            "Source",
            "Subscribes to a configured broker connection and emits live MQTT envelopes.",
            IsResource: false,
            [
                new("Connection", "MqttConnection", IsInput: true),
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.live-source",
            "Live MQTT Source",
            "Source",
            "Connects directly to a broker, subscribes, and emits live MQTT envelopes.",
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
            "Replays a selected message sequence through the pipeline.",
            IsResource: false,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "generated.source",
            "Generated Message Source",
            "Source",
            "Emits deterministic generated MQTT messages for tests and demos.",
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
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.metrics",
            "MQTT Metrics",
            "Observer",
            "Observes MQTT messages and emits message counts, payload sizes, topic activity, and latest-topic snapshots.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Snapshots", "MqttMetricsSnapshot", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.publish-request",
            "Publish Request",
            "Mapper",
            "Dynamically maps MQTT envelopes into MQTT publish requests.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "MqttPublishRequest", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.publisher",
            "MQTT Publisher",
            "Actor",
            "Publishes explicit MQTT publish requests through a broker session.",
            IsResource: false,
            [
                new("Connection", "MqttConnection", IsInput: true),
                new("Input", "MqttPublishRequest", IsInput: true),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.recording-request",
            "Recording Request",
            "Mapper",
            "Builds recording write requests from incoming MQTT envelopes.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "MqttRecordingRequest", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.recorder",
            "MQTT Recorder",
            "Actor",
            "Stores explicit MQTT recording requests in the local session store.",
            IsResource: false,
            [
                new("Input", "MqttRecordingRequest", IsInput: true),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "file.write-request",
            "File Write Request",
            "Mapper",
            "Dynamically maps MQTT envelopes into file write requests.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "FileWriteRequest", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "file.writer",
            "File Writer",
            "Actor",
            "Writes explicit file write requests to disk.",
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
        => _components.FirstOrDefault(component => string.Equals(component.Type, type, StringComparison.Ordinal));
}
