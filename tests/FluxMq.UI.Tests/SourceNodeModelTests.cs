using FluxMq.UI.Components.Workspace.Nodes.Sources;
using FluxMq.UI.Components.Workspace.Nodes.MqttTrigger;
using FluxMq.UI.Components.Workspace.Nodes.MetricSource;
using FluxMq.UI.Components.Workspace.Nodes.SessionSource;
using FluxMq.UI.Services;
using Shouldly;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class SourceNodeModelTests
{
    [Theory]
    [InlineData("generated.source", typeof(GeneratedSourceNodeModel))]
    [InlineData("metric.source", typeof(MetricSourceNodeModel))]
    [InlineData("replay.source", typeof(ReplaySourceNodeModel))]
    [InlineData("session.source", typeof(SessionSourceNodeModel))]
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
    public void MetricSourceNodeModel_BuildsMetricSourceConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("metric.source").ShouldNotBeNull();
        var model = new MetricSourceNodeModel(
            "workflow1.metricSource",
            new DiagramPoint(10, 20),
            "metricSource",
            descriptor,
            isResource: false)
        {
            MetricId = " messageRate ",
            EmitLatestOnStart = false,
            BoundedCapacity = 500
        };
        model.SetParameters(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" topic "] = " factory/ ",
            ["empty"] = ""
        });

        var config = model.BuildConfiguration();
        var parameters = config["parameters"]!.AsObject();

        config["metricId"]!.GetValue<string>().ShouldBe("messageRate");
        config["emitLatestOnStart"]!.GetValue<bool>().ShouldBeFalse();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(500);
        parameters.Count.ShouldBe(1);
        parameters["topic"]!.GetValue<string>().ShouldBe("factory/");
    }

    [Fact]
    public void MetricSourceNodeModel_ReadsMetricSourceConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("metric.source").ShouldNotBeNull();
        var model = new MetricSourceNodeModel(
            "workflow1.metricSource",
            new DiagramPoint(10, 20),
            "metricSource",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["metricId"] = "messageRate",
            ["emitLatestOnStart"] = false,
            ["boundedCapacity"] = 250,
            ["parameters"] = new JsonObject
            {
                ["topic"] = "factory/",
                ["qos"] = 1
            }
        });

        model.MetricId.ShouldBe("messageRate");
        model.EmitLatestOnStart.ShouldBeFalse();
        model.BoundedCapacity.ShouldBe(250);
        model.Parameters["topic"].ShouldBe("factory/");
        model.Parameters["qos"].ShouldBe("1");
        model.ResolvePortValueType(model.PortDescriptors.Single(port => port.Name == "Output"))
            .ShouldBe("NumberMetricReading");
    }

    [Fact]
    public void MetricSourceNodeModel_NormalizesMetricSourceConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("metric.source").ShouldNotBeNull();
        var model = new MetricSourceNodeModel(
            "workflow1.metricSource",
            new DiagramPoint(10, 20),
            "metricSource",
            descriptor,
            isResource: false)
        {
            MetricId = " messageRate ",
            BoundedCapacity = 0
        };
        model.SetParameters(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" topic "] = " factory/# ",
            ["empty"] = " "
        });

        var config = model.BuildConfiguration();
        var parameters = config["parameters"]!.AsObject();

        config["metricId"]!.GetValue<string>().ShouldBe("messageRate");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(1);
        parameters.Count.ShouldBe(1);
        parameters["topic"]!.GetValue<string>().ShouldBe("factory/#");

        model.LoadConfiguration(new JsonObject
        {
            ["boundedCapacity"] = -10,
            ["parameters"] = new JsonObject
            {
                ["enabled"] = true,
                ["window"] = 10,
                ["empty"] = string.Empty
            }
        });

        model.BoundedCapacity.ShouldBe(1);
        model.Parameters["enabled"].ShouldBe("true");
        model.Parameters["window"].ShouldBe("10");
        model.Parameters.ContainsKey("empty").ShouldBeFalse();
    }

    [Fact]
    public void MqttTriggerNodeModel_BuildsSubscriptionsWithExplicitQualityOfService()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.trigger").ShouldNotBeNull();
        var model = new MqttTriggerNodeModel(
            "workflow1.trigger",
            new DiagramPoint(10, 20),
            "trigger",
            descriptor,
            isResource: false)
        {
            Connection = "broker",
            Subscriptions = [new("factory/#", 2, ReceiveRetainedMessages: false, RetainAsPublished: true)],
            BoundedCapacity = 500
        };

        var config = model.BuildConfiguration();
        var subscription = config["subscriptions"]!.AsArray().Single()!.AsObject();

        subscription["topicFilter"]!.GetValue<string>().ShouldBe("factory/#");
        subscription["qos"]!.GetValue<int>().ShouldBe(2);
        subscription["receiveRetained"]!.GetValue<bool>().ShouldBeFalse();
        subscription["retainAsPublished"]!.GetValue<bool>().ShouldBeTrue();
        config["connection"]!.GetValue<string>().ShouldBe("broker");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(500);
    }

    [Fact]
    public void MqttTriggerNodeModel_NormalizesBoundedCapacity()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.trigger").ShouldNotBeNull();
        var model = new MqttTriggerNodeModel(
            "workflow1.trigger",
            new DiagramPoint(10, 20),
            "trigger",
            descriptor,
            isResource: false)
        {
            BoundedCapacity = 0
        };

        model.BuildConfiguration()["boundedCapacity"]!.GetValue<int>()
            .ShouldBe(MqttTriggerNodeModel.DefaultBoundedCapacity);

        model.LoadConfiguration(new JsonObject
        {
            ["boundedCapacity"] = -10,
            ["subscriptions"] = new JsonArray("factory/#")
        });

        model.BoundedCapacity.ShouldBe(MqttTriggerNodeModel.DefaultBoundedCapacity);
        MqttTriggerNodeModel.NormalizeBoundedCapacity(42).ShouldBe(42);
    }

    [Fact]
    public void MqttTriggerNodeModel_ReadsShortSubscriptionsWithRouterFriendlyQualityOfService()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.trigger").ShouldNotBeNull();
        var model = new MqttTriggerNodeModel(
            "workflow1.trigger",
            new DiagramPoint(10, 20),
            "trigger",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["subscriptions"] = new JsonArray("factory/#")
        });

        var subscription = model.Subscriptions.ShouldHaveSingleItem();
        subscription.TopicFilter.ShouldBe("factory/#");
        subscription.QualityOfService.ShouldBe(1);
        subscription.ReceiveRetainedMessages.ShouldBeTrue();
        subscription.RetainAsPublished.ShouldBeTrue();
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
    public void GeneratedSourceNodeModel_NormalizesGeneratedMessagesAndBuffer()
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
            Messages = [],
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();
        var message = config["messages"]!.AsArray().ShouldHaveSingleItem()!.AsObject();

        message["topic"]!.GetValue<string>().ShouldBe("factory/sample");
        message["payload"]!.GetValue<string>().ShouldBe("""{"value":21.7,"unit":"c","status":"ok"}""");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);

        model.LoadConfiguration(new JsonObject
        {
            ["boundedCapacity"] = -10,
            ["messages"] = new JsonArray()
        });

        model.Messages.ShouldHaveSingleItem().Topic.ShouldBe("factory/sample");
        model.BoundedCapacity.ShouldBe(1000);
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

    [Fact]
    public void ReplaySourceNodeModel_NormalizesReplayConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("replay.source").ShouldNotBeNull();
        var model = new ReplaySourceNodeModel(
            "workflow1.replay",
            new DiagramPoint(10, 20),
            "replay",
            descriptor,
            isResource: false)
        {
            SessionId = " session-1 ",
            Speed = -2,
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "sessionId",
            "speed",
            "boundedCapacity"
        ], ignoreOrder: true);
        config["sessionId"]!.GetValue<string>().ShouldBe("session-1");
        config["speed"]!.GetValue<double>().ShouldBe(1);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);

        model.LoadConfiguration(new JsonObject
        {
            ["sessionId"] = " ",
            ["speed"] = 0,
            ["boundedCapacity"] = -1
        });

        var fallback = model.BuildConfiguration();

        fallback["sessionId"]!.GetValue<string>().ShouldBeEmpty();
        fallback["speed"]!.GetValue<double>().ShouldBe(1);
        fallback["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);
    }

    [Fact]
    public void SessionSourceNodeModel_NormalizesStoredSessionConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("session.source").ShouldNotBeNull();
        var model = new SessionSourceNodeModel(
            "workflow1.sessionSource",
            new DiagramPoint(10, 20),
            "sessionSource",
            descriptor,
            isResource: false)
        {
            SessionId = " session-1 ",
            PreserveTiming = true,
            Speed = -2,
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "sessionId",
            "preserveTiming",
            "speed",
            "boundedCapacity"
        ], ignoreOrder: true);
        config["sessionId"]!.GetValue<string>().ShouldBe("session-1");
        config["preserveTiming"]!.GetValue<bool>().ShouldBeTrue();
        config["speed"]!.GetValue<double>().ShouldBe(SessionSourceNodeModel.DefaultSpeed);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(SessionSourceNodeModel.DefaultBoundedCapacity);

        model.LoadConfiguration(new JsonObject
        {
            ["sessionId"] = " ",
            ["preserveTiming"] = true,
            ["speed"] = 0,
            ["boundedCapacity"] = -1
        });

        var fallback = model.BuildConfiguration();

        fallback["sessionId"]!.GetValue<string>().ShouldBeEmpty();
        fallback["preserveTiming"]!.GetValue<bool>().ShouldBeTrue();
        fallback["speed"]!.GetValue<double>().ShouldBe(SessionSourceNodeModel.DefaultSpeed);
        fallback["boundedCapacity"]!.GetValue<int>().ShouldBe(SessionSourceNodeModel.DefaultBoundedCapacity);
    }
}
