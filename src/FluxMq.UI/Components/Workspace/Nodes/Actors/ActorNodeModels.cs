using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Components.Workspace.Nodes.Actors;

public sealed class MqttPublisherNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "mqtt.publisher", descriptor, isResource)
{
    public string Connection { get; set; } = FlowDefinitionComposer.BrokerResourceName;
    public int BoundedCapacity { get; set; } = ActorNodeConfiguration.DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
    {
        Connection = ActorNodeConfiguration.ReadString(config, "connection", FlowDefinitionComposer.BrokerResourceName);
        BoundedCapacity = ActorNodeConfiguration.ReadBoundedCapacity(config);
    }

    public override JsonObject BuildConfiguration() => new()
    {
        ["connection"] = string.IsNullOrWhiteSpace(Connection) ? FlowDefinitionComposer.BrokerResourceName : Connection.Trim(),
        ["boundedCapacity"] = ActorNodeConfiguration.NormalizeBoundedCapacity(BoundedCapacity)
    };
}

public sealed class MqttRecorderNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "mqtt.recorder", descriptor, isResource)
{
    public int BoundedCapacity { get; set; } = ActorNodeConfiguration.DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
        => BoundedCapacity = ActorNodeConfiguration.ReadBoundedCapacity(config);

    public override JsonObject BuildConfiguration() => ActorNodeConfiguration.BuildCapacityConfiguration(BoundedCapacity);
}

public sealed class FileWriterNodeModel(
    string id,
    DiagramPoint position,
    string nodeName,
    FlowComponentDescriptor? descriptor,
    bool isResource)
    : FlowDiagramNodeModel(id, position, nodeName, "file.writer", descriptor, isResource)
{
    public int BoundedCapacity { get; set; } = ActorNodeConfiguration.DefaultBoundedCapacity;

    protected override void OnConfigurationLoaded(JsonObject? config)
        => BoundedCapacity = ActorNodeConfiguration.ReadBoundedCapacity(config);

    public override JsonObject BuildConfiguration() => ActorNodeConfiguration.BuildCapacityConfiguration(BoundedCapacity);
}

internal static class ActorNodeConfiguration
{
    public const int DefaultBoundedCapacity = 1000;

    public static JsonObject BuildCapacityConfiguration(int boundedCapacity) => new()
    {
        ["boundedCapacity"] = NormalizeBoundedCapacity(boundedCapacity)
    };

    public static int ReadBoundedCapacity(JsonObject? config)
        => ReadInt(config, "boundedCapacity", DefaultBoundedCapacity);

    public static int NormalizeBoundedCapacity(int boundedCapacity)
        => boundedCapacity > 0 ? boundedCapacity : DefaultBoundedCapacity;

    public static string ReadString(JsonObject? config, string key, string fallback)
        => config?[key]?.GetValue<string?>() is { Length: > 0 } value ? value : fallback;

    private static int ReadInt(JsonObject? config, string key, int fallback)
    {
        if (config?[key] is JsonValue value && value.TryGetValue<int>(out var result) && result > 0)
        {
            return result;
        }

        return fallback;
    }
}
