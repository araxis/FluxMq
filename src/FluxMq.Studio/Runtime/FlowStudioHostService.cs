using FluxMq.App;
using FluxMq.Pipeline.Runtime;
using Microsoft.Extensions.Configuration;

namespace FluxMq.Studio.Runtime;

public sealed class FlowStudioHostService : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FlowApplicationHost? _host;

    public FlowApplicationHostState State { get; private set; } = FlowApplicationHostState.Empty;
    public FlowStudioResult? LastResult { get; private set; }

    public async Task<FlowStudioResult> ValidateAsync(string json, string sectionName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
            _host = CreateHost(json, sectionName);
            LastResult = ToResult(_host.Build(), _host.State, "validate");
            State = _host.State;
            return LastResult;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<FlowStudioResult> RunAsync(string json, string sectionName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
            _host = CreateHost(json, sectionName);
            var result = await _host.StartAsync(cancellationToken).ConfigureAwait(false);
            LastResult = ToResult(result, _host.State, "run");
            State = _host.State;
            return LastResult;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<FlowStudioResult> StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_host is null)
            {
                State = FlowApplicationHostState.Stopped;
                LastResult = new FlowStudioResult(
                    true,
                    0,
                    0,
                    FlowApplicationHostState.Stopped,
                    "stop",
                    [new FlowStudioDiagnostic("host", "Stopped", "Application is already stopped.")]);

                return LastResult;
            }

            await _host.StopAsync(cancellationToken).ConfigureAwait(false);
            State = _host.State;
            LastResult = new FlowStudioResult(
                true,
                _host.Runtime?.Workflows.Count ?? 0,
                _host.Runtime?.Resources.Count ?? 0,
                _host.State,
                "stop",
                []);
            return LastResult;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            State = FlowApplicationHostState.Faulted;
            LastResult = new FlowStudioResult(
                false,
                0,
                0,
                State,
                "stop",
                [new FlowStudioDiagnostic("host", "StopFailed", exception.Message)]);
            return LastResult;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeHostAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private static FlowApplicationHost CreateHost(string json, string sectionName)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        return new FlowApplicationHost(
            configuration,
            new FlowApplicationRuntimeBuilder(
                new FlowRuntimeNodeFactoryRegistry()
                    .RegisterPipelineComponentFactories()),
            sectionName: sectionName);
    }

    private static FlowStudioResult ToResult(FlowApplicationHostBuildResult result, FlowApplicationHostState state, string operation)
    {
        var diagnostics = new List<FlowStudioDiagnostic>();

        foreach (var error in result.Errors)
        {
            diagnostics.Add(new FlowStudioDiagnostic("host", error.Code.ToString(), error.Message));
        }

        if (result.RuntimeBuild is not null)
        {
            foreach (var error in result.RuntimeBuild.Validation.Errors)
            {
                diagnostics.Add(new FlowStudioDiagnostic("definition", error.Code.ToString(), error.Message));
            }

            foreach (var error in result.RuntimeBuild.Errors)
            {
                diagnostics.Add(new FlowStudioDiagnostic(
                    "runtime",
                    error.Code.ToString(),
                    error.Message,
                    error.WorkflowName,
                    error.NodeName?.Value,
                    error.PortName?.Value));
            }
        }

        return new FlowStudioResult(
            result.IsSuccess,
            result.RuntimeBuild?.Runtime?.Workflows.Count ?? 0,
            result.RuntimeBuild?.Runtime?.Resources.Count ?? 0,
            state,
            operation,
            diagnostics);
    }

    private async Task DisposeHostAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync().ConfigureAwait(false);
            _host = null;
        }
    }
}

public sealed record FlowStudioResult(
    bool IsSuccess,
    int WorkflowCount,
    int ResourceCount,
    FlowApplicationHostState State,
    string Operation,
    IReadOnlyList<FlowStudioDiagnostic> Diagnostics);

public sealed record FlowStudioDiagnostic(
    string Scope,
    string Code,
    string Message,
    string? Workflow = null,
    string? Node = null,
    string? Port = null);
