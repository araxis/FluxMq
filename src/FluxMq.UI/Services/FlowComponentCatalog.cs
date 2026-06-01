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
            "payload.inspect",
            "Payload Inspect",
            "Mapper",
            "Classifies byte or text payload requests and emits preview metadata.",
            IsResource: false,
            [
                new("Input", "PayloadInspectionRequest", IsInput: true),
                new("Output", "PayloadInspectionResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "timer.interval",
            "Timer Interval",
            "Source",
            "Emits timer ticks at a fixed interval.",
            IsResource: false,
            [
                new("Output", "TimerTick", IsInput: false)
            ]),
        new(
            "timer.schedule",
            "Scheduled Timer",
            "Source",
            "Emits schedule ticks from a cron expression.",
            IsResource: false,
            [
                new("Output", "ScheduleTick", IsInput: false)
            ]),
        new(
            "timer.delay",
            "Delay",
            "Control",
            "Delays inputs and emits them unchanged.",
            IsResource: false,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "Configured input type", IsInput: false)
            ]),
        new(
            "timer.debounce",
            "Debounce",
            "Control",
            "Emits the latest input after a quiet period.",
            IsResource: false,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "Configured input type", IsInput: false)
            ]),
        new(
            "timer.throttle",
            "Throttle",
            "Control",
            "Limits input emissions to a fixed interval.",
            IsResource: false,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Output", "Configured input type", IsInput: false)
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
            "flow.filter",
            "Flow Filter",
            "Control",
            "Lets matching inputs continue downstream and drops the rest.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("Output", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "flow.when",
            "When",
            "Control",
            "Splits inputs into true and false branches.",
            IsResource: false,
            [
                new("Input", "MqttEnvelope", IsInput: true),
                new("WhenTrue", "MqttEnvelope", IsInput: false),
                new("WhenFalse", "MqttEnvelope", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "flow.assert",
            "Flow Assertion",
            "Assertion",
            "Checks a configured input stream against an expected condition and emits pass/fail result streams.",
            IsResource: false,
            [
                new("Input", "Configured input type", IsInput: true),
                new("Result", "FlowAssertionResult", IsInput: false),
                new("Passed", "Configured input type", IsInput: false),
                new("Failed", "Configured input type", IsInput: false),
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
            "json.parse",
            "JSON Parse",
            "Transform",
            "Parses text or bytes into a JSON value.",
            IsResource: false,
            [
                new("Input", "JsonParseRequest", IsInput: true),
                new("Output", "JsonParseResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "json.stringify",
            "JSON Stringify",
            "Transform",
            "Serializes a value into JSON text and bytes.",
            IsResource: false,
            [
                new("Input", "JsonStringifyRequest", IsInput: true),
                new("Output", "JsonStringifyResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "text.encode",
            "Text Encode",
            "Transform",
            "Encodes text into bytes.",
            IsResource: false,
            [
                new("Input", "TextEncodeRequest", IsInput: true),
                new("Output", "TextEncodeResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "text.decode",
            "Text Decode",
            "Transform",
            "Decodes bytes into text.",
            IsResource: false,
            [
                new("Input", "TextDecodeRequest", IsInput: true),
                new("Output", "TextDecodeResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "base64.encode",
            "Base64 Encode",
            "Transform",
            "Encodes bytes or text into base64 text.",
            IsResource: false,
            [
                new("Input", "Base64EncodeRequest", IsInput: true),
                new("Output", "Base64EncodeResult", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ]),
        new(
            "base64.decode",
            "Base64 Decode",
            "Transform",
            "Decodes base64 text into bytes and optional text.",
            IsResource: false,
            [
                new("Input", "Base64DecodeRequest", IsInput: true),
                new("Output", "Base64DecodeResult", IsInput: false),
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
            "http.request",
            "HTTP Request",
            "Actor",
            "Sends typed HTTP requests and emits typed responses or request errors.",
            IsResource: false,
            [
                new("Input", "HttpRequestInput", IsInput: true),
                new("Output", "HttpResponseOutput", IsInput: false),
                new("Errors", "HttpErrorOutput", IsInput: false)
            ]),
        new(
            "mqtt.publisher",
            "MQTT Publisher",
            "Actor",
            "Publishes MqttPublishRequest values through a broker client. Add a Dynamic Mapper upstream when starting from MQTT envelopes.",
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
                new("Output", "MqttClientStateChanged", IsInput: false),
                new("Errors", "FlowError", IsInput: false)
            ])
    ];

    public IReadOnlyList<FlowComponentDescriptor> Components => _components;

    public FlowComponentDescriptor? Find(string type)
        => _components.FirstOrDefault(component => string.Equals(component.Type, type, StringComparison.Ordinal));
}
