using FluxMq.Core.Models;
using FluxMq.Core.Payloads;

namespace FluxMq.UI.Services;

public sealed record WorkspaceProjectionSnapshot(
    IReadOnlyList<MqttEnvelope> RecentMessages,
    MqttEnvelope? LatestMessage,
    PayloadInspectionResult LatestInspection,
    MqttEnvelope? SelectedMessage,
    PayloadInspectionResult SelectedInspection);
