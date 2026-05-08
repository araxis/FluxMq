namespace FluxMq.Pipeline.Components;

public readonly record struct FlowErrorCode(int Value)
{
    public static FlowErrorCode NodeFaulted => new(1000);
    public static FlowErrorCode ProcessingFailed => new(2000);
    public static FlowErrorCode DynamicExpressionFailed => new(3000);
    public override string ToString() => Value.ToString();
}
