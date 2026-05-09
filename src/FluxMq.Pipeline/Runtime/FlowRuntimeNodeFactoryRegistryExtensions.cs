using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using System.Text.Json;

namespace FluxMq.Pipeline.Runtime;

public static class FlowRuntimeNodeFactoryRegistryExtensions
{
    private static readonly FlowPortName InputPort = new("Input");
    private static readonly FlowPortName OutputPort = new("Output");
    private static readonly FlowPortName SnapshotsPort = new("Snapshots");

    public static FlowRuntimeNodeFactoryRegistry RegisterPipelineComponentFactories(this FlowRuntimeNodeFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        return registry
            .Register(PipelineFlowNodeTypes.PayloadInspector, CreatePayloadInspector)
            .Register(PipelineFlowNodeTypes.MetricsSink, CreateMetricsSink);
    }

    private static FlowRuntimeNode CreatePayloadInspector(FlowNodeName name, FlowNodeDefinition definition)
    {
        var component = new PayloadInspectorMapperComponent(boundedCapacity: GetBoundedCapacity(definition));

        return FlowRuntimeNode.Create(
            name,
            component,
            inputs:
            [
                new FlowInputPort<MqttEnvelope>(InputPort, component.Input)
            ],
            outputs:
            [
                new FlowOutputPort<InspectedMqttMessage>(OutputPort, component.Output),
                new FlowOutputPort<FlowError>(new FlowPortName("Errors"), component.Errors)
            ]);
    }

    private static FlowRuntimeNode CreateMetricsSink(FlowNodeName name, FlowNodeDefinition definition)
    {
        var component = new MqttMetricsSinkComponent(boundedCapacity: GetBoundedCapacity(definition));

        return FlowRuntimeNode.Create(
            name,
            component,
            inputs:
            [
                new FlowInputPort<MqttEnvelope>(InputPort, component.Input)
            ],
            outputs:
            [
                new FlowOutputPort<MqttMetricsSnapshot>(SnapshotsPort, component.Snapshots),
                new FlowOutputPort<FlowError>(new FlowPortName("Errors"), component.Errors)
            ]);
    }

    private static int GetBoundedCapacity(FlowNodeDefinition definition)
    {
        const int defaultBoundedCapacity = 1000;

        if (!definition.Configuration.TryGetValue("boundedCapacity", out var value))
        {
            return defaultBoundedCapacity;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var boundedCapacity) || boundedCapacity <= 0)
        {
            throw new InvalidOperationException("Configuration value 'boundedCapacity' must be a positive integer.");
        }

        return boundedCapacity;
    }
}
