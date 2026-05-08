namespace FluxMq.Core.Ids;

public readonly record struct ConnectionProfileId(Guid Value)
{
    public static ConnectionProfileId New() => new(Guid.NewGuid());
    public static ConnectionProfileId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
