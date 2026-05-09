using System.Text.Json;

namespace FluxMq.Cli;

public static class ValidateFlowResultRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void Write(ValidateFlowCommandResult result, CliOutputFormat format, ICliOutput output)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);

        if (format == CliOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        WriteText(result, output);
    }

    private static void WriteText(ValidateFlowCommandResult result, ICliOutput output)
    {
        if (result.IsValid)
        {
            output.WriteLine($"Flow application is valid. Workflows: {result.WorkflowCount}. Resources: {result.ResourceCount}.");
            return;
        }

        output.WriteLine("Flow application is invalid.");
        foreach (var diagnostic in result.Diagnostics)
        {
            var location = FormatLocation(diagnostic.WorkflowName, diagnostic.NodeName, diagnostic.PortName);
            output.WriteLine($"{FormatSource(diagnostic.Source)} {diagnostic.Code}{location}: {diagnostic.Message}");
        }
    }

    private static string FormatSource(string source)
    {
        return source switch
        {
            "host" => "Host error",
            "definition" => "Definition error",
            "runtime" => "Runtime error",
            _ => source
        };
    }

    private static string FormatLocation(string? workflowName, string? nodeName, string? portName)
    {
        var parts = new[] { workflowName, nodeName, portName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? "" : $" [{string.Join(".", parts)}]";
    }
}
