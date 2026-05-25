using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class DragStateServiceTests
{
    [Fact]
    public void FinishComponentDrag_EmitsDashboardTargetKindForDashboardDrop()
    {
        var service = new DragStateService();
        ComponentDropRequestedEventArgs? captured = null;
        service.ComponentDropRequested += (_, args) => captured = args;

        service.BeginComponentDrag(
            "event.counter",
            "Event counter",
            pointerId: 5,
            clientX: 10,
            clientY: 10,
            WorkspaceArtifactKind.Dashboard);
        service.MoveComponentDrag(pointerId: 5, clientX: 40, clientY: 40);
        service.SetComponentDragOverDesigner(pointerId: 5, isOverDesigner: true);

        var completed = service.FinishComponentDrag(pointerId: 5, clientX: 40, clientY: 40);

        completed.ShouldNotBeNull();
        completed.TargetKind.ShouldBe(WorkspaceArtifactKind.Dashboard);
        captured.ShouldNotBeNull();
        captured.TargetKind.ShouldBe(WorkspaceArtifactKind.Dashboard);
        captured.ComponentType.ShouldBe("event.counter");
    }

    [Fact]
    public void FinishComponentDrag_DefaultsToPipelineTargetKind()
    {
        var service = new DragStateService();

        service.BeginComponentDrag("mqtt.trigger", "Live MQTT Trigger", pointerId: 7, clientX: 10, clientY: 10);
        service.MoveComponentDrag(pointerId: 7, clientX: 30, clientY: 30);
        service.SetComponentDragOverDesigner(pointerId: 7, isOverDesigner: true);

        var completed = service.FinishComponentDrag(pointerId: 7, clientX: 30, clientY: 30);

        completed.ShouldNotBeNull();
        completed.TargetKind.ShouldBe(WorkspaceArtifactKind.Pipeline);
    }

    [Fact]
    public void FinishComponentDrag_EmitsTestTargetKindForScenarioStepDrop()
    {
        var service = new DragStateService();
        ComponentDropRequestedEventArgs? captured = null;
        service.ComponentDropRequested += (_, args) => captured = args;

        service.BeginComponentDrag(
            "expect.event",
            "Expect event",
            pointerId: 9,
            clientX: 10,
            clientY: 10,
            WorkspaceArtifactKind.Test);
        service.MoveComponentDrag(pointerId: 9, clientX: 25, clientY: 25);
        service.SetComponentDragOverDesigner(pointerId: 9, isOverDesigner: true);

        var completed = service.FinishComponentDrag(pointerId: 9, clientX: 25, clientY: 25);

        completed.ShouldNotBeNull();
        completed.TargetKind.ShouldBe(WorkspaceArtifactKind.Test);
        captured.ShouldNotBeNull();
        captured.TargetKind.ShouldBe(WorkspaceArtifactKind.Test);
        captured.ComponentType.ShouldBe("expect.event");
    }
}
