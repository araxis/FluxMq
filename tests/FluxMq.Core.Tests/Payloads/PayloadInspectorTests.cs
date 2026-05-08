using System.Text;
using FluxMq.Core.Payloads;
using FluentAssertions;

namespace FluxMq.Core.Tests.Payloads;

public sealed class PayloadInspectorTests
{
    [Fact]
    public void Inspect_DetectsAndFormatsJson()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("""{"device":"pump-1","value":42}"""));

        result.Format.Should().Be(PayloadFormat.Json);
        result.ContentTypeLabel.Should().Be("JSON");
        result.FormattedText.Should().Contain("\"device\": \"pump-1\"");
        result.HexDump.Should().Contain("00000000");
    }

    [Fact]
    public void Inspect_DetectsAndFormatsXml()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("<root><value>42</value></root>"));

        result.Format.Should().Be(PayloadFormat.Xml);
        result.FormattedText.Should().Contain("<value>42</value>");
    }

    [Fact]
    public void Inspect_DetectsBase64Text()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("SGVsbG8gTVFUVCE="));

        result.Format.Should().Be(PayloadFormat.Base64);
        result.FormattedText.Should().Contain("Decoded bytes: 11");
    }

    [Fact]
    public void Inspect_DetectsPlainText()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("temperature=21.4"));

        result.Format.Should().Be(PayloadFormat.Text);
        result.RawText.Should().Be("temperature=21.4");
    }

    [Fact]
    public void Inspect_DetectsBinaryPayload()
    {
        var result = PayloadInspector.Inspect([0xFF, 0x00, 0x10, 0x80]);

        result.Format.Should().Be(PayloadFormat.Binary);
        result.IsText.Should().BeFalse();
        result.HexDump.Should().StartWith("00000000  FF 00 10 80");
        result.HexDump.Should().EndWith("....");
    }

    [Fact]
    public void Inspect_HandlesEmptyPayload()
    {
        var result = PayloadInspector.Inspect([]);

        result.Format.Should().Be(PayloadFormat.Empty);
        result.SizeBytes.Should().Be(0);
        result.HexDump.Should().BeEmpty();
    }
}
