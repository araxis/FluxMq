using FluxMq.UI.Models;

namespace FluxMq.UI.Services;

public sealed class FlowComponentCatalog
{
    private readonly IReadOnlyList<FlowComponentDescriptor> _components =
    [
        new(
            "session.source",
            "Session Source",
            "Source",
            "Replays messages from a stored MQTT recording session and emits MQTT envelopes.",
            IsResource: false,
            [
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.connection",
            "MQTT Connection",
            "Resource",
            "Defines broker settings (host, port, credentials, TLS) and owns the live session. Triggers reference it by name.",
            IsResource: true,
            [
                new("Connection", "MqttConnection", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "mqtt.trigger",
            "MQTT Trigger",
            "Source",
            "Subscribes to an MQTT broker and emits envelopes whose topic matches one of its filters.",
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
            "mqtt.metrics-sink",
            "MQTT Metrics Sink",
            "Sink",
            "Aggregates message counts, payload sizes, topic activity, and latest topic.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Snapshots", "MqttMetricsSnapshot", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ])
    ];

    public IReadOnlyList<FlowComponentDescriptor> Components => _components;

    public FlowComponentDescriptor? Find(string type)
        => _components.FirstOrDefault(component => string.Equals(component.Type, type, StringComparison.Ordinal));
}
