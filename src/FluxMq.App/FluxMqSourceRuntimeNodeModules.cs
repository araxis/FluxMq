using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using FluxFlow.Components.Sources.Nodes;
using FluxFlow.Components.Sources.Options;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using FluxMq.App.Metrics;
using FluxMq.Components.MessageSource;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Metrics;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using MQTTnet.Protocol;
using static FluxMq.App.FluxMqRuntimeNodeConfigurationReader;

namespace FluxMq.App;

internal sealed class StoredSessionSourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.StoredSessionSource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
    {
        var sessionId = GetOptionalSessionId(context.Definition, "sessionId");
        if (sessionId is null)
        {
            return CreateEmptyMqttSource(context.Address, context.Definition);
        }

        if (context.MessageRepository is null)
        {
            throw new InvalidOperationException("Stored session source requires a message repository.");
        }

        var component = new StoredSessionSourceComponent(
            context.MessageRepository,
            sessionId.Value,
            preserveTiming: GetBoolOrDefault(context.Definition, "preserveTiming", false),
            speed: GetDoubleOrDefault(context.Definition, "speed", 1),
            boundedCapacity: GetBoundedCapacity(context.Definition));

        return MqttSourceNodeConfiguration.CreateSourceRuntimeNode(context.Address, component, component.Output);
    }

    private static RuntimeNode CreateEmptyMqttSource(NodeAddress address, NodeDefinition definition)
    {
        var component = new GeneratedSourceNode<MqttEnvelope>(
            MqttSourceNodeConfiguration.GetGeneratedSourceOptions(definition),
            []);

        return MqttSourceNodeConfiguration.CreateSourceRuntimeNode(address, component, component.Output);
    }
}

internal sealed class ReplaySourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.ReplaySource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
    {
        var sessionId = GetOptionalSessionId(context.Definition, "sessionId");
        if (sessionId is null)
        {
            return CreateEmptyMqttSource(context.Address, context.Definition);
        }

        if (context.MessageRepository is null)
        {
            throw new InvalidOperationException("Replay source requires a message repository.");
        }

        var component = new StoredSessionSourceComponent(
            context.MessageRepository,
            sessionId.Value,
            preserveTiming: true,
            speed: GetDoubleOrDefault(context.Definition, "speed", 1),
            boundedCapacity: GetBoundedCapacity(context.Definition));

        return MqttSourceNodeConfiguration.CreateSourceRuntimeNode(context.Address, component, component.Output);
    }

    private static RuntimeNode CreateEmptyMqttSource(NodeAddress address, NodeDefinition definition)
    {
        var component = new GeneratedSourceNode<MqttEnvelope>(
            MqttSourceNodeConfiguration.GetGeneratedSourceOptions(definition),
            []);

        return MqttSourceNodeConfiguration.CreateSourceRuntimeNode(address, component, component.Output);
    }
}

internal sealed class GeneratedMqttSourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.GeneratedSource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
    {
        var component = new GeneratedSourceNode<MqttEnvelope>(
            MqttSourceNodeConfiguration.GetGeneratedSourceOptions(context.Definition),
            GetGeneratedMessages(context.Definition));

        return MqttSourceNodeConfiguration.CreateSourceRuntimeNode(context.Address, component, component.Output);
    }

    private static IReadOnlyList<MqttEnvelope> GetGeneratedMessages(NodeDefinition definition)
    {
        if (!definition.Configuration.TryGetValue("messages", out var messagesElement))
        {
            return [];
        }

        if (messagesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Configuration value 'messages' must be an array.");
        }

        var messages = new List<MqttEnvelope>();
        foreach (var item in messagesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each generated message must be an object.");
            }

            messages.Add(new MqttEnvelope
            {
                Topic = MqttSourceNodeConfiguration.ReadTopic(ReadRequiredString(item, "topic")),
                Payload = ReadPayload(item),
                ReceivedAt = ReadDateTimeOffsetOrDefault(item, "receivedAt", DateTimeOffset.UtcNow),
                QualityOfService = MqttSourceNodeConfiguration.ParseQualityOfService(item),
                Retain = ReadBoolOrDefault(item, "retain", false)
            });
        }

        return messages;
    }

    private static byte[] ReadPayload(JsonElement item)
    {
        if (!item.TryGetProperty("payload", out var payload))
        {
            return [];
        }

        return payload.ValueKind switch
        {
            JsonValueKind.String => DecodePayload(payload.GetString() ?? string.Empty, ReadStringOrDefault(item, "payloadEncoding", "utf8")),
            JsonValueKind.Array => payload.EnumerateArray().Select(ReadByte).ToArray(),
            _ => throw new InvalidOperationException("Generated message payload must be a string or byte array.")
        };
    }
}

internal sealed class MetricSourceRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MetricSource;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
    {
        if (context.MetricStreamHost is null)
        {
            throw new InvalidOperationException("Metric source requires a metric stream host.");
        }

        var component = new MetricSourceComponent(
            context.MetricStreamHost,
            GetRequiredString(context.Definition, "metricId"),
            GetStringDictionary(context.Definition, "parameters"),
            emitLatestOnStart: GetBoolOrDefault(context.Definition, "emitLatestOnStart", true),
            boundedCapacity: GetBoundedCapacity(context.Definition));

        return RuntimeNode.Create(
            context.Address,
            component,
            outputs:
            [
                new OutputPort<FluxMetricReading<double>>(context.Address.Port(MqttSourceNodeConfiguration.OutputPort), component.Output),
                new OutputPort<FlowError>(context.Address.Port(MqttSourceNodeConfiguration.ErrorsPort), component.Errors)
            ]);
    }
}

internal static class MqttSourceNodeConfiguration
{
    public static readonly PortName OutputPort = new("Output");
    public static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNode CreateSourceRuntimeNode(
        NodeAddress address,
        IFlowNode component,
        ISourceBlock<MqttEnvelope> output)
        => RuntimeNode.Create(
            address,
            component,
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(OutputPort), output),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);

    public static GeneratedSourceOptions GetGeneratedSourceOptions(NodeDefinition definition)
    {
        var loop = GetBoolOrDefault(definition, "loop", false);
        var maxItems = GetOptionalInt(definition, "maxItems", minValue: 1);
        if (loop && !maxItems.HasValue)
        {
            throw new InvalidOperationException("generated.source option 'maxItems' is required when 'loop' is true.");
        }

        return new GeneratedSourceOptions
        {
            Name = GetNullableString(definition, "name") ?? "generated",
            OutputType = FlowContractTypeNames.MqttEnvelope,
            Loop = loop,
            MaxItems = maxItems,
            InitialDelayMilliseconds = GetIntOrDefault(definition, "initialDelayMilliseconds", 0, minValue: 0),
            IntervalMilliseconds = GetIntOrDefault(definition, "intervalMilliseconds", 0, minValue: 0),
            BoundedCapacity = GetBoundedCapacity(definition)
        };
    }

    public static MqttQualityOfServiceLevel ParseQualityOfService(JsonElement subscriptionElement)
    {
        if (!subscriptionElement.TryGetProperty("qos", out var qosElement))
        {
            return MqttQualityOfServiceLevel.AtMostOnce;
        }

        if (qosElement.ValueKind == JsonValueKind.Number && qosElement.TryGetInt32(out var qosValue))
        {
            return qosValue switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                1 => MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => throw new InvalidOperationException("Configuration value 'qos' must be 0, 1, or 2.")
            };
        }

        if (qosElement.ValueKind == JsonValueKind.String)
        {
            var value = qosElement.GetString();
            if (string.Equals(value, "AtMostOnce", StringComparison.OrdinalIgnoreCase))
            {
                return MqttQualityOfServiceLevel.AtMostOnce;
            }

            if (string.Equals(value, "AtLeastOnce", StringComparison.OrdinalIgnoreCase))
            {
                return MqttQualityOfServiceLevel.AtLeastOnce;
            }

            if (string.Equals(value, "ExactlyOnce", StringComparison.OrdinalIgnoreCase))
            {
                return MqttQualityOfServiceLevel.ExactlyOnce;
            }
        }

        throw new InvalidOperationException("Configuration value 'qos' must be 0, 1, 2, AtMostOnce, AtLeastOnce, or ExactlyOnce.");
    }

    public static string ReadTopic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Configuration value 'topic' must not be empty.");
        }

        return value;
    }
}
