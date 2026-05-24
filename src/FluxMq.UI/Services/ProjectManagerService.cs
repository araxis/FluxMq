using FluxMq.Core.Models;
using FluxMq.Components.Storage.Repositories;

namespace FluxMq.UI.Services;

public sealed class ProjectManagerService : IAsyncDisposable
{
    private readonly FlowDefinitionComposer _composer;
    private readonly IMessageRepository? _messageRepository;
    private readonly LiveMqttWorkspaceService? _live;
    private readonly List<FlowWorkspaceService> _projects = [];

    public ProjectManagerService(
        FlowDefinitionComposer composer,
        IMessageRepository? messageRepository = null,
        LiveMqttWorkspaceService? live = null)
    {
        _composer = composer;
        _messageRepository = messageRepository;
        _live = live;
    }

    public IReadOnlyList<FlowWorkspaceService> Projects => _projects;
    public int ActiveIndex { get; private set; } = -1;
    public FlowWorkspaceService? ActiveProject => ActiveIndex >= 0 && ActiveIndex < _projects.Count
        ? _projects[ActiveIndex] : null;

    public event EventHandler? Changed;

    public FlowWorkspaceService NewProject()
    {
        var project = CreateProject();
        _projects.Add(project);
        ActiveIndex = _projects.Count - 1;
        NotifyChanged();
        return project;
    }

    public async Task<FlowWorkspaceService> OpenAsync(string path, CancellationToken ct = default)
    {
        var existing = _projects.FirstOrDefault(p =>
            string.Equals(p.CurrentFilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SetActive(existing);
            return existing;
        }

        var project = CreateProject();
        project.SetFilePath(path);
        await project.LoadFromFileAsync(ct);
        _projects.Add(project);
        ActiveIndex = _projects.Count - 1;
        NotifyChanged();
        return project;
    }

    public void SetActive(FlowWorkspaceService project)
    {
        var idx = _projects.IndexOf(project);
        if (idx >= 0 && idx != ActiveIndex)
        {
            ActiveIndex = idx;
            NotifyChanged();
        }
    }

    public void SetActiveByIndex(int index)
    {
        if (index >= 0 && index < _projects.Count && index != ActiveIndex)
        {
            ActiveIndex = index;
            NotifyChanged();
        }
    }

    public async ValueTask CloseProjectAsync(FlowWorkspaceService project)
    {
        var idx = _projects.IndexOf(project);
        if (idx < 0) return;

        var closedConnections = project.GetConnectionResources()
            .Select(static connection => (connection.Name, connection.Profile))
            .ToArray();

        _projects.RemoveAt(idx);
        await project.DisposeAsync();

        if (_projects.Count == 0)
            ActiveIndex = -1;
        else if (ActiveIndex >= _projects.Count)
            ActiveIndex = _projects.Count - 1;

        await RemoveClosedProjectConnectionsAsync(closedConnections).ConfigureAwait(false);
        NotifyChanged();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var project in _projects)
            await project.DisposeAsync();
        _projects.Clear();
    }

    private FlowWorkspaceService CreateProject() => new(_composer, _messageRepository);

    private async Task RemoveClosedProjectConnectionsAsync(
        IReadOnlyList<(string ResourceName, MqttConnectionProfile Profile)> closedConnections)
    {
        if (_live is null || closedConnections.Count == 0)
        {
            return;
        }

        var remainingConnections = _projects
            .SelectMany(static project => project.GetConnectionResources())
            .Select(static connection => (connection.Name, connection.Profile))
            .ToArray();

        var unreferencedConnections = closedConnections
            .Where(closed => !remainingConnections.Any(remaining => SameConnectionReference(closed, remaining)))
            .ToArray();

        if (unreferencedConnections.Length > 0)
        {
            await _live.RemoveConnectionsAsync(unreferencedConnections).ConfigureAwait(false);
        }
    }

    private static bool SameConnectionReference(
        (string ResourceName, MqttConnectionProfile Profile) left,
        (string ResourceName, MqttConnectionProfile Profile) right)
        => string.Equals(left.ResourceName, right.ResourceName, StringComparison.Ordinal) &&
           string.Equals(left.Profile.Host, right.Profile.Host, StringComparison.OrdinalIgnoreCase) &&
           left.Profile.Port == right.Profile.Port &&
           left.Profile.UseTls == right.Profile.UseTls;

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
