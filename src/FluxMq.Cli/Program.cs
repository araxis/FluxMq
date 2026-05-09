using FluxMq.Cli;

var runner = new CliRunner(
    new TextWriterCliOutput(Console.Out),
    new TextWriterCliOutput(Console.Error));

return runner.Run(args);
