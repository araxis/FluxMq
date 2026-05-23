using Shouldly;
using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Blazor.Diagrams.Core.Models;
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
}
