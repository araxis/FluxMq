using FluxMq.App;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MessageSource;
using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Components.Logging;
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
    private static readonly TimeSpan RuntimeProjectionNotificationInterval = TimeSpan.FromMilliseconds(250);
    private readonly FlowDefinitionComposer _definitionComposer;
    private readonly IMessageRepository? _messageRepository;
    private readonly Func<MqttConnectionProfile, IMqttSession>? _runtimeSessionFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _logSync = new();
    private readonly object _metricsSync = new();
    private readonly object _payloadInspectionSync = new();
    private readonly object _triggerActivitySync = new();
    private readonly List<WorkspaceLogEntry> _logs = [];
    private readonly Dictionary<string, MqttMetricsSnapshot> _metricsSnapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InspectedMqttMessage> _payloadInspections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MqttTriggerActivitySnapshot> _triggerActivitySnapshots = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _runtimeProjectionLinks = [];
    private readonly List<IDataflowBlock> _runtimeProjectionTargets = [];
    private FlowApplicationHost? _host;
    private int _runtimeProjectionNotificationQueued;

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
    public IReadOnlyDictionary<string, MqttMetricsSnapshot> MetricsSnapshots { get; private set; } =
        new Dictionary<string, MqttMetricsSnapshot>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, InspectedMqttMessage> PayloadInspections { get; private set; } =
        new Dictionary<string, InspectedMqttMessage>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, MqttTriggerActivitySnapshot> TriggerActivitySnapshots { get; private set; } =
        new Dictionary<string, MqttTriggerActivitySnapshot>(StringComparer.Ordinal);

    public IReadOnlyList<string> WorkflowNames => _definitionComposer.GetWorkflowNames(DefinitionJson);
    public string? ActiveWorkflowName => _activeWorkflowName;

    public MqttMetricsSnapshot GetMetricsSnapshot(string? workflowName, string nodeName)
    {
        lock (_metricsSync)
        {
            return _metricsSnapshots.TryGetValue(MetricsKey(workflowName, nodeName), out var snapshot)
                ? snapshot
                : new MqttMetricsSnapshot();
        }
    }

    public InspectedMqttMessage? GetPayloadInspection(string? workflowName, string nodeName)
    {
        lock (_payloadInspectionSync)
        {
            return _payloadInspections.TryGetValue(RuntimeProjectionKey(workflowName, nodeName), out var inspection)
                ? inspection
                : null;
        }
    }

    public MqttTriggerActivitySnapshot GetTriggerActivitySnapshot(string? workflowName, string nodeName)
    {
        lock (_triggerActivitySync)
        {
            return _triggerActivitySnapshots.TryGetValue(RuntimeProjectionKey(workflowName, nodeName), out var snapshot)
                ? snapshot
                : new MqttTriggerActivitySnapshot();
        }
    }

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
            ClearRuntimeMetricsSnapshots(notify: false);
            ClearRuntimePayloadInspections(notify: false);
            ClearRuntimeTriggerActivitySnapshots(notify: false);
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
            ClearRuntimeMetricsSnapshots(notify: false);
            ClearRuntimePayloadInspections(notify: false);
            ClearRuntimeTriggerActivitySnapshots(notify: false);
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson), _messageRepository, _runtimeSessionFactory);
            var result = _host.Build();
            if (result.IsSuccess)
            {
                AttachRuntimeProjections();
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
        DisposeRuntimeProjectionSubscriptions();

        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }

    private void AttachRuntimeProjections()
    {
        DisposeRuntimeProjectionSubscriptions();

        if (_host?.Runtime is null)
        {
            return;
        }

        foreach (var node in _host.Runtime.Nodes)
        {
            AttachRuntimeErrorOutputs(node);

            if (node.Node is FlowLoggerComponent logger)
            {
                AppendLogs(logger.RecentEntries.Select(entry => ToWorkspaceLogEntry(node.Address, entry)), notify: false);
            }

            AttachRuntimeLogOutputs(node);
            AttachRuntimeMetricsOutputs(node);
            AttachRuntimePayloadInspectionOutputs(node);
            AttachRuntimeTriggerActivityOutputs(node);
        }
    }

    private void AttachRuntimeMetricsOutputs(RuntimeNode node)
    {
        foreach (var output in node.Outputs.OfType<OutputPort<MqttMetricsSnapshot>>())
        {
            AttachRuntimeMetricsSnapshots(node, output.Source);
        }
    }

    private void AttachRuntimeMetricsSnapshots(RuntimeNode node, ISourceBlock<MqttMetricsSnapshot> snapshots)
    {
        var address = node.Address;
        var target = new ActionBlock<MqttMetricsSnapshot>(
            snapshot => StoreRuntimeMetricsSnapshot(address, snapshot),
            new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _runtimeProjectionTargets.Add(target);
        _runtimeProjectionLinks.Add(snapshots.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
    }

    private void AttachRuntimePayloadInspectionOutputs(RuntimeNode node)
    {
        foreach (var output in node.Outputs.OfType<OutputPort<InspectedMqttMessage>>())
        {
            AttachRuntimePayloadInspections(node, output.Source);
        }
    }

    private void AttachRuntimePayloadInspections(RuntimeNode node, ISourceBlock<InspectedMqttMessage> inspections)
    {
        var address = node.Address;
        var target = new ActionBlock<InspectedMqttMessage>(
            inspection => StoreRuntimePayloadInspection(address, inspection),
            new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _runtimeProjectionTargets.Add(target);
        _runtimeProjectionLinks.Add(inspections.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
    }

    private void AttachRuntimeTriggerActivityOutputs(RuntimeNode node)
    {
        if (node.Node is not MqttTriggerComponent)
        {
            return;
        }

        foreach (var output in node.Outputs.OfType<OutputPort<MqttEnvelope>>())
        {
            if (string.Equals(output.Address.Port.Value, "Output", StringComparison.OrdinalIgnoreCase))
            {
                AttachRuntimeTriggerActivity(node, output.Source);
            }
        }
    }

    private void AttachRuntimeTriggerActivity(RuntimeNode node, ISourceBlock<MqttEnvelope> envelopes)
    {
        var address = node.Address;
        var target = new ActionBlock<MqttEnvelope>(
            envelope => StoreRuntimeTriggerActivity(address, envelope),
            new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _runtimeProjectionTargets.Add(target);
        _runtimeProjectionLinks.Add(envelopes.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
    }

    private void AttachRuntimeLogOutputs(RuntimeNode node)
    {
        foreach (var output in node.Outputs.OfType<OutputPort<FlowLogEntry>>())
        {
            AttachRuntimeLogEntries(node, output.Source);
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

        _runtimeProjectionTargets.Add(target);
        _runtimeProjectionLinks.Add(entries.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
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

            _runtimeProjectionTargets.Add(target);
            _runtimeProjectionLinks.Add(output.Source.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
        }
    }

    private void DisposeRuntimeProjectionSubscriptions()
    {
        foreach (var link in _runtimeProjectionLinks)
        {
            link.Dispose();
        }

        foreach (var target in _runtimeProjectionTargets)
        {
            target.Complete();
        }

        _runtimeProjectionLinks.Clear();
        _runtimeProjectionTargets.Clear();
    }

    private void StoreRuntimeMetricsSnapshot(NodeAddress address, MqttMetricsSnapshot snapshot)
    {
        lock (_metricsSync)
        {
            _metricsSnapshots[MetricsKey(address.Scope, address.Node.Value)] = snapshot;
            MetricsSnapshots = new Dictionary<string, MqttMetricsSnapshot>(_metricsSnapshots, StringComparer.Ordinal);
        }

        NotifyRuntimeProjectionChanged();
    }

    private void StoreRuntimePayloadInspection(NodeAddress address, InspectedMqttMessage inspection)
    {
        lock (_payloadInspectionSync)
        {
            _payloadInspections[RuntimeProjectionKey(address.Scope, address.Node.Value)] = inspection;
            PayloadInspections = new Dictionary<string, InspectedMqttMessage>(_payloadInspections, StringComparer.Ordinal);
        }

        NotifyRuntimeProjectionChanged();
    }

    private void StoreRuntimeTriggerActivity(NodeAddress address, MqttEnvelope envelope)
    {
        lock (_triggerActivitySync)
        {
            var key = RuntimeProjectionKey(address.Scope, address.Node.Value);
            var current = _triggerActivitySnapshots.TryGetValue(key, out var snapshot)
                ? snapshot
                : new MqttTriggerActivitySnapshot();

            _triggerActivitySnapshots[key] = current with
            {
                MessageCount = current.MessageCount + 1,
                LastTopic = envelope.Topic,
                LastPayloadBytes = envelope.Payload.Length,
                LastReceivedAt = envelope.ReceivedAt
            };
            TriggerActivitySnapshots = new Dictionary<string, MqttTriggerActivitySnapshot>(_triggerActivitySnapshots, StringComparer.Ordinal);
        }

        NotifyRuntimeProjectionChanged();
    }

    private void ClearRuntimeMetricsSnapshots(bool notify)
    {
        lock (_metricsSync)
        {
            _metricsSnapshots.Clear();
            MetricsSnapshots = new Dictionary<string, MqttMetricsSnapshot>(StringComparer.Ordinal);
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private void ClearRuntimePayloadInspections(bool notify)
    {
        lock (_payloadInspectionSync)
        {
            _payloadInspections.Clear();
            PayloadInspections = new Dictionary<string, InspectedMqttMessage>(StringComparer.Ordinal);
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private void ClearRuntimeTriggerActivitySnapshots(bool notify)
    {
        lock (_triggerActivitySync)
        {
            _triggerActivitySnapshots.Clear();
            TriggerActivitySnapshots = new Dictionary<string, MqttTriggerActivitySnapshot>(StringComparer.Ordinal);
        }

        if (notify)
        {
            NotifyChanged();
        }
    }

    private void NotifyRuntimeProjectionChanged()
    {
        if (Interlocked.Exchange(ref _runtimeProjectionNotificationQueued, 1) == 1)
        {
            return;
        }

        _ = NotifyRuntimeProjectionChangedAsync();
    }

    private async Task NotifyRuntimeProjectionChangedAsync()
    {
        try
        {
            await Task.Delay(RuntimeProjectionNotificationInterval).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _runtimeProjectionNotificationQueued, 0);
        }

        NotifyChanged();
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
        ClearRuntimeMetricsSnapshots(notify: false);
        ClearRuntimePayloadInspections(notify: false);
        ClearRuntimeTriggerActivitySnapshots(notify: false);
    }

    private void ReplaceDefinitionSilent(string json)
    {
        if (string.Equals(DefinitionJson, json, StringComparison.Ordinal))
            return;

        DefinitionJson = json;
        DefinitionRevision++;
        ClearRuntimeMetricsSnapshots(notify: false);
        ClearRuntimePayloadInspections(notify: false);
        ClearRuntimeTriggerActivitySnapshots(notify: false);
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string MetricsKey(string? workflowName, string nodeName)
        => RuntimeProjectionKey(workflowName, nodeName);

    private static string RuntimeProjectionKey(string? workflowName, string nodeName)
        => $"{workflowName ?? string.Empty}/{nodeName}";
}
