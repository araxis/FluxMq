using FluxMq.App;
using FluxMq.Core.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.UI.Models;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace FluxMq.UI.Services;

public sealed class FlowWorkspaceService : IAsyncDisposable
{
    private readonly FlowDefinitionComposer _definitionComposer;
    private readonly IMessageRepository? _messageRepository;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FlowApplicationHost? _host;

    public FlowWorkspaceService(
        FlowDefinitionComposer definitionComposer,
        IMessageRepository? messageRepository = null)
    {
        _definitionComposer = definitionComposer;
        _messageRepository = messageRepository;
        DefinitionJson = _definitionComposer.CreateEmptyDefinition();
    }

    private string? _activeWorkflowName;
    private string? _displayName;

    public FlowApplicationHost? Host => _host;
    public string DefinitionJson { get; private set; }
    public long DefinitionRevision { get; private set; }
    public string CurrentFilePath { get; private set; } = string.Empty;
    public string Name => !string.IsNullOrEmpty(CurrentFilePath)
        ? Path.GetFileNameWithoutExtension(CurrentFilePath)
        : _displayName ?? "Untitled";
    public bool HasUnsavedChanges { get; private set; }
    public RuntimeWorkspaceState State { get; private set; } = RuntimeWorkspaceState.Idle;
    public IReadOnlyList<WorkspaceDiagnostic> Diagnostics { get; private set; } = [];

    public IReadOnlyList<string> WorkflowNames => _definitionComposer.GetWorkflowNames(DefinitionJson);
    public string? ActiveWorkflowName => _activeWorkflowName;

    public IReadOnlyList<(string Name, string Type)> GetWorkflowNodes(string workflowName)
        => _definitionComposer.GetWorkflowNodes(DefinitionJson, workflowName);

    public Func<IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)>>? GetDiagramState { get; set; }
    public IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)>? StagedNodePositions { get; private set; }
    public void ConsumeStagedPositions() => StagedNodePositions = null;
    public Dictionary<string, (double X, double Y, bool Collapsed)> LastNodePositions { get; } = new(StringComparer.Ordinal);

    public event EventHandler? Changed;

    public void SetDisplayName(string name)
    {
        _displayName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        NotifyChanged();
    }

    public void SetActiveWorkflow(string name)
    {
        if (string.Equals(_activeWorkflowName, name, StringComparison.Ordinal)) return;
        _activeWorkflowName = name;
        NotifyChanged();
    }

    public void AddWorkflow(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            ReplaceDefinition(_definitionComposer.AddWorkflow(DefinitionJson, name));
            _activeWorkflowName ??= name;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "WorkflowAddFailed", exception.Message)];
        }
        NotifyChanged();
    }

    public void RemoveWorkflow(string name)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.RemoveWorkflow(DefinitionJson, name));
            if (string.Equals(_activeWorkflowName, name, StringComparison.Ordinal))
                _activeWorkflowName = WorkflowNames.FirstOrDefault();
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "WorkflowRemoveFailed", exception.Message)];
        }
        NotifyChanged();
    }

    public void SetFilePath(string path)
    {
        CurrentFilePath = path;
        NotifyChanged();
    }

    public string GetFullDefinitionJson()
    {
        IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)>? positions =
            GetDiagramState?.Invoke() ??
            (LastNodePositions.Count > 0 ? LastNodePositions : null);
        return positions is not null
            ? _definitionComposer.WriteNodePositions(DefinitionJson, positions)
            : DefinitionJson;
    }

    public void SetDefinitionJson(string json)
    {
        ReplaceDefinition(json);
        State = RuntimeWorkspaceState.Idle;
        Diagnostics = [];
        NotifyChanged();
    }

    public void ApplyLocalBroker(MqttConnectionProfile profile, string subscription)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.UpsertBroker(DefinitionJson, profile, subscription));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "DefinitionEditFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public IReadOnlyList<(MqttConnectionProfile Profile, string Subscription)> GetConnectionProfiles()
        => _definitionComposer.ReadConnectionsFromDefinition(DefinitionJson);

    public IReadOnlyList<string> GetConnectionNames()
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(DefinitionJson);
            var root = document.RootElement;

            if (root.TryGetProperty("FluxMq", out var fluxMq) &&
                fluxMq.TryGetProperty("FlowApplication", out var app))
            {
                root = app;
            }

            if (!root.TryGetProperty("resources", out var resources) ||
                resources.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return [];
            }

            return resources.EnumerateObject()
                .Where(r => r.Value.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            r.Value.TryGetProperty("type", out var t) &&
                            t.GetString() == "mqtt.connection")
                .Select(r => r.Name)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public void AddComponent(string componentType)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.AddComponent(DefinitionJson, componentType, _activeWorkflowName));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "DefinitionEditFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public void UpdateNodeConfiguration(string nodeName, System.Text.Json.Nodes.JsonObject configuration)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.UpdateNodeConfiguration(DefinitionJson, nodeName, configuration));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "NodeUpdateFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public void RenameWorkflowNode(string workflowName, string oldName, string newName)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.RenameWorkflowNode(DefinitionJson, workflowName, oldName, newName));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "NodeRenameFailed", exception.Message)];
        }
        NotifyChanged();
    }

    public void UpsertConnectionResource(string resourceName, MqttConnectionProfile profile)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.UpsertConnectionResource(DefinitionJson, resourceName, profile));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "ConnectionAddFailed", exception.Message)];
        }
        NotifyChanged();
    }

    public void SyncConnectionAndUpdateNode(string resourceName, MqttConnectionProfile profile, string nodeName, System.Text.Json.Nodes.JsonObject configuration)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.SyncConnectionAndSaveNode(DefinitionJson, resourceName, profile, nodeName, configuration));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "DefinitionEditFailed", exception.Message)
            ];
        }

        NotifyChanged();
    }

    public async Task LoadFromFileAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var fileJson = await File.ReadAllTextAsync(CurrentFilePath, cancellationToken).ConfigureAwait(false);
            StagedNodePositions = _definitionComposer.ReadNodePositions(fileJson);
            ReplaceDefinitionSilent(_definitionComposer.StripDesignerSection(fileJson));
            _activeWorkflowName = _definitionComposer.GetWorkflowNames(DefinitionJson).FirstOrDefault();
            HasUnsavedChanges = false;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "File", "LoadFailed", exception.Message)
            ];
        }
        finally
        {
            _gate.Release();
            NotifyChanged();
        }
    }

    public async Task SaveToFileAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
            return;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(CurrentFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var jsonToWrite = GetDiagramState is { } capture
                ? _definitionComposer.WriteNodePositions(DefinitionJson, capture())
                : DefinitionJson;
            await File.WriteAllTextAsync(CurrentFilePath, jsonToWrite, cancellationToken).ConfigureAwait(false);
            HasUnsavedChanges = false;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "File", "Saved", $"Saved to {CurrentFilePath}")
            ];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "File", "SaveFailed", exception.Message)
            ];
        }
        finally
        {
            _gate.Release();
            NotifyChanged();
        }
    }

    public async Task SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        CurrentFilePath = path;
        await SaveToFileAsync(cancellationToken);
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson), _messageRepository);
            var result = _host.Build();
            Diagnostics = CollectDiagnostics(result);
            State = result.IsSuccess ? RuntimeWorkspaceState.Valid : RuntimeWorkspaceState.Faulted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Validation", "Unhandled", exception.Message)
            ];
        }
        finally
        {
            _gate.Release();
            NotifyChanged();
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson), _messageRepository);
            var result = await _host.StartAsync(cancellationToken).ConfigureAwait(false);
            Diagnostics = CollectDiagnostics(result);
            State = result.IsSuccess ? RuntimeWorkspaceState.Running : RuntimeWorkspaceState.Faulted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Runtime", "StartFailed", exception.Message)
            ];
        }
        finally
        {
            _gate.Release();
            NotifyChanged();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_host is not null)
                await _host.StopAsync(cancellationToken).ConfigureAwait(false);

            State = RuntimeWorkspaceState.Stopped;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "Runtime", "Stopped", "Flow application stopped.")
            ];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Runtime", "StopFailed", exception.Message)
            ];
        }
        finally
        {
            _gate.Release();
            NotifyChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHostAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private static IConfiguration CreateConfiguration(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(bytes))
            .Build();
    }

    private static IReadOnlyList<WorkspaceDiagnostic> CollectDiagnostics(FlowApplicationHostBuildResult result)
    {
        var diagnostics = new List<WorkspaceDiagnostic>();

        diagnostics.AddRange(result.Errors.Select(error => new WorkspaceDiagnostic(
            "Error", "Host", error.Code.ToString(), error.Message)));

        if (result.RuntimeBuild is not null)
        {
            diagnostics.AddRange(result.RuntimeBuild.Validation.Errors.Select(error => new WorkspaceDiagnostic(
                "Error", "Definition", error.Code.ToString(), error.Message)));

            diagnostics.AddRange(result.RuntimeBuild.Errors.Select(error => new WorkspaceDiagnostic(
                "Error", "RuntimeBuild", error.Code.ToString(), error.Message)));
        }

        if (diagnostics.Count == 0)
            diagnostics.Add(new WorkspaceDiagnostic("Info", "Runtime", "Ready", "Flow application is valid."));

        return diagnostics;
    }

    private async ValueTask DisposeHostAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }

    private void ReplaceDefinition(string json)
    {
        if (string.Equals(DefinitionJson, json, StringComparison.Ordinal))
            return;

        DefinitionJson = json;
        DefinitionRevision++;
        HasUnsavedChanges = true;
    }

    private void ReplaceDefinitionSilent(string json)
    {
        if (string.Equals(DefinitionJson, json, StringComparison.Ordinal))
            return;

        DefinitionJson = json;
        DefinitionRevision++;
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
