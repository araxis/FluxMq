using FluxMq.App;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttPublisher;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using FluxMq.UI.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.UI.Services;

public sealed class FlowWorkspaceService : IAsyncDisposable
{
    private const int MaxWorkspaceLogs = 1000;
    private static readonly TimeSpan RuntimeStopTimeout = TimeSpan.FromSeconds(5);
    private readonly FlowDefinitionComposer _definitionComposer;
    private readonly IMessageRepository? _messageRepository;
    private readonly Func<MqttConnectionProfile, IMqttSession>? _runtimeSessionFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _logSync = new();
    private readonly List<WorkspaceLogEntry> _logs = [];
    private readonly List<IDisposable> _runtimeLogLinks = [];
    private readonly List<IDataflowBlock> _runtimeLogTargets = [];
    private FlowApplicationHost? _host;

    public FlowWorkspaceService(
        FlowDefinitionComposer definitionComposer,
        IMessageRepository? messageRepository = null,
        Func<MqttConnectionProfile, IMqttSession>? runtimeSessionFactory = null)
    {
        _definitionComposer = definitionComposer;
        _messageRepository = messageRepository;
        _runtimeSessionFactory = runtimeSessionFactory;
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
    public IReadOnlyList<WorkspaceLogEntry> Logs { get; private set; } = [];

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

    public void ClearLogs()
    {
        lock (_logSync)
        {
            _logs.Clear();
            Logs = [];
        }

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

    public IReadOnlyList<(string Name, MqttConnectionProfile Profile, string Subscription)> GetConnectionResources()
        => _definitionComposer.ReadConnectionResourcesFromDefinition(DefinitionJson);

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
            ReplaceDefinition(_definitionComposer.UpdateNodeConfiguration(DefinitionJson, nodeName, configuration, _activeWorkflowName));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "NodeUpdateFailed", exception.Message, _activeWorkflowName, nodeName)
            ];
        }

        NotifyChanged();
    }

    public void ConnectWorkflowPorts(
        string sourceNodeName,
        string sourcePortName,
        string targetNodeName,
        string targetPortName,
        bool replaceTargetPortLinks)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.ConnectWorkflowPorts(
                DefinitionJson,
                _activeWorkflowName,
                sourceNodeName,
                sourcePortName,
                targetNodeName,
                targetPortName,
                replaceTargetPortLinks));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "PortConnectFailed", exception.Message, _activeWorkflowName, targetNodeName, targetPortName)
            ];
        }

        NotifyChanged();
    }

    public void RemoveWorkflowPortLink(
        string sourceNodeName,
        string sourcePortName,
        string targetNodeName,
        string targetPortName)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.RemoveWorkflowPortLink(
                DefinitionJson,
                _activeWorkflowName,
                sourceNodeName,
                sourcePortName,
                targetNodeName,
                targetPortName));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "PortDisconnectFailed", exception.Message, _activeWorkflowName, targetNodeName, targetPortName)
            ];
        }

        NotifyChanged();
    }

    public void RemoveWorkflowNode(string nodeName)
    {
        try
        {
            ReplaceDefinition(_definitionComposer.RemoveWorkflowNode(DefinitionJson, _activeWorkflowName, nodeName));
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Designer", "NodeRemoveFailed", exception.Message, _activeWorkflowName, nodeName)
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
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "NodeRenameFailed", exception.Message, workflowName, oldName)];
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
            ReplaceDefinition(_definitionComposer.SyncConnectionAndSaveNode(DefinitionJson, resourceName, profile, nodeName, configuration, _activeWorkflowName));
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
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson), _messageRepository, _runtimeSessionFactory);
            var result = _host.Build();
            Diagnostics = CollectDiagnostics(result);
            State = result.IsSuccess ? RuntimeWorkspaceState.Valid : RuntimeWorkspaceState.Faulted;
            AppendDiagnosticsToLogs(Diagnostics, notify: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Validation", "Unhandled", exception.Message)
            ];
            AppendDiagnosticsToLogs(Diagnostics, notify: false);
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
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson), _messageRepository, _runtimeSessionFactory);
            var result = _host.Build();
            if (result.IsSuccess)
            {
                AttachRuntimeLoggers();
                result = await _host.StartBuiltAsync(cancellationToken).ConfigureAwait(false);
            }

            Diagnostics = CollectDiagnostics(result);
            State = result.IsSuccess ? RuntimeWorkspaceState.Running : RuntimeWorkspaceState.Faulted;
            AppendDiagnosticsToLogs(Diagnostics, notify: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Runtime", "StartFailed", exception.Message)
            ];
            AppendDiagnosticsToLogs(Diagnostics, notify: false);
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
            {
                using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                stopCts.CancelAfter(RuntimeStopTimeout);

                try
                {
                    await _host.StopAsync(stopCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await DisposeHostAsync().ConfigureAwait(false);
                    State = RuntimeWorkspaceState.Stopped;
                    Diagnostics =
                    [
                        new WorkspaceDiagnostic("Warning", "Runtime", "StopTimedOut", "Flow application stop timed out; runtime was disposed.")
                    ];
                    AppendDiagnosticsToLogs(Diagnostics, notify: false);
                    return;
                }
            }

            State = RuntimeWorkspaceState.Stopped;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Info", "Runtime", "Stopped", "Flow application stopped.")
            ];
            AppendDiagnosticsToLogs(Diagnostics, notify: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Runtime", "StopFailed", exception.Message)
            ];
            AppendDiagnosticsToLogs(Diagnostics, notify: false);
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
            "Error",
            "Host",
            error.Code.ToString(),
            error.Message,
            error.WorkflowName,
            error.NodeName,
            error.PortName)));

        if (result.RuntimeBuild is not null)
        {
            diagnostics.AddRange(result.RuntimeBuild.Validation.Errors.Select(error => new WorkspaceDiagnostic(
                "Error",
                "Definition",
                error.Code.ToString(),
                error.Message,
                error.WorkflowName,
                error.NodeName,
                error.PortName)));

            diagnostics.AddRange(result.RuntimeBuild.Errors
                .Where(error => error.Code != ApplicationRuntimeBuildErrorCode.ValidationFailed)
                .Select(error => new WorkspaceDiagnostic(
                    "Error",
                    "RuntimeBuild",
                    error.Code.ToString(),
                    error.Message,
                    error.WorkflowName,
                    error.NodeName?.Value,
                    error.PortName?.Value)));
        }

        if (diagnostics.Count == 0)
            diagnostics.Add(new WorkspaceDiagnostic("Info", "Runtime", "Ready", "Flow application is valid."));

        return diagnostics;
    }

    private async ValueTask DisposeHostAsync()
    {
        DisposeRuntimeLogSubscriptions();

        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }

    private void AttachRuntimeLoggers()
    {
        DisposeRuntimeLogSubscriptions();

        if (_host?.Runtime is null)
        {
            return;
        }

        foreach (var node in _host.Runtime.Nodes)
        {
            AttachRuntimeErrorOutputs(node);

            if (node.Node is not FlowLoggerComponent logger)
            {
                if (node.Node is MqttPublisherComponent publisher)
                {
                    AttachRuntimeLogEntries(node, publisher.Entries);
                }

                continue;
            }

            AppendLogs(logger.RecentEntries.Select(entry => ToWorkspaceLogEntry(node.Address, entry)), notify: false);
            AttachRuntimeLogEntries(node, logger.Entries);
        }
    }

    private void AttachRuntimeLogEntries(RuntimeNode node, ISourceBlock<FlowLogEntry> entries)
    {
        var address = node.Address;
        var target = new ActionBlock<FlowLogEntry>(
            entry => AppendLog(ToWorkspaceLogEntry(address, entry)),
            new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _runtimeLogTargets.Add(target);
        _runtimeLogLinks.Add(entries.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
    }

    private void AttachRuntimeErrorOutputs(RuntimeNode node)
    {
        foreach (var output in node.Outputs.OfType<OutputPort<FlowError>>())
        {
            var address = node.Address;
            var portName = output.Address.Port.Value;
            var target = new ActionBlock<FlowError>(
                error => AppendLog(ToWorkspaceLogEntry(address, portName, error)),
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = true,
                    MaxDegreeOfParallelism = 1
                });

            _runtimeLogTargets.Add(target);
            _runtimeLogLinks.Add(output.Source.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
        }
    }

    private void DisposeRuntimeLogSubscriptions()
    {
        foreach (var link in _runtimeLogLinks)
        {
            link.Dispose();
        }

        foreach (var target in _runtimeLogTargets)
        {
            target.Complete();
        }

        _runtimeLogLinks.Clear();
        _runtimeLogTargets.Clear();
    }

    private void AppendDiagnosticsToLogs(IEnumerable<WorkspaceDiagnostic> diagnostics, bool notify = true)
        => AppendLogs(diagnostics.Select(WorkspaceLogEntry.FromDiagnostic), notify);

    private void AppendLog(WorkspaceLogEntry entry)
        => AppendLogs([entry]);

    private void AppendLogs(IEnumerable<WorkspaceLogEntry> entries, bool notify = true)
    {
        var materialized = entries.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        lock (_logSync)
        {
            foreach (var entry in materialized)
            {
                _logs.Add(entry);
            }

            if (_logs.Count > MaxWorkspaceLogs)
            {
                _logs.RemoveRange(0, _logs.Count - MaxWorkspaceLogs);
            }

            Logs = _logs.ToArray();
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private static WorkspaceLogEntry ToWorkspaceLogEntry(NodeAddress address, FlowLogEntry entry)
        => new(
            entry.Timestamp,
            entry.Severity.ToString(),
            entry.Source,
            entry.ErrorCode?.ToString() ?? entry.Source,
            entry.Message,
            address.Scope,
            address.Node.Value,
            null,
            BuildRuntimeLogContext(entry));

    private static WorkspaceLogEntry ToWorkspaceLogEntry(NodeAddress address, string portName, FlowError error)
        => new(
            error.OccurredAt,
            "Error",
            "FlowError",
            error.Code.ToString(),
            error.Message,
            address.Scope,
            address.Node.Value,
            portName,
            BuildFlowErrorContext(error));

    private static string? BuildRuntimeLogContext(FlowLogEntry entry)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(entry.Topic))
        {
            parts.Add($"topic={entry.Topic}");
        }

        if (entry.PayloadBytes is not null)
        {
            parts.Add($"payloadBytes={entry.PayloadBytes}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Context))
        {
            parts.Add(entry.Context);
        }

        if (!string.IsNullOrWhiteSpace(entry.PayloadPreview))
        {
            parts.Add($"payload={entry.PayloadPreview}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? BuildFlowErrorContext(FlowError error)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(error.Context))
        {
            parts.Add(error.Context);
        }

        if (error.Exception is not null)
        {
            parts.Add($"exception={error.Exception.GetType().Name}: {error.Exception.Message}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
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
