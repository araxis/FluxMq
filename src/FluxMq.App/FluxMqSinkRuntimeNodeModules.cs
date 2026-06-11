using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Runtime;
using FluxMq.Components.FileWriter;
using FluxMq.Components.Logging;
using FluxMq.Components.MessageSource;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Replay;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Core.Ids;
using static FluxMq.App.FluxMqRuntimeNodeConfigurationReader;

namespace FluxMq.App;

internal sealed class MqttPublisherRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MqttPublisher;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => MqttSinkNodeConfiguration.CreatePublisher(context.Address, context.Definition, context.EngineContext);
}

internal sealed class MqttRecorderRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.MqttRecorder;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => MqttSinkNodeConfiguration.CreateRecorder(context.Address, context.Definition, context.MessageRepository);
}

internal sealed class FileWriterRuntimeNodeModule : IFluxMqRuntimeNodeModule
{
    public NodeType Type => FluxMqNodeTypes.FileWriter;

    public RuntimeNode Build(FluxMqRuntimeNodeBuildContext context)
        => MqttSinkNodeConfiguration.CreateFileWriter(context.Address, context.Definition);
}

internal static class MqttSinkNodeConfiguration
{
    private static readonly PortName InputPort = new("Input");
    private static readonly PortName EntriesPort = new("Entries");
    private static readonly PortName ErrorsPort = new("Errors");

    public static RuntimeNode CreatePublisher(
        NodeAddress address,
        NodeDefinition definition,
        RuntimeNodeFactoryContext context)
    {
        var connectionRef = GetRequiredString(definition, "connection");
        var resource = context.GetResource(new NodeName(connectionRef));
        if (resource.Node is not MqttConnectionComponent connection)
        {
            throw new InvalidOperationException(
                $"Resource '{connectionRef}' must be of type '{FluxMqNodeTypes.Connection.Value}' to be used by an mqtt.publisher.");
        }

        var component = new MqttPublisherComponent(connection.Client, boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttPublishRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowLogEntry>(address.Port(EntriesPort), component.Entries),
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    public static RuntimeNode CreateRecorder(
        NodeAddress address,
        NodeDefinition definition,
        IMessageRepository? messageRepository)
    {
        if (messageRepository is null)
        {
            throw new InvalidOperationException("MQTT recorder requires a message repository.");
        }

        var component = new MqttRecorderComponent(messageRepository, boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<MqttRecordingRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }

    public static RuntimeNode CreateFileWriter(NodeAddress address, NodeDefinition definition)
    {
        var component = new FileWriterComponent(boundedCapacity: GetBoundedCapacity(definition));

        return RuntimeNode.Create(
            address,
            component,
            inputs:
            [
                new InputPort<FileWriteRequest>(address.Port(InputPort), component.Input)
            ],
            outputs:
            [
                new OutputPort<FlowError>(address.Port(ErrorsPort), component.Errors)
            ]);
    }
}
