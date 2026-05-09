using FluxMq.Cli;

var runner = new CliRunner(
    new TextWriterCliOutput(Console.Out),
    new TextWriterCliOutput(Console.Error));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await runner.RunAsync(args, cancellation.Token);
