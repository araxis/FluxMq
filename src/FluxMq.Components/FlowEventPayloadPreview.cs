using System.Text;

namespace FluxMq.Components;

internal static class FlowEventPayloadPreview
{
    private const int DefaultMaxChars = 512;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static string? FromBytes(byte[] payload, int maxChars = DefaultMaxChars)
    {
        if (maxChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChars), maxChars, "Preview length must be positive.");
        }

        if (payload.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var text = StrictUtf8.GetString(payload);
            return text.Length <= maxChars ? text : text[..maxChars];
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
