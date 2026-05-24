namespace FluxMq.UI.Services;

public sealed class DragStateService
{
    private const double DragThreshold = 5d;

    public ActiveComponentDrag? ActiveComponentDrag { get; private set; }

    public event EventHandler? Changed;
    public event EventHandler<ComponentDropRequestedEventArgs>? ComponentDropRequested;

    public void BeginComponentDrag(string componentType, string displayName, long pointerId, double clientX, double clientY)
    {
        ActiveComponentDrag = new ActiveComponentDrag(
            componentType,
            displayName,
            pointerId,
            clientX,
            clientY,
            clientX,
            clientY,
            IsOverDesigner: false,
            DidDrag: false);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MoveComponentDrag(long pointerId, double clientX, double clientY)
    {
        if (ActiveComponentDrag is not { } active || active.PointerId != pointerId)
        {
            return;
        }

        var didDrag = active.DidDrag ||
                      Distance(clientX - active.StartX, clientY - active.StartY) >= DragThreshold;

        ActiveComponentDrag = active with
        {
            ClientX = clientX,
            ClientY = clientY,
            DidDrag = didDrag
        };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetComponentDragOverDesigner(long pointerId, bool isOverDesigner)
    {
        if (ActiveComponentDrag is not { } active || active.PointerId != pointerId)
        {
            return;
        }

        if (active.IsOverDesigner == isOverDesigner)
        {
            return;
        }

        ActiveComponentDrag = active with { IsOverDesigner = isOverDesigner };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public CompletedComponentDrag? FinishComponentDrag(long pointerId, double clientX, double clientY)
    {
        if (ActiveComponentDrag is not { } active || active.PointerId != pointerId)
        {
            return null;
        }

        var didDrag = active.DidDrag ||
                      Distance(clientX - active.StartX, clientY - active.StartY) >= DragThreshold;
        var completed = new CompletedComponentDrag(
            active.ComponentType,
            active.DisplayName,
            clientX,
            clientY,
            active.IsOverDesigner,
            didDrag);

        ActiveComponentDrag = null;
        Changed?.Invoke(this, EventArgs.Empty);

        if (completed.DidDrag && completed.IsOverDesigner)
        {
            ComponentDropRequested?.Invoke(
                this,
                new ComponentDropRequestedEventArgs(
                    completed.ComponentType,
                    completed.DisplayName,
                    completed.ClientX,
                    completed.ClientY));
        }

        return completed;
    }

    public void CancelComponentDrag(long? pointerId = null)
    {
        if (pointerId is not null &&
            ActiveComponentDrag is { } active &&
            active.PointerId != pointerId)
        {
            return;
        }

        ActiveComponentDrag = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static double Distance(double x, double y)
    {
        return Math.Sqrt((x * x) + (y * y));
    }
}

public sealed record ActiveComponentDrag(
    string ComponentType,
    string DisplayName,
    long PointerId,
    double StartX,
    double StartY,
    double ClientX,
    double ClientY,
    bool IsOverDesigner,
    bool DidDrag);

public sealed record CompletedComponentDrag(
    string ComponentType,
    string DisplayName,
    double ClientX,
    double ClientY,
    bool IsOverDesigner,
    bool DidDrag);

public sealed record ComponentDropRequestedEventArgs(
    string ComponentType,
    string DisplayName,
    double ClientX,
    double ClientY);
