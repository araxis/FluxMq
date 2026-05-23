namespace FluxMq.UI.Services;

public sealed class NodeEditDialogRefreshService
{
    private readonly Dictionary<string, Func<Task>> _refreshers = new(StringComparer.Ordinal);

    public void Register(string nodeId, Func<Task> refresh)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        _refreshers[nodeId] = refresh;
    }

    public void Unregister(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        _refreshers.Remove(nodeId);
    }

    public Task RefreshAsync(string nodeId)
        => !string.IsNullOrWhiteSpace(nodeId) && _refreshers.TryGetValue(nodeId, out var refresh)
            ? refresh()
            : Task.CompletedTask;
}
