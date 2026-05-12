using FluxMq.Pipeline.Runtime;
using Microsoft.Extensions.Configuration;

namespace FluxMq.App;

public sealed class FlowApplicationHost : IAsyncDisposable, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationRuntimeBuilder _runtimeBuilder;
    private readonly FlowApplicationConfigurationLoader _configurationLoader;
    private readonly string _sectionName;
    private ApplicationRuntime? _runtime;
    private bool _disposed;

    public FlowApplicationHost(
        IConfiguration configuration,
        ApplicationRuntimeBuilder runtimeBuilder,
        FlowApplicationConfigurationLoader? configurationLoader = null,
        string sectionName = FlowApplicationConfigurationLoader.DefaultSectionName)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _runtimeBuilder = runtimeBuilder ?? throw new ArgumentNullException(nameof(runtimeBuilder));
        _configurationLoader = configurationLoader ?? new FlowApplicationConfigurationLoader();
        _sectionName = sectionName;
    }

    public FlowApplicationHostState State { get; private set; } = FlowApplicationHostState.Empty;
    public ApplicationRuntime? Runtime => _runtime;
    public FlowApplicationHostBuildResult? LastBuildResult { get; private set; }
    public Exception? LastException { get; private set; }

    public static FlowApplicationHost CreateDefault(IConfiguration configuration)
    {
        var factories = new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories();

        return new FlowApplicationHost(configuration, new ApplicationRuntimeBuilder(factories));
    }

    public FlowApplicationHostBuildResult Build()
    {
        ThrowIfDisposed();

        DisposeRuntime();

        try
        {
            LastException = null;
            var definition = _configurationLoader.Load(_configuration, _sectionName);
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
        if (result.IsSuccess)
        {
            try
            {
                await _runtime!.StartAsync(cancellationToken).ConfigureAwait(false);
                State = FlowApplicationHostState.Running;
            }
            catch (OperationCanceledException)
            {
                throw;
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
        }

        return result;
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
