namespace FluxMq.Pipeline.Definitions;

public readonly record struct FlowNodeType
{
    public FlowNodeType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Flow node type cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}
