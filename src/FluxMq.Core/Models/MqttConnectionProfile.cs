namespace FluxMq.Core.Models;

public sealed record MqttConnectionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
    public string ClientId { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public bool UseTls { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public TimeSpan KeepAlive { get; init; } = TimeSpan.FromSeconds(60);
    public bool CleanStart { get; init; } = true;
}
