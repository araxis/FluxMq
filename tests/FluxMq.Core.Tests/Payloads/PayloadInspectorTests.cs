using System.Text;
using FluxMq.Core.Payloads;
using Shouldly;

namespace FluxMq.Core.Tests.Payloads;

public sealed class PayloadInspectorTests
{
    [Fact]
    public void Inspect_DetectsAndFormatsJson()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("""{"device":"pump-1","value":42}"""));

        result.Format.ShouldBe(PayloadFormat.Json);
        result.ContentTypeLabel.ShouldBe("JSON");
        result.DisplayTypeLabel.ShouldBe("JSON");
        result.FormattedText.ShouldContain("\"device\": \"pump-1\"");
        result.HexDump.ShouldContain("00000000");
    }

    [Theory]
    [InlineData("1", PayloadFormat.Number, "Number")]
    [InlineData("true", PayloadFormat.Boolean, "Boolean")]
    [InlineData("false", PayloadFormat.Boolean, "Boolean")]
    [InlineData("null", PayloadFormat.Null, "Null")]
    [InlineData("\"hello\"", PayloadFormat.String, "String")]
    [InlineData("[1,2]", PayloadFormat.Array, "Array")]
    public void Inspect_DetectsJsonValueKind(string payload, PayloadFormat expectedFormat, string expectedLabel)
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes(payload));

        result.Format.ShouldBe(expectedFormat);
        result.ContentTypeLabel.ShouldBe(expectedLabel);
        result.DisplayTypeLabel.ShouldBe(expectedLabel);
    }

    [Fact]
    public void Inspect_DetectsAndFormatsXml()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("<root><value>42</value></root>"));

        result.Format.ShouldBe(PayloadFormat.Xml);
        result.FormattedText.ShouldContain("<value>42</value>");
    }

    [Fact]
    public void Inspect_DetectsBase64Text()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("SGVsbG8gTVFUVCE="));

        result.Format.ShouldBe(PayloadFormat.Base64);
        result.FormattedText.ShouldContain("Decoded bytes: 11");
    }

    [Fact]
    public void Inspect_DetectsPlainText()
    {
        var result = PayloadInspector.Inspect(Encoding.UTF8.GetBytes("temperature=21.4"));

        result.Format.ShouldBe(PayloadFormat.Text);
        result.RawText.ShouldBe("temperature=21.4");
    }

    [Fact]
    public void Inspect_DetectsBinaryPayload()
    {
        var result = PayloadInspector.Inspect([0xFF, 0x00, 0x10, 0x80]);

        result.Format.ShouldBe(PayloadFormat.Binary);
        result.IsText.ShouldBeFalse();
        result.HexDump.ShouldStartWith("00000000  FF 00 10 80");
        result.HexDump.ShouldNotContain(".");
    }

    [Fact]
    public void Inspect_HandlesEmptyPayload()
    {
        var result = PayloadInspector.Inspect([]);

        result.Format.ShouldBe(PayloadFormat.Empty);
        result.SizeBytes.ShouldBe(0);
        result.HexDump.ShouldBeEmpty();
    }
}
