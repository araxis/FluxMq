using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace FluxMq.Core.Payloads;

public static class PayloadInspector
{
    public static PayloadInspectionResult Inspect(byte[]? payload)
    {
        payload ??= [];

        if (payload.Length == 0)
        {
            return BuildResult(PayloadFormat.Empty, payload, isText: true, rawText: string.Empty, formattedText: string.Empty);
        }

        if (!TryDecodeUtf8(payload, out var text))
        {
            return BuildResult(PayloadFormat.Binary, payload, isText: false, rawText: string.Empty, formattedText: string.Empty);
        }

        var trimmed = text.Trim();

        if (TryFormatJson(trimmed, out var formattedJson))
        {
            return BuildResult(PayloadFormat.Json, payload, isText: true, rawText: text, formattedText: formattedJson);
        }

        if (TryFormatXml(trimmed, out var formattedXml))
        {
            return BuildResult(PayloadFormat.Xml, payload, isText: true, rawText: text, formattedText: formattedXml);
        }

        if (TryDetectBase64(trimmed, out var decodedLength))
        {
            var formatted = $"Base64 payload\nDecoded bytes: {decodedLength}";
            return BuildResult(PayloadFormat.Base64, payload, isText: true, rawText: text, formattedText: formatted);
        }

        if (LooksLikeText(text))
        {
            return BuildResult(PayloadFormat.Text, payload, isText: true, rawText: text, formattedText: text);
        }

        return BuildResult(PayloadFormat.Binary, payload, isText: false, rawText: string.Empty, formattedText: string.Empty);
    }

    private static PayloadInspectionResult BuildResult(
        PayloadFormat format,
        byte[] payload,
        bool isText,
        string rawText,
        string formattedText)
    {
        var label = format switch
        {
            PayloadFormat.Empty => "Empty",
            PayloadFormat.Json => "JSON",
            PayloadFormat.Xml => "XML",
            PayloadFormat.Base64 => "Base64",
            PayloadFormat.Text => "Text",
            PayloadFormat.Binary => "Binary",
            _ => "Unknown"
        };

        return new PayloadInspectionResult
        {
            Format = format,
            SizeBytes = payload.Length,
            IsText = isText,
            ContentTypeLabel = label,
            RawText = rawText,
            FormattedText = formattedText,
            HexDump = BuildHexDump(payload),
            Metadata = $"{payload.Length} bytes | {label}"
        };
    }

    private static bool TryDecodeUtf8(byte[] payload, out string text)
    {
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = encoding.GetString(payload);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static bool TryFormatJson(string text, out string formatted)
    {
        formatted = string.Empty;

        if (string.IsNullOrWhiteSpace(text) || (text[0] != '{' && text[0] != '['))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            formatted = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryFormatXml(string text, out string formatted)
    {
        formatted = string.Empty;

        if (string.IsNullOrWhiteSpace(text) || text[0] != '<')
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(text, LoadOptions.None);
            formatted = document.ToString(SaveOptions.None);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool TryDetectBase64(string text, out int decodedLength)
    {
        decodedLength = 0;

        if (text.Length < 8 || text.Length % 4 != 0 || text.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var bytes = Encoding.ASCII.GetBytes(text);
        Span<byte> buffer = stackalloc byte[bytes.Length];
        if (!Base64.DecodeFromUtf8(bytes, buffer, out _, out var bytesWritten).Equals(OperationStatus.Done))
        {
            return false;
        }

        decodedLength = bytesWritten;
        return true;
    }

    private static bool LooksLikeText(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        var controlCount = text.Count(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');
        return controlCount == 0 || controlCount <= Math.Max(1, text.Length / 100);
    }

    private static string BuildHexDump(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var offset = 0; offset < payload.Length; offset += 8)
        {
            var line = payload.AsSpan(offset, Math.Min(8, payload.Length - offset));
            builder.Append(offset.ToString("X8"));
            builder.Append("  ");

            for (var index = 0; index < 8; index++)
            {
                builder.Append(index < line.Length ? line[index].ToString("X2") : "  ");
                builder.Append(index == 3 ? "  " : " ");
            }

            if (offset + 8 < payload.Length)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
