namespace FluxMq.Cli;

public interface ICliOutput
{
    void WriteLine(string message);
}

public sealed class TextWriterCliOutput : ICliOutput
{
    private readonly TextWriter _writer;

    public TextWriterCliOutput(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void WriteLine(string message) => _writer.WriteLine(message);
}
