namespace FluxMq.Pipeline.Definitions;

public readonly record struct PortName
{
    public PortName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Flow port name cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}
