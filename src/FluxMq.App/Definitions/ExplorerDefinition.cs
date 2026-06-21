using FluxFlow.Components.Secrets.Contracts;

namespace FluxMq.App.Definitions;

public sealed record ExplorerDefinition
{
    public const string MqttTopicsType = "mqtt.topics";

    public string Type { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ConnectionResource { get; init; }
    public ExplorerConnectionDefinition? Connection { get; init; }
    public List<string> Subscriptions { get; init; } = [];
    public bool AutoConnect { get; init; } = true;
}

public sealed record ExplorerConnectionDefinition
{
    public string? Name { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public bool? UseTls { get; init; }
    public bool? AllowUntrustedCertificates { get; init; }
    public string? CaCertificatePath { get; init; }
    public string? ClientCertificatePath { get; init; }
    public string? ClientCertificatePassword { get; init; }
    public string? ClientId { get; init; }
    public bool? CleanStart { get; init; }
    public int? KeepAliveSeconds { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public SecretReference? PasswordSecret { get; init; }
}
