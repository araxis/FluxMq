using FluxMq.Core.Models;
using FluxMq.Core.Session;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using FluxMq.Pipeline.Scenarios;
using Microsoft.Extensions.Configuration;

namespace FluxMq.App;

public sealed class FlowApplicationHost(
    IConfiguration configuration,
    ApplicationRuntimeBuilder runtimeBuilder,
    FlowApplicationConfigurationLoader? configurationLoader = null,
    string sectionName = FlowApplicationConfigurationLoader.DefaultSectionName,
    ScenarioRunner? scenarioRunner = null)
    : IAsyncDisposable, IDisposable
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ApplicationRuntimeBuilder _runtimeBuilder = runtimeBuilder ?? throw new ArgumentNullException(nameof(runtimeBuilder));
    private readonly FlowApplicationConfigurationLoader _configurationLoader = configurationLoader ?? new FlowApplicationConfigurationLoader();
    private readonly ScenarioRunner _scenarioRunner = scenarioRunner ?? new ScenarioRunner();
    private ApplicationDefinition? _definition;
    private ApplicationRuntime? _runtime;
    private bool _disposed;

    public FlowApplicationHostState State { get; private set; } = FlowApplicationHostState.Empty;
    public ApplicationDefinition? Definition => _definition;
    public ApplicationRuntime? Runtime => _runtime;
    public FlowApplicationHostBuildResult? LastBuildResult { get; private set; }
    public Exception? LastException { get; private set; }

    public static FlowApplicationHost CreateDefault(
        IConfiguration configuration,
        IMessageRepository? messageRepository = null,
        Func<MqttConnectionProfile, IMqttSession>? sessionFactory = null)
    {
        var factories = new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(sessionFactory, messageRepository);

        return new FlowApplicationHost(configuration, new ApplicationRuntimeBuilder(factories));
    }

    public FlowApplicationHostBuildResult Build()
    {
        ThrowIfDisposed();

        DisposeRuntime();

        try
        {
            LastException = null;
            var definition = _configurationLoader.Load(_configuration, sectionName);
            _definition = definition;
            var runtimeBuild = _runtimeBuilder.Build(definition);

            if (runtimeBuild.IsSuccess)
            {
                _runtime = runtimeBuild.Runtime;
                State = FlowApplicationHostState.Built;
            }
            else
            {
                State = FlowApplicationHostState.Empty;
            }

            LastBuildResult = FlowApplicationHostBuildResult.FromRuntime(runtimeBuild);
            return LastBuildResult;
        }
        catch (FlowApplicationConfigurationException exception)
        {
            State = FlowApplicationHostState.Empty;
            _definition = null;
            LastException = exception;
            LastBuildResult = FlowApplicationHostBuildResult.FromHostError(
                new FlowApplicationHostBuildError(
                    FlowApplicationHostBuildErrorCode.InvalidConfiguration,
                    exception.Message,
                    exception));

            return LastBuildResult;
        }
    }

    public FlowApplicationHostBuildResult Start()
        => StartAsync().GetAwaiter().GetResult();

    public async Task<FlowApplicationHostBuildResult> StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var result = Build();
        if (!result.IsSuccess) return result;

        return await StartBuiltAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<FlowApplicationHostBuildResult> StartBuiltAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_runtime is null || LastBuildResult is null || !LastBuildResult.IsSuccess)
        {
            var buildResult = Build();
            if (!buildResult.IsSuccess || _runtime is null)
            {
                return buildResult;
            }

            return await StartBuiltAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = LastBuildResult;

        try
        {
            await _runtime!.StartAsync(cancellationToken).ConfigureAwait(false);
            State = FlowApplicationHostState.Running;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ApplicationRuntimeNodeStartException exception)
        {
            State = FlowApplicationHostState.Faulted;
            LastException = exception.InnerException ?? exception;
            LastBuildResult = FlowApplicationHostBuildResult.FromHostError(
                new FlowApplicationHostBuildError(
                    FlowApplicationHostBuildErrorCode.StartFailed,
                    exception.Message,
                    exception,
                    exception.NodeAddress.Scope == WellKnownScopes.Resources ? null : exception.NodeAddress.Scope,
                    exception.NodeAddress.Node.Value));

            return LastBuildResult;
        }
        catch (Exception exception)
        {
            State = FlowApplicationHostState.Faulted;
            LastException = exception;
            LastBuildResult = FlowApplicationHostBuildResult.FromHostError(
                new FlowApplicationHostBuildError(
                    FlowApplicationHostBuildErrorCode.StartFailed,
                    $"Flow application start failed: {exception.Message}",
                    exception));

            return LastBuildResult;
        }

        return result;
    }

    public async Task<ScenarioRunResult> RunScenarioAsync(
        string scenarioName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            throw new ArgumentException("Scenario name cannot be empty.", nameof(scenarioName));
        }

        var definition = _definition ?? _configurationLoader.Load(_configuration, sectionName);
        if (!definition.Tests.TryGetValue(scenarioName, out _))
        {
            throw new InvalidOperationException($"Scenario '{scenarioName}' does not exist.");
        }

        if (_runtime is null || State != FlowApplicationHostState.Running)
        {
            var startResult = await StartAsync(cancellationToken).ConfigureAwait(false);
            if (!startResult.IsSuccess || _runtime is null || _definition is null)
            {
                throw new InvalidOperationException($"Scenario '{scenarioName}' cannot run because the app runtime did not start.");
            }

            definition = _definition;
        }

        var scenario = definition.Tests[scenarioName];
        return await _scenarioRunner
            .RunAsync(scenarioName, scenario, _runtime.Events, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_runtime is null)
        {
            State = FlowApplicationHostState.Stopped;
            return;
        }

        try
        {
            _runtime.Complete();
            await _runtime.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            State = FlowApplicationHostState.Stopped;
            LastException = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            State = FlowApplicationHostState.Faulted;
            LastException = exception;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeRuntime();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisposeRuntimeAsync().ConfigureAwait(false);
    }

    private void DisposeRuntime()
    {
        _runtime?.Dispose();
        _runtime = null;
    }

    private async ValueTask DisposeRuntimeAsync()
    {
        if (_runtime is not null)
        {
            await _runtime.DisposeAsync().ConfigureAwait(false);
            _runtime = null;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
