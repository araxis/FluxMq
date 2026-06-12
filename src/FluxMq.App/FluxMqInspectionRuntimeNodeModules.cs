using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Core.Metrics;
using FluxMq.Core.Models;
using System.Text.Json;
using static FluxMq.App.FluxMqRuntimeNodeConfigurationReader;

namespace FluxMq.App;

internal sealed class PayloadInspectorRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.PayloadInspector;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqInspectionNodeConfiguration.CreatePayloadInspector(context.Address, context.Definition);
}

internal sealed class MqttMetricsRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MqttMetrics;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqInspectionNodeConfiguration.CreateMqttMetrics(context.Address, context.Definition);
}

internal sealed class FlowLoggerRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.FlowLogger;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqInspectionNodeConfiguration.CreateFlowLogger(context.Address, context.Definition);
}

internal sealed class JsonSchemaValidatorRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.JsonSchemaValidator;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => FluxMqInspectionNodeConfiguration.CreateJsonSchemaValidator(context.Address, context.Definition);
}

internal static class FluxMqInspectionNodeConfiguration
{
    private static readonly PortName InputPort = new("Input");
    private static readonly PortName OutputPort = new("Output");
    private static readonly PortName ResultPort = new("Result");
    private static readonly PortName ValidPort = new("Valid");
    private static readonly PortName InvalidPort = new("Invalid");
    private static readonly PortName SnapshotsPort = new("Snapshots");
    private static readonly PortName EntriesPort = new("Entries");
    private static readonly PortName FlowErrorsPort = new("FlowErrors");
    private static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNode CreatePayloadInspector(NodeAddress address, NodeDefinition definition)
    {
        var component = new PayloadInspectorMapperComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<InspectedMqttMessage>(address.Port(OutputPort), component.Output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    public static RuntimeNode CreateMqttMetrics(NodeAddress address, NodeDefinition definition)
    {
        var component = new MqttMetricsComponent(
            boundedCapacity: GetBoundedCapacity(definition),
            rateWindow: TimeSpan.FromSeconds(GetDoubleOrDefault(
                definition,
                "rateWindowSeconds",
                MqttMetricsComponent.DefaultRateWindow.TotalSeconds)));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<MqttMetricsSnapshot>(address.Port(SnapshotsPort), component.Snapshots),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    public static RuntimeNode CreateFlowLogger(NodeAddress address, NodeDefinition definition)
    {
        var hasMessageInput = definition.Ports.ContainsKey(InputPort.Value);
        var hasFlowErrorInput = definition.Ports.ContainsKey(FlowErrorsPort.Value);
        if (!hasMessageInput && !hasFlowErrorInput)
        {
            hasMessageInput = true;
        }

        var component = new FlowLoggerComponent(
            boundedCapacity: GetBoundedCapacity(definition),
            maxEntries: GetIntOrDefault(definition, "maxEntries", 500, minValue: 1),
            includePayloadPreview: GetBoolOrDefault(definition, "includePayloadPreview", false),
            maxPayloadPreviewChars: GetIntOrDefault(definition, "maxPayloadPreviewChars", 512, minValue: 1),
            waitForMessageInputCompletion: hasMessageInput,
            waitForFlowErrorInputCompletion: hasFlowErrorInput);

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input),
                new InputPort<FlowError>(address.Port(FlowErrorsPort), component.FlowErrors)
            ],
            outputs:
            [
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    public static RuntimeNode CreateJsonSchemaValidator(NodeAddress address, NodeDefinition definition)
    {
        var component = new JsonSchemaValidatorComponent(
            GetJsonSchemaValidatorDefinition(definition),
            boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttEnvelope>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<JsonSchemaValidationResult>(address.Port(ResultPort), component.Result),
                new OutputPort<MqttEnvelope>(address.Port(ValidPort), component.Valid),
                new OutputPort<MqttEnvelope>(address.Port(InvalidPort), component.Invalid),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    private static JsonSchemaValidatorDefinition GetJsonSchemaValidatorDefinition(NodeDefinition definition)
    {
        var schema = GetOptionalJsonSchema(definition);
        var schemaPath = GetNullableString(definition, "schemaPath") ?? GetNullableString(definition, "schemaFile");
        if (schema is null && string.IsNullOrWhiteSpace(schemaPath))
        {
            throw new InvalidOperationException(
                "JSON Schema validator requires configuration value 'schema' or 'schemaPath'.");
        }

        return new JsonSchemaValidatorDefinition
        {
            SchemaJson = schema,
            SchemaPath = schemaPath,
            SchemaId = GetNullableString(definition, "schemaId") ?? schemaPath ?? "inline"
        };
    }

    private static string? GetOptionalJsonSchema(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("schema", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var schema = value.GetString();
            return string.IsNullOrWhiteSpace(schema) ? null : schema;
        }

        return value.GetRawText();
    }
}
