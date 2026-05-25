using System.Text.Json;

namespace FluxMq.Cli;

public static class ScenarioRunResultRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void Write(ScenarioRunCommandResult result, CliOutputFormat format, ICliOutput output)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);

        if (format == CliOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        var status = result.IsSuccess ? "passed" : "failed";
        output.WriteLine($"Scenario '{result.Name}' {status}. Steps: {result.Steps.Count}. Duration: {result.DurationMilliseconds:0} ms.");

        foreach (var step in result.Steps)
        {
            var message = string.IsNullOrWhiteSpace(step.Message) ? "" : $": {step.Message}";
            output.WriteLine($"- {step.Name} [{step.Type}] {step.Status}{message}");
        }
    }
}
