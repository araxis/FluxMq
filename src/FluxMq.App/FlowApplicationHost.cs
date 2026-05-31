using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.App.Scenarios;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using FluxMq.Pipeline.Scenarios;
using Microsoft.Extensions.Configuration;

namespace FluxMq.App;

public sealed class FlowApplicationHost(
    IConfiguration? configuration,
    ApplicationRuntimeBuilder runtimeBuilder,
    FlowApplicationConfigurationLoader? configurationLoader = null,
    string sectionName = FlowApplicationConfigurationLoader.DefaultSectionName,
    ScenarioRunner? scenarioRunner = null,
    Func<MqttConnectionProfile, IMqttBrokerClient>? scenarioClientFactory = null,
    ApplicationDefinition? applicationDefinition = null)
    : IAsyncDisposable, IDisposable
{
    private readonly IConfiguration? _configuration = configuration;
    private readonly ApplicationRuntimeBuilder _runtimeBuilder = runtimeBuilder ?? throw new ArgumentNullException(nameof(runtimeBuilder));
    private readonly FlowApplicationConfigurationLoader _configurationLoader = configurationLoader ?? new FlowApplicationConfigurationLoader();
    private readonly ScenarioRunner _scenarioRunner = scenarioRunner ?? CreateDefaultScenarioRunner();
    private readonly Func<MqttConnectionProfile, IMqttBrokerClient> _scenarioClientFactory =
        scenarioClientFactory ?? (static profile => new MqttBrokerClient(profile));
    private readonly ApplicationDefinition? _applicationDefinition = applicationDefinition;
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
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null)
    {
        clientFactory ??= static profile => new MqttBrokerClient(profile);
        var factories = new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(clientFactory, messageRepository);

        return new FlowApplicationHost(
            configuration,
            new ApplicationRuntimeBuilder(factories),
            scenarioClientFactory: clientFactory);
    }

    public static FlowApplicationHost CreateDefault(
        ApplicationDefinition definition,
        IMessageRepository? messageRepository = null,
        Func<MqttConnectionProfile, IMqttBrokerClient>? clientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        clientFactory ??= static profile => new MqttBrokerClient(profile);
        var factories = new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(clientFactory, messageRepository);

        return new FlowApplicationHost(
            null,
            new ApplicationRuntimeBuilder(factories),
            scenarioClientFactory: clientFactory,
            applicationDefinition: definition);
    }

    public static ScenarioRunner CreateDefaultScenarioRunner()
        => new(CreateDefaultScenarioStepRunnerRegistry());

    public static ScenarioStepRunnerRegistry CreateDefaultScenarioStepRunnerRegistry()
        => new ScenarioStepRunnerRegistry()
            .Register(new ExpectEventScenarioStepRunner())
            .Register(new WhenEventScenarioStepRunner())
            .Register(new MqttPublishScenarioStepRunner())
            .Register(new MqttTriggerScenarioStepRunner());

    public FlowApplicationHostBuildResult Build()
    {
        ThrowIfDisposed();

        DisposeRuntime();

        try
        {
            LastException = null;
            var definition = _applicationDefinition ?? LoadDefinitionFromConfiguration();
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

        var definition = _definition ?? _applicationDefinition ?? LoadDefinitionFromConfiguration();
        if (!definition.Tests.TryGetValue(scenarioName, out _))
        {
            throw new InvalidOperationException($"Scenario '{scenarioName}' does not exist.");
        }

        if (_runtime is null || State != FlowApplicationHostState.Running)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenarioName}' cannot run against this host because the app runtime is not running. Start the app explicitly or run the scenario with an external test runner.");
        }

        var scenario = definition.Tests[scenarioName];
        return await _scenarioRunner
            .RunAsync(
                scenarioName,
                scenario,
                _runtime.Events,
                CreateScenarioStepServices(_runtime),
                cancellationToken)
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

    private ApplicationDefinition LoadDefinitionFromConfiguration()
    {
        if (_configuration is null)
        {
            throw new InvalidOperationException("A flow application configuration was not provided.");
        }

        return _configurationLoader.Load(_configuration, sectionName);
    }

    private ScenarioStepServices CreateScenarioStepServices(ApplicationRuntime runtime)
    {
        var mqttClientFactory = new RuntimeMqttScenarioClientFactory(runtime, _scenarioClientFactory);
        return ScenarioStepServices.Empty
            .Add<IMqttScenarioClientFactory>(mqttClientFactory);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
