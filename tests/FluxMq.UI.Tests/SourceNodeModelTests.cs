using FluxMq.UI.Components.Workspace.Nodes.Sources;
using FluxMq.UI.Services;
using Shouldly;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class SourceNodeModelTests
{
    [Theory]
    [InlineData("generated.source", typeof(GeneratedSourceNodeModel))]
    [InlineData("replay.source", typeof(ReplaySourceNodeModel))]
    public void FlowNodeModelFactory_CreatesTypedSourceModels(string nodeType, Type expectedType)
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(nodeType).ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            $"workflow1.{nodeType}",
            new DiagramPoint(10, 20),
            "source",
            nodeType,
            descriptor,
            isResource: false);

        model.GetType().ShouldBe(expectedType);
        model.Category.ShouldBe("Source");
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Errors" && !port.IsInput);
    }

    [Fact]
    public void GeneratedSourceNodeModel_BuildsConfiguredMessages()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("generated.source").ShouldNotBeNull();
        var model = new GeneratedSourceNodeModel(
            "workflow1.generated",
            new DiagramPoint(10, 20),
            "generated",
            descriptor,
            isResource: false)
        {
            Messages =
            [
                new GeneratedMessageDraft
                {
                    Topic = "factory/one",
                    Payload = "hello",
                    QualityOfService = 2,
                    Retain = true,
                    ReceivedAt = "2026-05-23T10:00:00Z"
                }
            ],
            BoundedCapacity = 500
        };

        var config = model.BuildConfiguration();
        var message = config["messages"]!.AsArray().Single()!.AsObject();

        message["topic"]!.GetValue<string>().ShouldBe("factory/one");
        message["payload"]!.GetValue<string>().ShouldBe("hello");
        message["payloadEncoding"]!.GetValue<string>().ShouldBe("utf8");
        message["qos"]!.GetValue<int>().ShouldBe(2);
        message["retain"]!.GetValue<bool>().ShouldBeTrue();
        message["receivedAt"]!.GetValue<string>().ShouldBe("2026-05-23T10:00:00Z");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(500);
    }

    [Fact]
    public void ReplaySourceNodeModel_BuildsReplayConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("replay.source").ShouldNotBeNull();
        var sessionId = Guid.NewGuid().ToString();
        var model = new ReplaySourceNodeModel(
            "workflow1.replay",
            new DiagramPoint(10, 20),
            "replay",
            descriptor,
            isResource: false)
        {
            SessionId = sessionId,
            Speed = 4,
            BoundedCapacity = 300
        };

        var config = model.BuildConfiguration();

        config["sessionId"]!.GetValue<string>().ShouldBe(sessionId);
        config["speed"]!.GetValue<double>().ShouldBe(4);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(300);
    }
}
