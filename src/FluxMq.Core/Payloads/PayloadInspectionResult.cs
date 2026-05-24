using System.Text.Json;

namespace FluxMq.Core.Payloads;

public sealed record PayloadInspectionResult
{
    public PayloadFormat Format { get; init; }
    public int SizeBytes { get; init; }
    public bool IsText { get; init; }
    public string ContentTypeLabel { get; init; } = string.Empty;
    public JsonValueKind JsonValueKind { get; init; } = JsonValueKind.Undefined;
    public string DisplayTypeLabel => ContentTypeLabel;
    public string RawText { get; init; } = string.Empty;
    public string FormattedText { get; init; } = string.Empty;
    public string HexDump { get; init; } = string.Empty;
    public string Metadata { get; init; } = string.Empty;
}
