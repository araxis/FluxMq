using FluxMq.UI.Components.Workspace.Nodes.Actors;
using FluxMq.UI.Services;
using Shouldly;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class ActorNodeModelTests
{
    [Theory]
    [InlineData("mqtt.publisher", typeof(MqttPublisherNodeModel))]
    [InlineData("mqtt.recorder", typeof(MqttRecorderNodeModel))]
    [InlineData("file.writer", typeof(FileWriterNodeModel))]
    public void FlowNodeModelFactory_CreatesTypedActorModels(string nodeType, Type expectedType)
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(nodeType).ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            $"workflow1.{nodeType}",
            new DiagramPoint(10, 20),
            "actor",
            nodeType,
            descriptor,
            isResource: false);

        model.GetType().ShouldBe(expectedType);
        model.Category.ShouldBe("Actor");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Errors" && !port.IsInput);
    }

    [Fact]
    public void MqttPublisherNodeModel_BuildsBrokerConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.publisher").ShouldNotBeNull();
        var model = new MqttPublisherNodeModel(
            "workflow1.publisher",
            new DiagramPoint(10, 20),
            "publisher",
            descriptor,
            isResource: false)
        {
            Connection = " broker2 ",
            BoundedCapacity = 250
        };

        descriptor.Ports.ShouldNotContain(port => port.Name == "Connection");
        model.PortDescriptors.ShouldNotContain(port => port.Name == "Connection");

        var config = model.BuildConfiguration();

        config.Count.ShouldBe(2);
        config["connection"]!.GetValue<string>().ShouldBe("broker2");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(250);
    }

    [Fact]
    public void ActorNodeModels_NormalizeInvalidBufferSize()
    {
        var catalog = new FlowComponentCatalog();
        var publisher = new MqttPublisherNodeModel(
            "workflow1.publisher",
            new DiagramPoint(10, 20),
            "publisher",
            catalog.Find("mqtt.publisher"),
            isResource: false)
        {
            BoundedCapacity = 0
        };

        var recorder = new MqttRecorderNodeModel(
            "workflow1.recorder",
            new DiagramPoint(10, 20),
            "recorder",
            catalog.Find("mqtt.recorder"),
            isResource: false)
        {
            BoundedCapacity = 0
        };

        var writer = new FileWriterNodeModel(
            "workflow1.fileWriter",
            new DiagramPoint(10, 20),
            "fileWriter",
            catalog.Find("file.writer"),
            isResource: false)
        {
            BoundedCapacity = -1
        };

        publisher.BuildConfiguration()["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);
        recorder.BuildConfiguration()["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);
        writer.BuildConfiguration()["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);
    }
}
