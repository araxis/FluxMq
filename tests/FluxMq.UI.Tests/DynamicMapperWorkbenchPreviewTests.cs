using FluxMq.Core.Models;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using MQTTnet.Protocol;
using Shouldly;
using System.Text;

namespace FluxMq.UI.Tests;

public sealed class DynamicMapperWorkbenchPreviewTests
{
    [Fact]
    public void Variables_IncludesPayloadJsonFields()
    {
        var envelope = CreateEnvelope(
            "factory/line-a/status",
            """{"value":42,"nested":{"status":"ok"},"items":[{"id":"a"}]}""");

        var variables = DynamicMapperWorkbenchPreview.Variables(envelope);

        variables.ShouldContain(variable =>
            variable.Label == "payloadJson" &&
            variable.JsonataExpression == "payloadJson" &&
            variable.JsonOnly);
        variables.ShouldContain(variable =>
            variable.Label == "value" &&
            variable.JsonataExpression == "payloadJson.value" &&
            variable.Value == "42");
        variables.ShouldContain(variable =>
            variable.Label == "status" &&
            variable.JsonataExpression == "payloadJson.nested.status" &&
            variable.Value == "ok");
        variables.ShouldContain(variable =>
            variable.Label == "id" &&
            variable.JsonataExpression == "payloadJson.items[0].id" &&
            variable.Value == "a");
    }

    [Fact]
    public void Variables_LimitsWidePayloadJsonTrees()
    {
        var fields = string.Join(",", Enumerable.Range(0, 80).Select(index => $@"""field{index}"":{index}"));
        var envelope = CreateEnvelope("factory/line-a/status", "{" + fields + "}");

        var variables = DynamicMapperWorkbenchPreview.Variables(envelope);

        variables.Count.ShouldBeLessThanOrEqualTo(32);
    }

    [Fact]
    public void ParseInputJson_EnvelopeObject_UpdatesSampleEnvelope()
    {
        const string inputJson = """
        {
          "topic": "factory/line-b/status",
          "qos": 2,
          "retain": true,
          "payload": {
            "status": "warn"
          }
        }
        """;

        var result = DynamicMapperWorkbenchPreview.ParseInputJson(inputJson);

        result.Success.ShouldBeTrue(result.Error);
        result.Envelope.ShouldNotBeNull();
        result.Envelope.Topic.ShouldBe("factory/line-b/status");
        result.Envelope.QualityOfService.ShouldBe(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce);
        result.Envelope.Retain.ShouldBeTrue();
        Encoding.UTF8.GetString(result.Envelope.Payload).ShouldContain("\"status\": \"warn\"");
    }

    [Fact]
    public void ParseInputJson_RawJsonObject_TreatsObjectAsPayload()
    {
        const string inputJson = """
        {
          "status": "ok",
          "value": 42
        }
        """;

        var result = DynamicMapperWorkbenchPreview.ParseInputJson(inputJson);

        result.Success.ShouldBeTrue(result.Error);
        result.Envelope.ShouldNotBeNull();
        result.Envelope.Topic.ShouldBe("sample/topic");
        Encoding.UTF8.GetString(result.Envelope.Payload).ShouldContain("\"value\": 42");
    }

    [Fact]
    public void Preview_JsonataPublishRequest_UsesRuntimeEngine()
    {
        var envelope = CreateEnvelope("factory/line-a/status", """{"status":"ok"}""");
        const string expression = """
        {
          "topic": "mirror/" & topic,
          "payload": "mapped:" & payloadJson.status,
          "qos": 1,
          "retain": false
        }
        """;

        var preview = DynamicMapperWorkbenchPreview.Preview("jsonata", "MqttPublishRequest", expression, envelope);

        preview.Success.ShouldBeTrue(preview.Error);
        preview.Properties.Single(property => property.Name == "Topic").Value
            .ShouldBe("mirror/factory/line-a/status");
        preview.Properties.Single(property => property.Name == "Payload").Value
            .ShouldBe("mapped:ok");
        preview.Properties.Single(property => property.Name == "QoS").Value.ShouldBe("1");
        preview.Properties.Single(property => property.Name == "Retain").Value.ShouldBe("False");
        preview.Json.ShouldContain("\"topic\": \"mirror/factory/line-a/status\"");
        preview.Json.ShouldContain("\"payload\": \"mapped:ok\"");
    }

    [Fact]
    public void OutputFields_UseSelectedEngineExpressionExamples()
    {
        var dynamicFields = DynamicMapperWorkbenchPreview.OutputFields("MqttPublishRequest", "dynamic-expresso");
        var jsonataFields = DynamicMapperWorkbenchPreview.OutputFields("MqttPublishRequest", "jsonata");

        dynamicFields.Single(field => field.Name == "topic").Example
            .ShouldBe("\"mirror/\" + topic");
        jsonataFields.Single(field => field.Name == "topic").Example
            .ShouldBe("\"mirror/\" & topic");
    }

    [Fact]
    public void OutputFields_AnyContract_HasGenericResultShape()
    {
        var fields = DynamicMapperWorkbenchPreview.OutputFields("Any", "jsonata");

        fields.ShouldHaveSingleItem();
        fields[0].Name.ShouldBe("result");
        fields[0].Type.ShouldBe("any JSON");
    }

    [Fact]
    public void PreviewAny_Jsonata_ReturnsRawExpressionObject()
    {
        var envelope = CreateEnvelope("factory/line-a/status", """{"status":"ok"}""");
        const string expression = """
        {
          "topic": topic,
          "status": payloadJson.status
        }
        """;

        var preview = DynamicMapperWorkbenchPreview.PreviewAny("jsonata", expression, envelope);

        preview.Success.ShouldBeTrue(preview.Error);
        preview.Title.ShouldBe("Any JSON");
        preview.Json.ShouldContain("\"topic\": \"factory/line-a/status\"");
        preview.Json.ShouldContain("\"status\": \"ok\"");
    }

    [Fact]
    public void Preview_DynamicExpressoFileWriteRequest_UsesRuntimeEngine()
    {
        var envelope = CreateEnvelope("factory/line-a/status", """{"status":"ok"}""");
        const string expression = """
        new FileWriteRequest {
          Path = "messages/" + topic + ".json",
          Content = Encoding.UTF8.GetBytes(payloadText),
          Mode = FileWriteMode.Append,
          CreateDirectory = true
        }
        """;

        var preview = DynamicMapperWorkbenchPreview.Preview("dynamic-expresso", "FileWriteRequest", expression, envelope);

        preview.Success.ShouldBeTrue(preview.Error);
        preview.Properties.Single(property => property.Name == "Path").Value
            .ShouldBe("messages/factory/line-a/status.json");
        preview.Properties.Single(property => property.Name == "Content").Value
            .ShouldBe("""{"status":"ok"}""");
        preview.Properties.Single(property => property.Name == "Mode").Value.ShouldBe("Append");
        preview.Properties.Single(property => property.Name == "Create directory").Value.ShouldBe("True");
    }

    [Fact]
    public void Preview_RecordingRequest_RequiresLiteralSessionId()
    {
        var envelope = CreateEnvelope("factory/line-a/status", "{}");

        var preview = DynamicMapperWorkbenchPreview.Preview(
            "jsonata",
            "MqttRecordingRequest",
            "{}",
            envelope);

        preview.Success.ShouldBeFalse();
        preview.Error.ShouldNotBeNull();
        preview.Error.ShouldContain("sessionId");
    }

    private static MqttEnvelope CreateEnvelope(string topic, string payload)
        => new()
        {
            Topic = topic,
            Payload = Encoding.UTF8.GetBytes(payload),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            ReceivedAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z")
        };
}
