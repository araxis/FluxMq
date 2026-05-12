namespace FluxMq.Pipeline.Definitions;

public readonly record struct WorkflowName
{
    public WorkflowName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workflow name cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}
