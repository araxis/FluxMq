namespace FluxMq.Pipeline.Definitions;

public sealed record FlowPortReference(FlowNodeName Node, FlowPortName Port)
{
    public static FlowPortReference Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Flow port reference cannot be empty.");
        }

        var separator = value.LastIndexOf('.');
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new FormatException("Flow port reference must use 'node.port' format.");
        }

        return new FlowPortReference(
            new FlowNodeName(value[..separator]),
            new FlowPortName(value[(separator + 1)..]));
    }

    public override string ToString() => $"{Node}.{Port}";
}
