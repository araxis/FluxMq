using FluxMq.App;
using FluxMq.Core.Models;
using FluxMq.UI.Models;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace FluxMq.UI.Services;

public sealed class FlowWorkspaceService : IAsyncDisposable
{
    private readonly FlowDefinitionFactory _definitionFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FlowApplicationHost? _host;

    public FlowWorkspaceService(FlowDefinitionFactory definitionFactory)
    {
        _definitionFactory = definitionFactory;
        DefinitionJson = _definitionFactory.CreateInspectPayloadsDefinition(DefaultProfile(), "#");
        CurrentFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FluxMQ",
            "inspect-payloads.json");
    }

    public string DefinitionJson { get; private set; }
    public string CurrentFilePath { get; private set; }
    public RuntimeWorkspaceState State { get; private set; } = RuntimeWorkspaceState.Idle;
    public IReadOnlyList<WorkspaceDiagnostic> Diagnostics { get; private set; } = [];

    public event EventHandler? Changed;

    public void SetFilePath(string path)
    {
        CurrentFilePath = path;
        NotifyChanged();
    }

    public void SetDefinitionJson(string json)
    {
        DefinitionJson = json;
        State = RuntimeWorkspaceState.Idle;
        Diagnostics = [];
        NotifyChanged();
    }

    public void ApplyLocalBroker(MqttConnectionProfile profile, string subscription)
    {
        try
        {
            DefinitionJson = _definitionFactory.UpsertComponent(DefinitionJson, "mqtt.message-source", profile, subscription);
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

    public void AddComponent(string componentType, MqttConnectionProfile profile, string subscription)
    {
        try
        {
            DefinitionJson = _definitionFactory.UpsertComponent(DefinitionJson, componentType, profile, subscription);
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
            DefinitionJson = await File.ReadAllTextAsync(CurrentFilePath, cancellationToken).ConfigureAwait(false);
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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(CurrentFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(CurrentFilePath, DefinitionJson, cancellationToken).ConfigureAwait(false);
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

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson));
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
            _host = FlowApplicationHost.CreateDefault(CreateConfiguration(DefinitionJson));
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
            {
                await _host.StopAsync(cancellationToken).ConfigureAwait(false);
            }

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

    private static MqttConnectionProfile DefaultProfile()
        => new()
        {
            Name = "local-broker",
            Host = "localhost",
            Port = 1883
        };

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
            error.Message)));

        if (result.RuntimeBuild is not null)
        {
            diagnostics.AddRange(result.RuntimeBuild.Validation.Errors.Select(error => new WorkspaceDiagnostic(
                "Error",
                "Definition",
                error.Code.ToString(),
                error.Message)));

            diagnostics.AddRange(result.RuntimeBuild.Errors.Select(error => new WorkspaceDiagnostic(
                "Error",
                "RuntimeBuild",
                error.Code.ToString(),
                error.Message)));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(new WorkspaceDiagnostic("Info", "Runtime", "Ready", "Flow application is valid."));
        }

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

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
