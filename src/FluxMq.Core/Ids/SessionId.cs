namespace FluxMq.Core.Ids;

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());
    public static SessionId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
