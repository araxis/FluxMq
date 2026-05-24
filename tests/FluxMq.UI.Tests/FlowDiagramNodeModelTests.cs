using Shouldly;
using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
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
    public void FlowNodeModelFactory_NormalizesMetricsSinkAlias()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.metrics-sink").ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            "workflow1.metrics",
            new DiagramPoint(10, 20),
            "metrics",
            "mqtt.metrics-sink",
            descriptor,
            isResource: false);

        model.NodeType.ShouldBe("mqtt.metrics");
        model.DisplayName.ShouldBe("MQTT Metrics");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.IsInput);
    }

    [Fact]
    public void FlowAssertionNodeModel_BuildsAssertionConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("flow.assertion").ShouldNotBeNull();
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
