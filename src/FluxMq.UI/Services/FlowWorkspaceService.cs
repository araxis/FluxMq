using FluxMq.App;
using FluxMq.App.Scenarios;
using FluxMq.Core.Ids;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.MessageSource;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.Logging;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using FluxMq.Pipeline.Scenarios;
using FluxMq.UI.Models;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.UI.Services;

public sealed class FlowWorkspaceService : IAsyncDisposable
{
    private const int MaxWorkspaceLogs = 1000;
    private const int MaxRuntimeEvents = 1000;
    private const int MaxScenarioRunHistory = 20;
    private const int MaxFlowEventPayloadPreviewChars = 512;
    private const double DefaultAddedNodeX = 420d;
    private const double DefaultAddedNodeY = 120d;
    private const double AddedNodeColumnSpacing = 300d;
    private const double AddedNodeRowSpacing = 170d;
    private const int AddedNodeRowsBeforeNewColumn = 4;
    private static readonly TimeSpan RuntimeStopTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RuntimeProjectionNotificationInterval = TimeSpan.FromMilliseconds(250);
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly FlowDefinitionComposer _definitionComposer;
    private readonly IMessageRepository? _messageRepository;
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient>? _runtimeClientFactory;
    private readonly DashboardEventFilterCatalog _dashboardEventFilters;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _logSync = new();
    private readonly object _metricsSync = new();
    private readonly object _payloadInspectionSync = new();
    private readonly object _triggerActivitySync = new();
    private readonly object _runtimeEventSync = new();
    private readonly object _scenarioHistorySync = new();
    private readonly List<WorkspaceLogEntry> _logs = [];
    private readonly List<FlowEvent> _runtimeEvents = [];
    private readonly List<ScenarioRunResult> _scenarioRunHistory = [];
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
        Func<MqttConnectionProfile, IMqttBrokerClient>? runtimeClientFactory = null,
        DashboardEventFilterCatalog? dashboardEventFilters = null)
    {
        _definitionComposer = definitionComposer;
        _messageRepository = messageRepository;
        _runtimeClientFactory = runtimeClientFactory;
        _dashboardEventFilters = dashboardEventFilters ?? DashboardEventFilterCatalog.Shared;
        DefinitionJson = _definitionComposer.CreateEmptyDefinition();
    }

    private string? _activeWorkflowName;
    private string? _activeDashboardName;
    private string? _activeTestName;
    private WorkspaceArtifactKind _activeArtifactKind = WorkspaceArtifactKind.Pipeline;
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
    public IReadOnlyList<FlowEvent> RuntimeEvents { get; private set; } = [];
    public bool IsScenarioRunning { get; private set; }
    public ScenarioRunResult? LastScenarioRunResult { get; private set; }
    public IReadOnlyList<ScenarioRunResult> ScenarioRunHistory { get; private set; } = [];

    public IReadOnlyList<string> WorkflowNames => _definitionComposer.GetWorkflowNames(DefinitionJson);
    public IReadOnlyList<string> DashboardNames => _definitionComposer.GetDashboardNames(DefinitionJson);
    public IReadOnlyList<string> TestNames => _definitionComposer.GetTestNames(DefinitionJson);
    public string? ActiveWorkflowName => _activeWorkflowName;
    public string? ActiveDashboardName => _activeDashboardName;
    public string? ActiveDashboardCellName { get; private set; }
    public string? ActiveTestName => _activeTestName;
    public WorkspaceArtifactKind ActiveArtifactKind => _activeArtifactKind;
    public string? ActiveArtifactName => _activeArtifactKind switch
    {
        WorkspaceArtifactKind.Pipeline => _activeWorkflowName,
        WorkspaceArtifactKind.Dashboard => _activeDashboardName,
        WorkspaceArtifactKind.Test => _activeTestName,
        WorkspaceArtifactKind.Logs => "Logs",
        _ => null
    };

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

    public DashboardEventSnapshot GetDashboardEventSnapshot(DashboardWidgetSnapshot widget)
    {
        lock (_runtimeEventSync)
        {
            var matching = _runtimeEvents
                .Where(flowEvent => MatchesDashboardEventWidget(widget, flowEvent))
                .ToArray();
            return new DashboardEventSnapshot(matching.Length, matching.LastOrDefault());
        }
    }

    public void RecordManualMqttPublish(
        string topic,
        string payload,
        int qualityOfService,
        bool retain,
        string? connectionName = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var payloadBytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["qos"] = qualityOfService.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["retain"] = retain.ToString()
        };

        if (!string.IsNullOrWhiteSpace(connectionName))
        {
            attributes["connection"] = connectionName.Trim();
        }

        var flowEvent = new FlowEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FluxMqEventTypes.MqttMessagePublished,
            Source = "LivePublisher",
            Subject = topic.Trim(),
            Status = "published",
            Topic = topic.Trim(),
            PayloadBytes = payloadBytes.Length,
            PayloadPreview = CreatePayloadPreview(payloadBytes),
            Attributes = attributes
        };

        StoreWorkspaceEvent(flowEvent, address: null);
    }

    public TestScenarioSnapshot? GetActiveTestScenario()
        => string.IsNullOrWhiteSpace(_activeTestName)
            ? null
            : _definitionComposer.GetTestScenario(DefinitionJson, _activeTestName);

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
        if (string.Equals(_activeWorkflowName, name, StringComparison.Ordinal) &&
            _activeArtifactKind == WorkspaceArtifactKind.Pipeline)
        {
            return;
        }

        _activeWorkflowName = name;
        ActiveDashboardCellName = null;
        _activeArtifactKind = WorkspaceArtifactKind.Pipeline;
        NotifyChanged();
    }

    public void SetActiveDashboard(string name)
    {
        if (string.Equals(_activeDashboardName, name, StringComparison.Ordinal) &&
            _activeArtifactKind == WorkspaceArtifactKind.Dashboard)
        {
            return;
        }

        _activeDashboardName = name;
        ActiveDashboardCellName = null;
        _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
        NotifyChanged();
    }

    public void SetActiveTest(string name)
    {
        if (string.Equals(_activeTestName, name, StringComparison.Ordinal) &&
            _activeArtifactKind == WorkspaceArtifactKind.Test)
        {
            return;
        }

        _activeTestName = name;
        ActiveDashboardCellName = null;
        _activeArtifactKind = WorkspaceArtifactKind.Test;
        LastScenarioRunResult = LatestScenarioRunForTest(name);
        NotifyChanged();
    }

    public void SetActiveLogs()
    {
        if (_activeArtifactKind == WorkspaceArtifactKind.Logs)
        {
            return;
        }

        ActiveDashboardCellName = null;
        _activeArtifactKind = WorkspaceArtifactKind.Logs;
        NotifyChanged();
    }

    public void AddWorkflow(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            ReplaceDefinition(_definitionComposer.AddWorkflow(DefinitionJson, name));
            _activeWorkflowName ??= name;
            _activeArtifactKind = WorkspaceArtifactKind.Pipeline;
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

    public void AddDashboard(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            ReplaceDefinition(_definitionComposer.AddDashboard(DefinitionJson, name));
            _activeDashboardName = name;
            ActiveDashboardCellName = null;
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardAddFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void AddTest(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            ReplaceDefinition(_definitionComposer.AddTest(DefinitionJson, name));
            _activeTestName = name;
            _activeArtifactKind = WorkspaceArtifactKind.Test;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "TestAddFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public DashboardLayoutSnapshot? GetActiveDashboardLayout()
        => string.IsNullOrWhiteSpace(_activeDashboardName)
            ? null
            : _definitionComposer.GetDashboardLayout(DefinitionJson, _activeDashboardName);

    public void SetActiveDashboardCell(string? cellName)
    {
        var normalized = string.IsNullOrWhiteSpace(cellName) ? null : cellName;
        if (string.Equals(ActiveDashboardCellName, normalized, StringComparison.Ordinal))
        {
            return;
        }

        ActiveDashboardCellName = normalized;
        NotifyChanged();
    }

    public void AddDashboardWidget(string widgetType, string? cellName = null)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.AddDashboardWidget(
                DefinitionJson,
                _activeDashboardName,
                widgetType,
                string.IsNullOrWhiteSpace(cellName) ? ActiveDashboardCellName : cellName));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardWidgetAddFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void UpdateDashboardWidget(string widgetName, IReadOnlyDictionary<string, string> configuration)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.UpdateDashboardWidgetConfiguration(
                DefinitionJson,
                _activeDashboardName,
                widgetName,
                configuration));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardWidgetUpdateFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void RemoveDashboardWidget(string widgetName)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName) ||
            string.IsNullOrWhiteSpace(widgetName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.RemoveDashboardWidget(DefinitionJson, _activeDashboardName, widgetName));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardWidgetRemoveFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void AddTestScenarioStep(string stepType)
    {
        if (string.IsNullOrWhiteSpace(_activeTestName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.AddScenarioStep(DefinitionJson, _activeTestName, stepType));
            _activeArtifactKind = WorkspaceArtifactKind.Test;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
            LastScenarioRunResult = null;
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "ScenarioStepAddFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void UpdateTestScenarioStep(
        string stepName,
        string stepType,
        IReadOnlyDictionary<string, string> configuration)
    {
        if (string.IsNullOrWhiteSpace(_activeTestName) ||
            string.IsNullOrWhiteSpace(stepName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.UpdateScenarioStep(
                DefinitionJson,
                _activeTestName,
                stepName,
                stepType,
                configuration));
            _activeArtifactKind = WorkspaceArtifactKind.Test;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
            LastScenarioRunResult = null;
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "ScenarioStepUpdateFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void RemoveTestScenarioStep(string stepName)
    {
        if (string.IsNullOrWhiteSpace(_activeTestName) ||
            string.IsNullOrWhiteSpace(stepName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.RemoveScenarioStep(DefinitionJson, _activeTestName, stepName));
            _activeArtifactKind = WorkspaceArtifactKind.Test;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
            LastScenarioRunResult = null;
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "ScenarioStepRemoveFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void MoveTestScenarioStep(string stepName, int offset)
    {
        if (string.IsNullOrWhiteSpace(_activeTestName) ||
            string.IsNullOrWhiteSpace(stepName) ||
            offset == 0)
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.MoveScenarioStep(DefinitionJson, _activeTestName, stepName, offset));
            _activeArtifactKind = WorkspaceArtifactKind.Test;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
            LastScenarioRunResult = null;
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "ScenarioStepMoveFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void UpdateDashboardGridTracks(IEnumerable<string> columns, IEnumerable<string> rows)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.UpdateDashboardGridTracks(DefinitionJson, _activeDashboardName, columns, rows));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardUpdateFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void ResizeDashboardGrid(int rows, int columns)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.ResizeDashboardGrid(DefinitionJson, _activeDashboardName, rows, columns));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardResizeFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void UpdateDashboardTrack(string axis, int index, string size, double padding)
        => UpdateDashboardTopology(
            json => _definitionComposer.UpdateDashboardTrack(json, _activeDashboardName!, axis, index, size, padding),
            "DashboardTrackUpdateFailed");

    public void AddDashboardRow()
        => UpdateDashboardTopology(json => _definitionComposer.AddDashboardRow(json, _activeDashboardName!), "DashboardRowAddFailed");

    public void RemoveDashboardRow()
        => UpdateDashboardTopology(json => _definitionComposer.RemoveDashboardRow(json, _activeDashboardName!), "DashboardRowRemoveFailed");

    public void AddDashboardColumn()
        => UpdateDashboardTopology(json => _definitionComposer.AddDashboardColumn(json, _activeDashboardName!), "DashboardColumnAddFailed");

    public void RemoveDashboardColumn()
        => UpdateDashboardTopology(json => _definitionComposer.RemoveDashboardColumn(json, _activeDashboardName!), "DashboardColumnRemoveFailed");

    public void AddDashboardCell()
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.AddDashboardCell(DefinitionJson, _activeDashboardName));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardCellAddFailed", exception.Message)];
        }

        NotifyChanged();
    }

    public void MergeDashboardCells(IEnumerable<DashboardCellSnapshot> selectedCells)
        => UpdateDashboardTopology(
            json => _definitionComposer.MergeDashboardCells(json, _activeDashboardName!, selectedCells),
            "DashboardCellMergeFailed");

    public void SplitDashboardCell(string cellName)
        => UpdateDashboardTopology(
            json => _definitionComposer.SplitDashboardCell(json, _activeDashboardName!, cellName),
            "DashboardCellSplitFailed");

    public void SubdivideDashboardCell(DashboardCellSnapshot selectedCell, int rowParts, int columnParts)
        => UpdateDashboardTopology(
            json => _definitionComposer.SubdivideDashboardCell(json, _activeDashboardName!, selectedCell, rowParts, columnParts),
            "DashboardCellSubdivideFailed");

    public void RemoveDashboardCell(string cellName)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName) || string.IsNullOrWhiteSpace(cellName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(_definitionComposer.RemoveDashboardCell(DefinitionJson, _activeDashboardName, cellName));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", "DashboardCellRemoveFailed", exception.Message)];
        }

        NotifyChanged();
    }

    private void UpdateDashboardTopology(Func<string, string> update, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(_activeDashboardName))
        {
            return;
        }

        try
        {
            ReplaceDefinition(update(DefinitionJson));
            _activeArtifactKind = WorkspaceArtifactKind.Dashboard;
            State = RuntimeWorkspaceState.Idle;
            Diagnostics = [];
        }
        catch (Exception exception)
        {
            State = RuntimeWorkspaceState.Faulted;
            Diagnostics = [new WorkspaceDiagnostic("Error", "Designer", errorCode, exception.Message)];
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
        var positions = CollectDesignerNodePositions();
        return positions.Count > 0
            ? _definitionComposer.WriteNodePositions(DefinitionJson, positions)
            : DefinitionJson;
    }

    private IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> CollectDesignerNodePositions()
    {
        var positions = new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal);

        if (StagedNodePositions is { Count: > 0 })
        {
            foreach (var (key, position) in StagedNodePositions)
            {
                positions[key] = position;
            }
        }

        foreach (var (key, position) in LastNodePositions)
        {
            positions[key] = position;
        }

        if (GetDiagramState?.Invoke() is { } livePositions)
        {
            foreach (var (key, position) in livePositions)
            {
                positions[key] = position;
            }
        }

        LastNodePositions.Clear();
        foreach (var (key, position) in positions)
        {
            LastNodePositions[key] = position;
        }

        return positions;
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

    public void AddComponent(string componentType, (double X, double Y)? requestedPosition = null)
    {
        try
        {
            var targetWorkflowName = _activeWorkflowName ?? FlowDefinitionComposer.DefaultWorkflowName;
            var existingNodes = _definitionComposer.GetWorkflowNodes(DefinitionJson, targetWorkflowName)
                .Select(static node => node.Name)
                .ToHashSet(StringComparer.Ordinal);

            var updatedJson = _definitionComposer.AddComponent(DefinitionJson, componentType, _activeWorkflowName);
            var addedNodeName = FindAddedWorkflowNode(updatedJson, targetWorkflowName, existingNodes);

            ReplaceDefinition(updatedJson);
            _activeWorkflowName ??= WorkflowNames.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(addedNodeName))
            {
                StageAddedNodePosition(targetWorkflowName, addedNodeName, requestedPosition);
            }

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

    private string? FindAddedWorkflowNode(string updatedJson, string workflowName, IReadOnlySet<string> existingNodes)
        => _definitionComposer.GetWorkflowNodes(updatedJson, workflowName)
            .Select(static node => node.Name)
            .FirstOrDefault(nodeName => !existingNodes.Contains(nodeName));

    private void StageAddedNodePosition(string workflowName, string nodeName, (double X, double Y)? requestedPosition)
    {
        var positions = new Dictionary<string, (double X, double Y, bool Collapsed)>(StringComparer.Ordinal);
        var capturedPositions = GetDiagramState?.Invoke()
                                ?? (LastNodePositions.Count > 0 ? LastNodePositions : null);

        if (capturedPositions is not null)
        {
            foreach (var (key, capturedPosition) in capturedPositions)
            {
                positions[key] = capturedPosition;
            }
        }

        var position = requestedPosition ?? FindOpenAddedNodePosition(positions);
        var positionKey = $"{workflowName}.{nodeName}";
        positions[positionKey] = (position.X, position.Y, false);
        StagedNodePositions = positions;
        LastNodePositions[positionKey] = (position.X, position.Y, false);
    }

    private static (double X, double Y) FindOpenAddedNodePosition(
        IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> positions)
    {
        for (var column = 0; column < 8; column++)
        {
            for (var row = 0; row < AddedNodeRowsBeforeNewColumn; row++)
            {
                var x = DefaultAddedNodeX + column * AddedNodeColumnSpacing;
                var y = DefaultAddedNodeY + row * AddedNodeRowSpacing;

                if (!IsNodePositionOccupied(positions, x, y))
                {
                    return (x, y);
                }
            }
        }

        return (DefaultAddedNodeX, DefaultAddedNodeY);
    }

    private static bool IsNodePositionOccupied(
        IReadOnlyDictionary<string, (double X, double Y, bool Collapsed)> positions,
        double x,
        double y)
        => positions.Values.Any(position =>
            Math.Abs(position.X - x) < 260d &&
            Math.Abs(position.Y - y) < 140d);

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
            LastNodePositions.Clear();
            foreach (var (key, position) in StagedNodePositions)
            {
                LastNodePositions[key] = position;
            }
            ReplaceDefinitionSilent(_definitionComposer.StripDesignerSection(fileJson));
            NormalizeActiveArtifactSelection();
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

            var jsonToWrite = GetFullDefinitionJson();
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

    public async Task SaveScenarioReportAsync(
        ScenarioRunResult result,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Scenario report path cannot be empty.", nameof(path));
        }

        await SaveScenarioReportJsonAsync(
            ScenarioRunReportFormatter.ToJson(result, GetActiveTestScenario()),
            path,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task SaveScenarioReportJsonAsync(
        string json,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        await WriteScenarioReportContentAsync(json, path, cancellationToken).ConfigureAwait(false);
    }

    public static async Task SaveScenarioReportTextAsync(
        string text,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        await WriteScenarioReportContentAsync(text, path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteScenarioReportContentAsync(
        string content,
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Scenario report path cannot be empty.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
    }

    public bool SelectScenarioRunHistoryResult(ScenarioRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(_activeTestName) ||
            !string.Equals(result.Name, _activeTestName, StringComparison.Ordinal))
        {
            return false;
        }

        lock (_scenarioHistorySync)
        {
            if (!_scenarioRunHistory.Any(historyResult => ReferenceEquals(historyResult, result)))
            {
                return false;
            }

            LastScenarioRunResult = result;
        }

        NotifyChanged();
        return true;
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
            ClearRuntimeEvents(notify: false);
            _host = FlowApplicationHost.CreateDefault(CreateApplicationDefinition(DefinitionJson), _messageRepository, _runtimeClientFactory);
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
            ClearRuntimeEvents(notify: false);
            _host = FlowApplicationHost.CreateDefault(CreateApplicationDefinition(DefinitionJson), _messageRepository, _runtimeClientFactory);
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

    public async Task<ScenarioRunResult?> RunActiveTestScenarioAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_activeTestName))
        {
            return null;
        }

        var scenarioName = _activeTestName;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        IsScenarioRunning = true;
        LastScenarioRunResult = null;
        NotifyChanged();

        try
        {
            var definition = CreateApplicationDefinition(DefinitionJson);
            if (!definition.Tests.TryGetValue(scenarioName, out var scenario))
            {
                throw new InvalidOperationException($"Scenario '{scenarioName}' does not exist.");
            }

            var isolatedEvents = CreateScenarioEventSource(out var shouldCompleteScenarioEvents);
            if (shouldCompleteScenarioEvents is not null &&
                ScenarioEventSourceRequirements.RequiresAttachedEventStream(scenario))
            {
                throw new InvalidOperationException(
                    ScenarioEventSourceRequirements.DescribeMissingEventStream(scenarioName));
            }

            var services = CreateScenarioStepServices(definition, scenarioName);
            ScenarioRunResult result;
            try
            {
                result = await FlowApplicationHost.CreateDefaultScenarioRunner()
                    .RunAsync(
                        scenarioName,
                        scenario,
                        isolatedEvents,
                        services,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (shouldCompleteScenarioEvents is not null)
                {
                    shouldCompleteScenarioEvents.Complete();
                }
            }

            LastScenarioRunResult = result;
            AddScenarioRunHistory(result);
            Diagnostics =
            [
                new WorkspaceDiagnostic(
                    result.IsSuccess ? "Info" : "Error",
                    "Scenario",
                    result.Status.ToString(),
                    $"Test scenario '{result.Name}' {result.Status.ToString().ToLowerInvariant()}.")
            ];
            AppendScenarioDiagnosticsToLogs(Diagnostics, scenarioName, notify: false);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Diagnostics =
            [
                new WorkspaceDiagnostic("Error", "Scenario", "RunFailed", exception.Message)
            ];
            AppendScenarioDiagnosticsToLogs(Diagnostics, scenarioName, notify: false);
            return null;
        }
        finally
        {
            IsScenarioRunning = false;
            _gate.Release();
            NotifyChanged();
        }
    }

    private ISourceBlock<FlowEvent> CreateScenarioEventSource(out IDataflowBlock? ownedEventSource)
    {
        if (_host?.Runtime is not null && State == RuntimeWorkspaceState.Running)
        {
            ownedEventSource = null;
            return _host.Runtime.Events;
        }

        var emptyEvents = new BroadcastBlock<FlowEvent>(static flowEvent => flowEvent);
        ownedEventSource = emptyEvents;
        return emptyEvents;
    }

    private ScenarioStepServices CreateScenarioStepServices(ApplicationDefinition definition, string scenarioName)
    {
        var scenarioEventObserver = new WorkspaceScenarioEventObserver(
            flowEvent => AppendLog(ToScenarioWorkspaceLogEntry(flowEvent, scenarioName)));

        if (_host?.Runtime is { } runtime && State == RuntimeWorkspaceState.Running)
        {
            var runtimeClientFactory = new RuntimeMqttScenarioClientFactory(runtime, _runtimeClientFactory);
            return ScenarioStepServices.Empty
                .Add<IMqttScenarioClientFactory>(runtimeClientFactory)
                .Add<IScenarioEventObserver>(scenarioEventObserver);
        }

        var definitionClientFactory = new ApplicationDefinitionMqttScenarioClientFactory(definition, _runtimeClientFactory);
        return ScenarioStepServices.Empty
            .Add<IMqttScenarioClientFactory>(definitionClientFactory)
            .Add<IScenarioEventObserver>(scenarioEventObserver);
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

    private static ApplicationDefinition CreateApplicationDefinition(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("FluxMq", out var fluxMq) &&
            fluxMq.TryGetProperty("FlowApplication", out var flowApplication))
        {
            root = flowApplication;
        }
        else if (root.TryGetProperty("FlowApplication", out var directFlowApplication))
        {
            root = directFlowApplication;
        }

        return root.Deserialize<ApplicationDefinition>(ApplicationDefinitionJson.CreateSerializerOptions())
            ?? throw new InvalidOperationException("Flow application definition is empty.");
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

        AttachRuntimeEvents(_host.Runtime);

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

    private void AttachRuntimeEvents(ApplicationRuntime runtime)
    {
        var eventSourceAddresses = runtime.Nodes
            .Where(node => node.Node is IFlowEventSource)
            .ToDictionary(node => node.Node.Id, node => node.Address);

        var target = new ActionBlock<FlowEvent>(
            flowEvent => StoreRuntimeEvent(flowEvent, eventSourceAddresses),
            new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = true,
                MaxDegreeOfParallelism = 1
            });

        _runtimeProjectionTargets.Add(target);
        _runtimeProjectionLinks.Add(runtime.Events.LinkTo(target, new DataflowLinkOptions { PropagateCompletion = true }));
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

    private void StoreRuntimeEvent(
        FlowEvent flowEvent,
        IReadOnlyDictionary<FlowNodeId, NodeAddress> eventSourceAddresses)
    {
        NodeAddress? address = null;
        if (flowEvent.SourceNodeId is { } sourceNodeId)
        {
            eventSourceAddresses.TryGetValue(sourceNodeId, out address);
        }

        StoreWorkspaceEvent(flowEvent, address);
    }

    private void StoreWorkspaceEvent(FlowEvent flowEvent, NodeAddress? address)
    {
        lock (_runtimeEventSync)
        {
            _runtimeEvents.Add(flowEvent);
            if (_runtimeEvents.Count > MaxRuntimeEvents)
            {
                _runtimeEvents.RemoveRange(0, _runtimeEvents.Count - MaxRuntimeEvents);
            }

            RuntimeEvents = _runtimeEvents.ToArray();
        }

        AppendLogs([ToWorkspaceLogEntry(flowEvent, address)], notify: false);
        NotifyRuntimeProjectionChanged();
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

    private void ClearRuntimeEvents(bool notify)
    {
        lock (_runtimeEventSync)
        {
            _runtimeEvents.Clear();
            RuntimeEvents = [];
        }

        if (notify)
        {
            NotifyChanged();
        }
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
            NotifyChanged();
        }
        catch
        {
            // Projection notifications are best-effort UI refresh signals.
            // Keep the background throttle healthy even if a subscriber fails.
        }
        finally
        {
            Interlocked.Exchange(ref _runtimeProjectionNotificationQueued, 0);
        }
    }

    private void AppendDiagnosticsToLogs(IEnumerable<WorkspaceDiagnostic> diagnostics, bool notify = true)
        => AppendLogs(diagnostics.Select(diagnostic => WorkspaceLogEntry.FromDiagnostic(diagnostic)), notify);

    private void AppendScenarioDiagnosticsToLogs(
        IEnumerable<WorkspaceDiagnostic> diagnostics,
        string scenarioName,
        bool notify = true)
        => AppendLogs(diagnostics.Select(diagnostic => WorkspaceLogEntry.FromDiagnostic(
            diagnostic,
            WorkspaceLogScopes.TestRunner,
            WorkspaceLogArtifactKinds.Test,
            scenarioName)), notify);

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
            BuildRuntimeLogContext(entry),
            WorkspaceLogScopes.App,
            WorkspaceLogArtifactKinds.Pipeline,
            address.Scope);

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
            BuildFlowErrorContext(error),
            WorkspaceLogScopes.App,
            WorkspaceLogArtifactKinds.Pipeline,
            address.Scope);

    private static WorkspaceLogEntry ToWorkspaceLogEntry(FlowEvent flowEvent, NodeAddress? address)
        => new(
            flowEvent.Timestamp,
            "Info",
            string.IsNullOrWhiteSpace(flowEvent.Source) ? "RuntimeEvent" : flowEvent.Source,
            flowEvent.Type,
            BuildRuntimeEventMessage(flowEvent),
            address?.Scope,
            address?.Node.Value,
            null,
            BuildRuntimeEventContext(flowEvent),
            WorkspaceLogScopes.App,
            address is null ? null : WorkspaceLogArtifactKinds.Pipeline,
            address?.Scope);

    private static WorkspaceLogEntry ToScenarioWorkspaceLogEntry(FlowEvent flowEvent, string scenarioName)
        => new(
            flowEvent.Timestamp,
            "Info",
            string.IsNullOrWhiteSpace(flowEvent.Source) ? "ScenarioEvent" : flowEvent.Source,
            flowEvent.Type,
            BuildScenarioEventMessage(flowEvent),
            null,
            null,
            null,
            BuildRuntimeEventContext(flowEvent),
            WorkspaceLogScopes.TestRunner,
            WorkspaceLogArtifactKinds.Test,
            scenarioName);

    private static string BuildRuntimeEventMessage(FlowEvent flowEvent)
    {
        var target = !string.IsNullOrWhiteSpace(flowEvent.Topic)
            ? flowEvent.Topic
            : flowEvent.Subject;

        return string.IsNullOrWhiteSpace(target)
            ? $"Observed runtime event '{flowEvent.Type}'."
            : $"Observed runtime event '{flowEvent.Type}' for '{target}'.";
    }

    private static string BuildScenarioEventMessage(FlowEvent flowEvent)
    {
        var target = !string.IsNullOrWhiteSpace(flowEvent.Topic)
            ? flowEvent.Topic
            : flowEvent.Subject;

        return string.IsNullOrWhiteSpace(target)
            ? $"Observed scenario event '{flowEvent.Type}'."
            : $"Observed scenario event '{flowEvent.Type}' for '{target}'.";
    }

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

    private static string? BuildRuntimeEventContext(FlowEvent flowEvent)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(flowEvent.Topic))
        {
            parts.Add($"topic={flowEvent.Topic}");
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.Subject) &&
            !string.Equals(flowEvent.Subject, flowEvent.Topic, StringComparison.Ordinal))
        {
            parts.Add($"subject={flowEvent.Subject}");
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.Status))
        {
            parts.Add($"status={flowEvent.Status}");
        }

        if (flowEvent.PayloadBytes is not null)
        {
            parts.Add($"payloadBytes={flowEvent.PayloadBytes}");
        }

        foreach (var attribute in flowEvent.Attributes.OrderBy(attribute => attribute.Key, StringComparer.Ordinal))
        {
            parts.Add($"{attribute.Key}={attribute.Value}");
        }

        if (!string.IsNullOrWhiteSpace(flowEvent.PayloadPreview))
        {
            parts.Add($"payload={flowEvent.PayloadPreview}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? CreatePayloadPreview(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var text = StrictUtf8.GetString(payload);
            return text.Length <= MaxFlowEventPayloadPreviewChars
                ? text
                : text[..MaxFlowEventPayloadPreviewChars];
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
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
        TryNormalizeActiveArtifactSelection();
        ClearRuntimeMetricsSnapshots(notify: false);
        ClearRuntimePayloadInspections(notify: false);
        ClearRuntimeTriggerActivitySnapshots(notify: false);
        ClearRuntimeEvents(notify: false);
        ClearScenarioRunHistory();
    }

    private void ReplaceDefinitionSilent(string json)
    {
        if (string.Equals(DefinitionJson, json, StringComparison.Ordinal))
            return;

        DefinitionJson = json;
        DefinitionRevision++;
        TryNormalizeActiveArtifactSelection();
        ClearRuntimeMetricsSnapshots(notify: false);
        ClearRuntimePayloadInspections(notify: false);
        ClearRuntimeTriggerActivitySnapshots(notify: false);
        ClearRuntimeEvents(notify: false);
        ClearScenarioRunHistory();
    }

    private void AddScenarioRunHistory(ScenarioRunResult result)
    {
        lock (_scenarioHistorySync)
        {
            _scenarioRunHistory.Insert(0, result);
            if (_scenarioRunHistory.Count > MaxScenarioRunHistory)
            {
                _scenarioRunHistory.RemoveRange(MaxScenarioRunHistory, _scenarioRunHistory.Count - MaxScenarioRunHistory);
            }

            ScenarioRunHistory = _scenarioRunHistory.ToArray();
        }
    }

    private ScenarioRunResult? LatestScenarioRunForTest(string? testName)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return null;
        }

        lock (_scenarioHistorySync)
        {
            return _scenarioRunHistory.FirstOrDefault(result =>
                string.Equals(result.Name, testName, StringComparison.Ordinal));
        }
    }

    private void ClearScenarioRunHistory()
    {
        lock (_scenarioHistorySync)
        {
            _scenarioRunHistory.Clear();
            ScenarioRunHistory = [];
            LastScenarioRunResult = null;
        }
    }

    private void NormalizeActiveArtifactSelection()
    {
        var workflows = WorkflowNames;
        if (_activeWorkflowName is null || !workflows.Contains(_activeWorkflowName, StringComparer.Ordinal))
        {
            _activeWorkflowName = workflows.FirstOrDefault();
        }

        var dashboards = DashboardNames;
        if (_activeDashboardName is null || !dashboards.Contains(_activeDashboardName, StringComparer.Ordinal))
        {
            _activeDashboardName = dashboards.FirstOrDefault();
            ActiveDashboardCellName = null;
        }

        var tests = TestNames;
        if (_activeTestName is null || !tests.Contains(_activeTestName, StringComparer.Ordinal))
        {
            _activeTestName = tests.FirstOrDefault();
        }

        if (_activeArtifactKind == WorkspaceArtifactKind.Logs)
        {
            ActiveDashboardCellName = null;
            return;
        }

        if (_activeArtifactKind == WorkspaceArtifactKind.Pipeline && _activeWorkflowName is not null ||
            _activeArtifactKind == WorkspaceArtifactKind.Dashboard && _activeDashboardName is not null ||
            _activeArtifactKind == WorkspaceArtifactKind.Test && _activeTestName is not null)
        {
            return;
        }

        _activeArtifactKind = _activeWorkflowName is not null
            ? WorkspaceArtifactKind.Pipeline
            : _activeDashboardName is not null
                ? WorkspaceArtifactKind.Dashboard
                : _activeTestName is not null
                    ? WorkspaceArtifactKind.Test
                    : WorkspaceArtifactKind.Pipeline;

        if (_activeArtifactKind != WorkspaceArtifactKind.Dashboard)
        {
            ActiveDashboardCellName = null;
        }
    }

    private void TryNormalizeActiveArtifactSelection()
    {
        try
        {
            NormalizeActiveArtifactSelection();
        }
        catch (InvalidOperationException exception) when (exception.InnerException is JsonException)
        {
            // Invalid in-progress JSON is reported by validation; keep the current selection meanwhile.
        }
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static string MetricsKey(string? workflowName, string nodeName)
        => RuntimeProjectionKey(workflowName, nodeName);

    private static string RuntimeProjectionKey(string? workflowName, string nodeName)
        => $"{workflowName ?? string.Empty}/{nodeName}";

    private bool MatchesDashboardEventWidget(DashboardWidgetSnapshot widget, FlowEvent flowEvent)
        => _dashboardEventFilters.Matches(widget, flowEvent);

    private sealed class WorkspaceScenarioEventObserver(Action<FlowEvent> observe) : IScenarioEventObserver
    {
        public void Observe(FlowEvent flowEvent)
            => observe(flowEvent);
    }
}
