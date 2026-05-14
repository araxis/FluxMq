using FluxMq.UI.Models;
using Shouldly;
using FluxMq.UI.Components.Diagram;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class FlowDiagramNodeModelTests
{
    [Fact]
    public void SetActivity_StoresLatestActivityText()
    {
        var model = new FlowDiagramNodeModel(
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.SetActivity("Connected | 3 messages");

        model.ActivityText.ShouldBe("Connected | 3 messages");
    }

    [Fact]
    public void NewNode_DefaultsToCollapsed()
    {
        var model = new FlowDiagramNodeModel(
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
}
