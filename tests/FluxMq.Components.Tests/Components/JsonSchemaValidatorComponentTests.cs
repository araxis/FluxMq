using FluxMq.Components.JsonSchema;
using FluxMq.Core.Models;
using MQTTnet.Protocol;
using Shouldly;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Components;

public sealed class JsonSchemaValidatorComponentTests
{
    [Fact]
    public async Task Input_ValidatesMqttPayloadAgainstSchema()
    {
        var component = new JsonSchemaValidatorComponent(new JsonSchemaValidatorDefinition
        {
            SchemaId = "status-schema",
            SchemaJson = """
            {
              "type": "object",
              "required": ["status"],
              "properties": {
                "status": { "const": "ok" }
              }
            }
            """
        });
        var output = new BufferBlock<JsonSchemaValidationResult>();

        component.Result.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(CreateEnvelope("""{"status":"ok"}"""));
        component.Input.Post(CreateEnvelope("""{"status":"fault"}"""));
        component.Complete();

        var valid = await output.ReceiveAsync();
        var invalid = await output.ReceiveAsync();
        await component.Completion;

        valid.SchemaId.ShouldBe("status-schema");
        valid.IsValid.ShouldBeTrue();
        valid.Issues.ShouldBeEmpty();
        invalid.IsValid.ShouldBeFalse();
        invalid.Issues.ShouldNotBeEmpty();
        invalid.Issues[0].Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Input_InvalidJsonPayload_ReturnsInvalidResult()
    {
        var component = new JsonSchemaValidatorComponent(new JsonSchemaValidatorDefinition
        {
            SchemaJson = """{"type":"object"}"""
        });
        var output = new BufferBlock<JsonSchemaValidationResult>();

        component.Result.LinkTo(output, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(CreateEnvelope("not-json"));
        component.Complete();

        var result = await output.ReceiveAsync();
        await component.Completion;

        result.IsValid.ShouldBeFalse();
        result.Issues.ShouldHaveSingleItem();
        result.Issues[0].Message.ShouldContain("Payload is not valid JSON");
    }

    [Fact]
    public async Task Input_RoutesValidAndInvalidEnvelopesToBranchOutputs()
    {
        var component = new JsonSchemaValidatorComponent(new JsonSchemaValidatorDefinition
        {
            SchemaJson = """
            {
              "type": "object",
              "required": ["status"],
              "properties": {
                "status": { "const": "ok" }
              }
            }
            """
        });
        var validTopics = new List<string>();
        var invalidTopics = new List<string>();
        var validSink = new ActionBlock<MqttEnvelope>(envelope => validTopics.Add(envelope.Topic));
        var invalidSink = new ActionBlock<MqttEnvelope>(envelope => invalidTopics.Add(envelope.Topic));

        component.Valid.LinkTo(validSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Invalid.LinkTo(invalidSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(CreateEnvelope("""{"status":"ok"}""", "factory/valid"));
        component.Input.Post(CreateEnvelope("""{"status":"fault"}""", "factory/invalid"));
        component.Complete();

        await Task.WhenAll(component.Completion, validSink.Completion, invalidSink.Completion);

        validTopics.ShouldBe(["factory/valid"]);
        invalidTopics.ShouldBe(["factory/invalid"]);
    }

    private static MqttEnvelope CreateEnvelope(string payload, string topic = "factory/status")
        => new()
        {
            Topic = topic,
            Payload = Encoding.UTF8.GetBytes(payload),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            ReceivedAt = DateTimeOffset.Parse("2026-05-23T10:00:00Z")
        };
}
