namespace FluxMq.UI.Services;

public sealed class DragStateService
{
    public ActiveComponentDrag? ActiveComponentDrag { get; private set; }

    public event EventHandler? Changed;
    public event EventHandler<ComponentDropRequestedEventArgs>? ComponentDropRequested;

    public void UpdateComponentDrag(string componentType, string displayName, double clientX, double clientY, bool isOverDesigner)
    {
        ActiveComponentDrag = new ActiveComponentDrag(componentType, displayName, clientX, clientY, isOverDesigner);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void FinishComponentDrag(string componentType, string displayName, double clientX, double clientY, bool isOverDesigner)
    {
        ActiveComponentDrag = null;
        Changed?.Invoke(this, EventArgs.Empty);

        if (isOverDesigner)
        {
            ComponentDropRequested?.Invoke(
                this,
                new ComponentDropRequestedEventArgs(componentType, displayName, clientX, clientY));
        }
    }

    public void CancelComponentDrag()
    {
        ActiveComponentDrag = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record ActiveComponentDrag(
    string ComponentType,
    string DisplayName,
    double ClientX,
    double ClientY,
    bool IsOverDesigner);

public sealed record ComponentDropRequestedEventArgs(
    string ComponentType,
    string DisplayName,
    double ClientX,
    double ClientY);
