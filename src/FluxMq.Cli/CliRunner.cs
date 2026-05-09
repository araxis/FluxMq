using FluxMq.App;
using FluxMq.Pipeline.Runtime;
using Microsoft.Extensions.Configuration;

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
        if (!CliOptionsParser.TryParse(args, out var options, out var parseError))
        {
            _error.WriteLine(parseError!);
            WriteUsage(_error);
            return (int)CliExitCode.UsageError;
        }

        if (options.Command is "-h" or "--help")
        {
            WriteUsage(_output);
            return (int)CliExitCode.Success;
        }

        try
        {
            if (string.Equals(options.Command, CliOptions.ValidateCommand, StringComparison.OrdinalIgnoreCase))
            {
                return Validate(options);
            }

            return await RunFlow(options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _error.WriteLine($"Unexpected failure: {exception.Message}");
            return (int)CliExitCode.UnexpectedError;
        }
    }

    private int Validate(CliOptions options)
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

    private async Task<int> RunFlow(CliOptions options, CancellationToken cancellationToken)
    {
        if (!TryBuildHost(options, out var host, out var exitCode))
        {
            return exitCode;
        }

        await using (var flowHost = host ?? throw new InvalidOperationException("Host was not created."))
        {
            var startResult = flowHost.Start();
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
            new FlowApplicationRuntimeBuilder(
                new FlowRuntimeNodeFactoryRegistry()
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

    private static void WriteUsage(ICliOutput output)
    {
        output.WriteLine("Usage:");
        output.WriteLine("  fluxmq validate --config <path> [--section <name>] [--output text|json]");
        output.WriteLine("  fluxmq run --config <path> [--section <name>] [--duration-ms <milliseconds>] [--output text|json]");
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
