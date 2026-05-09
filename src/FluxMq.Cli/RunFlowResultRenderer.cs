using System.Text.Json;

namespace FluxMq.Cli;

public static class RunFlowResultRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void Write(RunFlowCommandResult result, CliOutputFormat format, ICliOutput output)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);

        if (format == CliOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        output.WriteLine($"Flow application stopped. Reason: {result.ExitReason}. State: {result.HostState}.");
    }
}
