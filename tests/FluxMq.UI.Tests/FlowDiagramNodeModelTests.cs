using FluxMq.UI.Models;
using FluentAssertions;
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
            "mqtt.message-source",
            descriptor: null,
            isResource: false);

        model.SetActivity("Connected | 3 messages");

        model.ActivityText.Should().Be("Connected | 3 messages");
    }

    [Fact]
    public void Toggle_SwitchesBetweenExpandedAndCollapsedState()
    {
        var model = new FlowDiagramNodeModel(
            new DiagramPoint(10, 20),
            "source",
            "mqtt.message-source",
            descriptor: null,
            isResource: false);

        model.Toggle();

        model.IsCollapsed.Should().BeTrue();

        model.Toggle();

        model.IsCollapsed.Should().BeFalse();
    }

    [Fact]
    public void SetCollapsed_AppliesRequestedState()
    {
        var model = new FlowDiagramNodeModel(
            new DiagramPoint(10, 20),
            "source",
            "mqtt.message-source",
            descriptor: null,
            isResource: false);

        model.SetCollapsed(true);
        model.IsCollapsed.Should().BeTrue();

        model.SetCollapsed(false);
        model.IsCollapsed.Should().BeFalse();
    }
}
