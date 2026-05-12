namespace FluxMq.Pipeline.Definitions;

public readonly record struct NodeName
{
    public NodeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Flow node name cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}
