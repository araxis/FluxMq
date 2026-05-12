namespace FluxMq.Pipeline.Definitions;

public sealed record PortReference(NodeName Node, PortName Port)
{
    public static PortReference Parse(string value)
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

        return new PortReference(
            new NodeName(value[..separator]),
            new PortName(value[(separator + 1)..]));
    }

    public override string ToString() => $"{Node}.{Port}";
}
