using Shouldly;
using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Components.Workspace.Nodes.StateReducer;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Blazor.Diagrams.Core.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class FlowDiagramNodeModelTests
{
    [Fact]
    public void SetActivity_StoresLatestActivityText()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.SetActivity("Connected | 3 messages");

        model.ActivityText.ShouldBe("Connected | 3 messages");
    }

    [Fact]
    public void SetDiagnostics_StoresNodeDiagnosticsAndPrimarySeverity()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var warning = new WorkspaceDiagnostic("Warning", "Definition", "Check", "Check this node.", "workflow1", "source");
        var error = new WorkspaceDiagnostic("Error", "RuntimeBuild", "FactoryFailed", "Factory failed.", "workflow1", "source");

        model.SetDiagnostics([warning, error]);

        model.Diagnostics.ShouldBe([warning, error]);
        model.PrimaryDiagnostic.ShouldBe(error);

        model.SetDiagnostics([]);
        model.Diagnostics.ShouldBeEmpty();
        model.PrimaryDiagnostic.ShouldBeNull();
    }

    [Fact]
    public void NewNode_DefaultsToCollapsed()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.IsCollapsed.ShouldBeTrue();
    }

    [Fact]
    public void Toggle_SwitchesBetweenCollapsedAndExpandedState()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        // Default: collapsed.
        model.Toggle();
        model.IsCollapsed.ShouldBeFalse();

        model.Toggle();
        model.IsCollapsed.ShouldBeTrue();
    }

    [Fact]
    public void SetCollapsed_AppliesRequestedState()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.SetCollapsed(true);
        model.IsCollapsed.ShouldBeTrue();

        model.SetCollapsed(false);
        model.IsCollapsed.ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_PreservesDescriptorDirectionIndependentlyOfSide()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var leftOutput = new FlowPortModel(model, PortAlignment.Left, "Output", representsInput: false);
        var rightInput = new FlowPortModel(model, PortAlignment.Right, "Input", representsInput: true);

        leftOutput.RepresentsInput.ShouldBeFalse();
        rightInput.RepresentsInput.ShouldBeTrue();
    }

    [Fact]
    public void FlowPortModel_RejectsSameDirectionLinks()
    {
        var first = new FlowDiagramNodeModel(
            "workflow1.first",
            new DiagramPoint(10, 20),
            "first",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);
        var second = new FlowDiagramNodeModel(
            "workflow1.second",
            new DiagramPoint(40, 20),
            "second",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var firstOutput = new FlowPortModel(first, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");
        var secondOutput = new FlowPortModel(second, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");

        firstOutput.CanAttachTo(secondOutput).ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_RejectsMismatchedValueTypes()
    {
        var source = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);
        var publisher = new FlowDiagramNodeModel(
            "workflow1.publisher",
            new DiagramPoint(40, 20),
            "publisher",
            "mqtt.publisher",
            descriptor: null,
            isResource: false);

        var output = new FlowPortModel(source, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");
        var input = new FlowPortModel(publisher, PortAlignment.Left, "Input", representsInput: true, valueType: "MqttPublishRequest");

        output.CanCarryValueTo(input).ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_FlagsErrorPortsForDistinctStyling()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var errors = new FlowPortModel(model, PortAlignment.Right, "Errors", representsInput: false, valueType: "FlowError");
        var output = new FlowPortModel(model, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");

        errors.IsErrorPort.ShouldBeTrue();
        output.IsErrorPort.ShouldBeFalse();
    }

    [Fact]
    public void DynamicMapperNodeModel_UsesConfiguredOutputTypeForPortType()
    {
        var descriptor = new ComponentPortDescriptor("Output", "Configured output type", IsInput: false);
        var model = new DynamicMapperNodeModel(
            "workflow1.mapper",
            new DiagramPoint(10, 20),
            "mapper",
            descriptor: null,
            isResource: false)
        {
            OutputContract = DynamicMapperNodeModel.OutputContractTyped,
            OutputType = "FileWriteRequest"
        };

        model.ResolvePortValueType(descriptor).ShouldBe("FileWriteRequest");
    }

    [Fact]
    public void FlowPortModel_AnchorsLinksToVisibleHandleCenter()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var port = new FlowPortModel(model, PortAlignment.Left, "Input")
        {
            Initialized = true,
            Position = new DiagramPoint(100, 200),
            Size = new Blazor.Diagrams.Core.Geometry.Size(12, 12)
        };

        port.GetShape().GetPointAtAngle(180).ShouldBe(new DiagramPoint(106, 206));
        port.GetShape().GetPointAtAngle(0).ShouldBe(new DiagramPoint(106, 206));
    }

    [Fact]
    public void FlowLinkVisuals_BuildsCompactConditionLabel()
    {
        var label = FlowLinkVisuals.ConditionLabel(
            " input.Topic.StartsWith(\"factory/\") \r\n && input.Payload.Length > 0 ");

        label.ShouldBe("when: input.Topic.StartsWith(\"factory/\") && input.Payload.Length > 0");
    }

    [Fact]
    public void FlowLinkVisuals_TruncatesLongConditionLabel()
    {
        var label = FlowLinkVisuals.ConditionLabel(new string('x', 100)).ShouldNotBeNull();

        label.ShouldStartWith("when: ");
        label.ShouldEndWith("...");
        label.Length.ShouldBe(78);
    }

    [Fact]
    public void FlowLinkVisuals_PrefersConditionStyleOverErrorStyle()
    {
        FlowLinkVisuals.ColorFor(hasCondition: true, isError: true)
            .ShouldBe(FlowLinkVisuals.ConditionalColor);
        FlowLinkVisuals.WidthFor(hasCondition: true)
            .ShouldBe(FlowLinkVisuals.ConditionalWidth);
    }

    [Fact]
    public void FlowDiagramLinkModel_TogglesSelectionStyle()
    {
        var source = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);
        var target = new FlowDiagramNodeModel(
            "workflow1.target",
            new DiagramPoint(220, 20),
            "target",
            "mqtt.publisher",
            descriptor: null,
            isResource: false);
        var sourcePort = new FlowPortModel(source, PortAlignment.Right, "Output", representsInput: false);
        var targetPort = new FlowPortModel(target, PortAlignment.Left, "Input", representsInput: true);
        var link = new FlowDiagramLinkModel(sourcePort, targetPort);

        link.SetNormalStyle(FlowLinkVisuals.ConditionalColor, FlowLinkVisuals.ConditionalWidth);
        link.ApplySelectionStyle(selected: true);

        link.Color.ShouldBe(FlowLinkVisuals.ConditionalColor);
        link.Width.ShouldBe(FlowLinkVisuals.SelectedWidth);

        link.ApplySelectionStyle(selected: false);

        link.Color.ShouldBe(FlowLinkVisuals.ConditionalColor);
        link.Width.ShouldBe(FlowLinkVisuals.ConditionalWidth);
    }

    [Fact]
    public void FlowNodeModelFactory_CreatesMetricsNodeModel()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.metrics").ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            "workflow1.metrics",
            new DiagramPoint(10, 20),
            "metrics",
            "mqtt.metrics",
            descriptor,
            isResource: false);

        model.NodeType.ShouldBe("mqtt.metrics");
        model.DisplayName.ShouldBe("MQTT Metrics");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.IsInput);
    }

    [Fact]
    public void FlowNodeModelFactory_CreatesStateReducerNodeModel()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("state.reducer").ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            "workflow1.stateReducer",
            new DiagramPoint(10, 20),
            "stateReducer",
            "state.reducer",
            descriptor,
            isResource: false);

        model.ShouldBeOfType<StateReducerNodeModel>();
        model.NodeType.ShouldBe("state.reducer");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.ValueType == "StateReducerInput");
    }

    [Theory]
    [InlineData("flow.switch", typeof(RoutingSwitchNodeModel))]
    [InlineData("flow.window", typeof(RoutingWindowNodeModel))]
    [InlineData("flow.fork", typeof(RoutingForkNodeModel))]
    [InlineData("flow.merge", typeof(RoutingMergeNodeModel))]
    public void FlowNodeModelFactory_CreatesRoutingNodeModels(string nodeType, Type expectedType)
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(nodeType).ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            $"workflow1.{nodeType}",
            new DiagramPoint(10, 20),
            "routing",
            nodeType,
            descriptor,
            isResource: false);

        model.ShouldBeOfType(expectedType);
    }

    [Fact]
    public void RoutingSwitchNodeModel_BuildsRoutesAndPorts()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(RoutingNodeTypes.Switch).ShouldNotBeNull();
        var model = new RoutingSwitchNodeModel(
            "workflow1.switch",
            new DiagramPoint(10, 20),
            "switch",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "MqttPublishRequest",
            ["expression"] = "retain == true",
            ["routes"] = new JsonArray("True", "False"),
            ["routeOutputs"] = new JsonObject
            {
                ["True"] = "Retained",
                ["False"] = "Live"
            },
            ["emitRouteEnvelope"] = true,
            ["boundedCapacity"] = 64
        });

        model.PortDescriptors.ShouldContain(port => port.Name == "Retained" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Live" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Routed" && !port.IsInput);
        model.ResolvePortValueType(new ComponentPortDescriptor("Retained", "Configured input type", IsInput: false))
            .ShouldBe("MqttPublishRequest");

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("MqttPublishRequest");
        config["expression"]!.GetValue<string>().ShouldBe("retain == true");
        config["emitRouteEnvelope"]!.GetValue<bool>().ShouldBeTrue();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
        config["routeOutputs"]!.AsObject()["True"]!.GetValue<string>().ShouldBe("Retained");
    }

    [Fact]
    public void RoutingFanNodeModels_BuildConfiguredPorts()
    {
        var catalog = new FlowComponentCatalog();
        var fork = new RoutingForkNodeModel(
            "workflow1.fork",
            new DiagramPoint(10, 20),
            "fork",
            catalog.Find(RoutingNodeTypes.Fork).ShouldNotBeNull(),
            isResource: false);
        var merge = new RoutingMergeNodeModel(
            "workflow1.merge",
            new DiagramPoint(10, 20),
            "merge",
            catalog.Find(RoutingNodeTypes.Merge).ShouldNotBeNull(),
            isResource: false);

        fork.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "TimerTick",
            ["outputs"] = new JsonArray("Audit", "Dashboard"),
            ["boundedCapacity"] = 32
        });
        merge.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "TimerTick",
            ["inputs"] = new JsonArray("Primary", "Replay"),
            ["boundedCapacity"] = 16
        });

        fork.PortDescriptors.ShouldContain(port => port.Name == "Audit" && !port.IsInput);
        fork.ResolvePortValueType(new ComponentPortDescriptor("Audit", "Configured input type", IsInput: false))
            .ShouldBe("TimerTick");
        fork.BuildConfiguration()["outputs"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe(["Audit", "Dashboard"]);

        merge.PortDescriptors.ShouldContain(port => port.Name == "Primary" && port.IsInput);
        merge.ResolvePortValueType(new ComponentPortDescriptor("Primary", "Configured input type", IsInput: true))
            .ShouldBe("TimerTick");
        merge.BuildConfiguration()["inputs"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe(["Primary", "Replay"]);
    }

    [Fact]
    public void RoutingWindowNodeModel_BuildsConfigurationAndPorts()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingWindowNodeModel(
            "workflow1.window",
            new DiagramPoint(10, 20),
            "window",
            catalog.Find(RoutingNodeTypes.Window).ShouldNotBeNull(),
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "TimerTick",
            ["maxItems"] = 25,
            ["timeMilliseconds"] = 0,
            ["emitPartialOnCompletion"] = false,
            ["boundedCapacity"] = 64
        });

        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.ValueType == "TimerTick" && port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowWindow" && !port.IsInput);
        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("TimerTick");
        model.ResolvePortValueType(new ComponentPortDescriptor("Output", "FlowWindow", IsInput: false))
            .ShouldBe("FlowWindow");

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("TimerTick");
        config["maxItems"]!.GetValue<int>().ShouldBe(25);
        config["timeMilliseconds"]!.GetValue<int>().ShouldBe(0);
        config["emitPartialOnCompletion"]!.GetValue<bool>().ShouldBeFalse();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
    }

    [Fact]
    public void FlowAssertionNodeModel_BuildsAssertionConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("flow.assert").ShouldNotBeNull();
        var model = new FlowAssertionNodeModel(
            "workflow1.assertion",
            new DiagramPoint(10, 20),
            "assertion",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["assertionName"] = "Topic starts with factory",
            ["inputType"] = "MqttPublishRequest",
            ["expression"] = "topic.StartsWith(\"factory/\")",
            ["failureMessage"] = "Topic did not match factory.",
            ["boundedCapacity"] = 250
        });

        var config = model.BuildConfiguration();

        config["assertionName"]!.GetValue<string>().ShouldBe("Topic starts with factory");
        config["inputType"]!.GetValue<string>().ShouldBe("MqttPublishRequest");
        config["expression"]!.GetValue<string>().ShouldBe("topic.StartsWith(\"factory/\")");
        config["failureMessage"]!.GetValue<string>().ShouldBe("Topic did not match factory.");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(250);
        model.ResolvePortValueType(new ComponentPortDescriptor("Passed", "Configured input type", IsInput: false))
            .ShouldBe("MqttPublishRequest");
    }

    [Fact]
    public void FlowAssertionNodeModel_UsesSharedAssertionInputTypes()
    {
        FlowAssertionNodeModel.InputTypes.ShouldBe(FlowContractTypeNames.AssertionInputTypes);
        FlowAssertionNodeModel.InputTypes.ShouldContain("StateReducerResult");

        FlowAssertionNodeModel.NormalizeInputType("FluxFlow.Components.State.Contracts.StateReducerResult")
            .ShouldBe("StateReducerResult");
    }

    [Fact]
    public void StateReducerNodeModel_BuildsReducerConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("state.reducer").ShouldNotBeNull();
        var model = new StateReducerNodeModel(
            "workflow1.stateReducer",
            new DiagramPoint(10, 20),
            "stateReducer",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["engine"] = "dynamic-expresso",
            ["keyExpression"] = "topic",
            ["reducer"] = "state == null ? input : state",
            ["expressionName"] = "latest",
            ["boundedCapacity"] = 64,
            ["maxKeys"] = 32
        });

        var config = model.BuildConfiguration();

        config["engine"]!.GetValue<string>().ShouldBe("dynamic-expresso");
        config["keyExpression"]!.GetValue<string>().ShouldBe("topic");
        config["reducer"]!.GetValue<string>().ShouldBe("state == null ? input : state");
        config["expressionName"]!.GetValue<string>().ShouldBe("latest");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
        config["maxKeys"]!.GetValue<int>().ShouldBe(32);
    }

    [Fact]
    public void MqttMetricsNodeModel_BuildsRateConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.metrics").ShouldNotBeNull();
        var model = new MqttMetricsNodeModel(
            "workflow1.metrics",
            new DiagramPoint(10, 20),
            "metrics",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["boundedCapacity"] = 250,
            ["rateWindowSeconds"] = 15,
            ["metricCardColumns"] = 3,
            ["displayMetrics"] = new JsonArray(
                MqttMetricsNodeModel.MetricMessages,
                MqttMetricsNodeModel.MetricPayloadBytes,
                MqttMetricsNodeModel.MetricTopics,
                MqttMetricsNodeModel.MetricCurrentRate,
                MqttMetricsNodeModel.MetricAverageRate,
                MqttMetricsNodeModel.MetricRetained,
                MqttMetricsNodeModel.MetricAveragePayload)
        });

        var config = model.BuildConfiguration();

        config["boundedCapacity"]!.GetValue<int>().ShouldBe(250);
        config["rateWindowSeconds"]!.GetValue<double>().ShouldBe(15);
        config["metricCardColumns"]!.GetValue<int>().ShouldBe(3);
        config.ContainsKey("metricCardRows").ShouldBeFalse();
        config["displayMetrics"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe([
                MqttMetricsNodeModel.MetricMessages,
                MqttMetricsNodeModel.MetricPayloadBytes,
                MqttMetricsNodeModel.MetricTopics,
                MqttMetricsNodeModel.MetricCurrentRate,
                MqttMetricsNodeModel.MetricAverageRate,
                MqttMetricsNodeModel.MetricRetained,
                MqttMetricsNodeModel.MetricAveragePayload
            ]);
    }
}
