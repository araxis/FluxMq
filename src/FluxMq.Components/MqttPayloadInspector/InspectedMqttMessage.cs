using FluxMq.Core.Models;
using FluxMq.Core.Payloads;

namespace FluxMq.Components.MqttPayloadInspector;

public sealed record InspectedMqttMessage(MqttEnvelope Envelope, PayloadInspectionResult Payload);
