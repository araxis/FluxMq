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
        if (result.IsSuccess)
        {
            var runtime = result.RuntimeBuild!.Runtime!;
            _output.WriteLine($"Flow application is valid. Workflows: {runtime.Workflows.Count}. Resources: {runtime.Resources.Count}.");
            return (int)CliExitCode.Success;
        }

        _error.WriteLine("Flow application is invalid.");
        foreach (var error in result.Errors)
        {
            _error.WriteLine($"Host error {error.Code}: {error.Message}");
        }

        if (result.RuntimeBuild is not null)
        {
            foreach (var error in result.RuntimeBuild.Validation.Errors)
            {
                _error.WriteLine($"Definition error {error.Code}: {error.Message}");
            }

            foreach (var error in result.RuntimeBuild.Errors)
            {
                var location = FormatLocation(error.WorkflowName, error.NodeName?.Value, error.PortName?.Value);
                _error.WriteLine($"Runtime error {error.Code}{location}: {error.Message}");
            }
        }

        return (int)CliExitCode.ValidationError;
    }

    private static string FormatLocation(string? workflowName, string? nodeName, string? portName)
    {
        var parts = new[] { workflowName, nodeName, portName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? "" : $" [{string.Join(".", parts)}]";
    }

    private static void WriteUsage(ICliOutput output)
    {
        output.WriteLine("Usage:");
        output.WriteLine("  fluxmq validate --config <path> [--section <name>]");
    }
}
