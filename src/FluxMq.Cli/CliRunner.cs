using FluxMq.App;
using FluxMq.Cli.Commands;
using FluxMq.Pipeline.Runtime;
using FluxMq.Pipeline.Scenarios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace FluxMq.Cli;

public sealed class CliRunner
{
    private readonly ICliOutput _output;
    private readonly ICliOutput _error;

    public CliRunner(ICliOutput output, ICliOutput error)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public int Run(string[] args)
        => RunAsync(args).GetAwaiter().GetResult();

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        services.AddSingleton(this);

        using var registrar = new CliTypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("fluxmq");
            config.AddCommand<ValidateCliCommand>(CliOptions.ValidateCommand);
            config.AddCommand<RunCliCommand>(CliOptions.RunCommand);
            config.AddCommand<ScenarioCliCommand>(CliOptions.ScenarioCommand);
        });

        try
        {
            return await app.RunAsync(args, cancellationToken).ConfigureAwait(false);
        }
        catch (CommandParseException)
        {
            return (int)CliExitCode.UsageError;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _error.WriteLine($"Unexpected failure: {exception.Message}");
            return (int)CliExitCode.UnexpectedError;
        }
    }

    internal int Validate(CliOptions options)
    {
        if (!TryBuildHost(options, out var host, out var exitCode))
        {
            return exitCode;
        }

        using (var flowHost = host ?? throw new InvalidOperationException("Host was not created."))
        {
            var result = flowHost.Build();
            var commandResult = CreateValidateResult(result);
            var output = options.OutputFormat == CliOutputFormat.Json ? _output : commandResult.IsValid ? _output : _error;
            ValidateFlowResultRenderer.Write(commandResult, options.OutputFormat, output);

            return commandResult.IsValid ? (int)CliExitCode.Success : (int)CliExitCode.ValidationError;
        }
    }

    internal async Task<int> RunFlow(CliOptions options, CancellationToken cancellationToken)
    {
        if (!TryBuildHost(options, out var host, out var exitCode))
        {
            return exitCode;
        }

        await using (var flowHost = host ?? throw new InvalidOperationException("Host was not created."))
        {
            var startResult = await flowHost.StartAsync(cancellationToken).ConfigureAwait(false);
            var validationResult = CreateValidateResult(startResult);
            if (!validationResult.IsValid)
            {
                if (options.OutputFormat == CliOutputFormat.Json)
                {
                    RunFlowResultRenderer.Write(
                        new RunFlowCommandResult(
                            false,
                            validationResult.WorkflowCount,
                            validationResult.ResourceCount,
                            "validation failed",
                            flowHost.State.ToString(),
                            validationResult.Diagnostics),
                        options.OutputFormat,
                        _output);
                }
                else
                {
                    ValidateFlowResultRenderer.Write(validationResult, options.OutputFormat, _error);
                }

                return (int)CliExitCode.ValidationError;
            }

            if (options.OutputFormat == CliOutputFormat.Text)
            {
                _output.WriteLine($"Flow application is running. Workflows: {validationResult.WorkflowCount}. Resources: {validationResult.ResourceCount}.");
            }

            var exitReason = await WaitForRunExit(options.RunDuration, cancellationToken).ConfigureAwait(false);
            await flowHost.StopAsync(CancellationToken.None).ConfigureAwait(false);

            var runResult = new RunFlowCommandResult(
                true,
                validationResult.WorkflowCount,
                validationResult.ResourceCount,
                exitReason,
                flowHost.State.ToString(),
                validationResult.Diagnostics);

            RunFlowResultRenderer.Write(runResult, options.OutputFormat, _output);
            return (int)CliExitCode.Success;
        }
    }

    internal async Task<int> RunScenario(CliOptions options, CancellationToken cancellationToken)
    {
        if (!TryBuildHost(options, out var host, out var exitCode))
        {
            return exitCode;
        }

        await using (var flowHost = host ?? throw new InvalidOperationException("Host was not created."))
        {
            try
            {
                var result = await flowHost
                    .RunScenarioAsync(options.ScenarioName ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false);
                var commandResult = CreateScenarioResult(result);
                var output = options.OutputFormat == CliOutputFormat.Json || commandResult.IsSuccess ? _output : _error;

                ScenarioRunResultRenderer.Write(commandResult, options.OutputFormat, output);
                return commandResult.IsSuccess ? (int)CliExitCode.Success : (int)CliExitCode.ScenarioFailed;
            }
            catch (InvalidOperationException exception)
            {
                if (options.OutputFormat == CliOutputFormat.Json)
                {
                    ScenarioRunResultRenderer.Write(
                        CreateScenarioFailureResult(options.ScenarioName ?? string.Empty, exception.Message),
                        options.OutputFormat,
                        _output);
                }
                else
                {
                    _error.WriteLine(exception.Message);
                }

                return (int)CliExitCode.ValidationError;
            }
        }
    }

    private bool TryBuildHost(CliOptions options, out FlowApplicationHost? host, out int exitCode)
    {
        var configurationPath = Path.GetFullPath(options.ConfigurationPath!);
        if (!File.Exists(configurationPath))
        {
            _error.WriteLine($"Configuration file was not found: {configurationPath}");
            host = null;
            exitCode = (int)CliExitCode.UsageError;
            return false;
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configurationPath, optional: false, reloadOnChange: false)
            .Build();

        host = new FlowApplicationHost(
            configuration,
            new ApplicationRuntimeBuilder(
                new RuntimeNodeFactoryRegistry()
                    .RegisterPipelineComponentFactories()),
            sectionName: options.SectionName);
        exitCode = (int)CliExitCode.Success;
        return true;
    }

    private static ValidateFlowCommandResult CreateValidateResult(FlowApplicationHostBuildResult result)
    {
        var diagnostics = new List<ValidateFlowDiagnostic>();
        foreach (var error in result.Errors)
        {
            diagnostics.Add(new ValidateFlowDiagnostic("host", error.Code.ToString(), error.Message));
        }

        if (result.RuntimeBuild is not null)
        {
            foreach (var error in result.RuntimeBuild.Validation.Errors)
            {
                diagnostics.Add(new ValidateFlowDiagnostic("definition", error.Code.ToString(), error.Message));
            }

            foreach (var error in result.RuntimeBuild.Errors)
            {
                diagnostics.Add(new ValidateFlowDiagnostic(
                    "runtime",
                    error.Code.ToString(),
                    error.Message,
                    error.WorkflowName,
                    error.NodeName?.Value,
                    error.PortName?.Value));
            }
        }

        var runtime = result.RuntimeBuild?.Runtime;

        return new ValidateFlowCommandResult(
            result.IsSuccess,
            runtime?.Workflows.Count ?? 0,
            runtime?.Resources.Count ?? 0,
            diagnostics);
    }

    private static ScenarioRunCommandResult CreateScenarioResult(ScenarioRunResult result)
        => new(
            result.Name,
            result.IsSuccess,
            result.Status.ToString(),
            result.StartedAt,
            result.FinishedAt,
            (result.FinishedAt - result.StartedAt).TotalMilliseconds,
            result.Steps.Select(step => new ScenarioStepCommandResult(
                step.Name,
                step.Type,
                step.Status.ToString(),
                step.Message,
                step.MatchedEvent?.Type,
                step.MatchedEvent?.Topic,
                step.MatchedEvent?.Status)).ToArray());

    private static ScenarioRunCommandResult CreateScenarioFailureResult(string name, string message)
    {
        var now = DateTimeOffset.UtcNow;
        return new ScenarioRunCommandResult(
            name,
            false,
            ScenarioRunStatus.Failed.ToString(),
            now,
            now,
            0,
            [
                new ScenarioStepCommandResult(
                    "startup",
                    "host",
                    ScenarioStepRunStatus.Failed.ToString(),
                    message,
                    null,
                    null,
                    null)
            ]);
    }

    private static async Task<string> WaitForRunExit(TimeSpan? duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration ?? Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return duration is null ? "completed" : "duration elapsed";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return "canceled";
        }
    }
}
