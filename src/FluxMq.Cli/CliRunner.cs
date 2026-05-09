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
            return Validate(options);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _error.WriteLine($"Unexpected failure: {exception.Message}");
            return (int)CliExitCode.UnexpectedError;
        }
    }

    private int Validate(CliOptions options)
    {
        var configurationPath = Path.GetFullPath(options.ConfigurationPath!);
        if (!File.Exists(configurationPath))
        {
            _error.WriteLine($"Configuration file was not found: {configurationPath}");
            return (int)CliExitCode.UsageError;
        }

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configurationPath, optional: false, reloadOnChange: false)
            .Build();

        using var host = new FlowApplicationHost(
            configuration,
            new FlowApplicationRuntimeBuilder(
                new FlowRuntimeNodeFactoryRegistry()
                    .RegisterPipelineComponentFactories()),
            sectionName: options.SectionName);

        var result = host.Build();
        var commandResult = CreateValidateResult(result);
        var output = options.OutputFormat == CliOutputFormat.Json ? _output : commandResult.IsValid ? _output : _error;
        ValidateFlowResultRenderer.Write(commandResult, options.OutputFormat, output);

        return commandResult.IsValid ? (int)CliExitCode.Success : (int)CliExitCode.ValidationError;
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
    }
}
