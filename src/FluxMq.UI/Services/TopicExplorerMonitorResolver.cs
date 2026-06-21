using FluxMq.App.Definitions;
using FluxMq.Core.Models;

namespace FluxMq.UI.Services;

public static class TopicExplorerMonitorResolver
{
    public static IReadOnlyList<TopicExplorerMonitorConnection> Resolve(FlowWorkspaceService? project)
        => project is null
            ? []
            : Resolve(project.GetExplorers(), project.GetConnectionResources());

    public static IReadOnlyList<TopicExplorerMonitorConnection> Resolve(
        IReadOnlyDictionary<string, ExplorerDefinition> explorers,
        IReadOnlyList<(string Name, MqttConnectionProfile Profile, string Subscription)> connectionResources)
    {
        ArgumentNullException.ThrowIfNull(explorers);
        ArgumentNullException.ThrowIfNull(connectionResources);

        var configured = explorers
            .Where(static explorer => IsTopicExplorer(explorer.Value) && explorer.Value.AutoConnect)
            .Select(explorer => ResolveConfiguredExplorer(explorer.Key, explorer.Value, connectionResources))
            .Where(static connection => connection is not null)
            .Select(static connection => connection!)
            .ToArray();

        return configured.Length > 0
            ? configured
            : ResolveFallback(connectionResources);
    }

    private static bool IsTopicExplorer(ExplorerDefinition explorer)
        => string.Equals(explorer.Type, ExplorerDefinition.MqttTopicsType, StringComparison.Ordinal);

    private static TopicExplorerMonitorConnection? ResolveConfiguredExplorer(
        string explorerName,
        ExplorerDefinition explorer,
        IReadOnlyList<(string Name, MqttConnectionProfile Profile, string Subscription)> connectionResources)
    {
        var baseConnection = string.IsNullOrWhiteSpace(explorer.ConnectionResource)
            ? default
            : connectionResources.FirstOrDefault(connection =>
                string.Equals(connection.Name, explorer.ConnectionResource, StringComparison.Ordinal));

        if (baseConnection.Profile is null && string.IsNullOrWhiteSpace(explorer.Connection?.Host))
        {
            return null;
        }

        var baseProfile = baseConnection.Profile ?? new MqttConnectionProfile
        {
            Name = DisplayName(explorerName, explorer, null),
            Host = explorer.Connection?.Host?.Trim() ?? "localhost",
            Port = explorer.Connection?.Port is > 0 ? explorer.Connection.Port.Value : 1883,
            ClientId = CreateDefaultClientId(explorerName)
        };

        var displayName = DisplayName(explorerName, explorer, baseProfile);
        var profile = ApplyConnection(explorerName, displayName, baseProfile, explorer.Connection);

        return new TopicExplorerMonitorConnection(
            explorerName.Trim(),
            displayName,
            EndpointLabel(profile),
            LiveMqttWorkspaceService.CreateTopicMonitorResourceName(explorerName),
            profile,
            SubscriptionList(explorer.Subscriptions));
    }

    private static IReadOnlyList<TopicExplorerMonitorConnection> ResolveFallback(
        IReadOnlyList<(string Name, MqttConnectionProfile Profile, string Subscription)> connectionResources)
        => connectionResources
            .GroupBy(static connection => new BrokerEndpointKey(
                NormalizeHost(connection.Profile.Host),
                connection.Profile.Port,
                connection.Profile.UseTls))
            .Select(static group =>
            {
                var first = group.First();
                var displayName = DisplayName(first.Name, first.Profile);
                var resourceSeed = ResourceSeed(group.Key);
                var profile = first.Profile with
                {
                    Name = displayName,
                    ClientId = CreateDefaultClientId(resourceSeed)
                };

                return new TopicExplorerMonitorConnection(
                    resourceSeed,
                    displayName,
                    EndpointLabel(profile),
                    LiveMqttWorkspaceService.CreateTopicMonitorResourceName(resourceSeed),
                    profile,
                    LiveMqttWorkspaceService.TopicExplorerMonitorSubscription);
            })
            .OrderBy(static connection => connection.DisplayName, StringComparer.Ordinal)
            .ToArray();

    private static MqttConnectionProfile ApplyConnection(
        string explorerName,
        string displayName,
        MqttConnectionProfile baseProfile,
        ExplorerConnectionDefinition? connection)
    {
        var passwordSecret = connection?.PasswordSecret ?? baseProfile.PasswordSecret;
        var password = connection?.Password is not null
            ? NullIfWhiteSpace(connection.Password)
            : baseProfile.Password;

        if (connection?.PasswordSecret is not null)
        {
            password = null;
        }

        return baseProfile with
        {
            Name = displayName,
            Host = NullIfWhiteSpace(connection?.Host) ?? baseProfile.Host,
            Port = connection?.Port is > 0 ? connection.Port.Value : baseProfile.Port,
            UseTls = connection?.UseTls ?? baseProfile.UseTls,
            AllowUntrustedCertificates = connection?.AllowUntrustedCertificates ?? baseProfile.AllowUntrustedCertificates,
            CaCertificatePath = connection?.CaCertificatePath is not null
                ? NullIfWhiteSpace(connection.CaCertificatePath)
                : baseProfile.CaCertificatePath,
            ClientCertificatePath = connection?.ClientCertificatePath is not null
                ? NullIfWhiteSpace(connection.ClientCertificatePath)
                : baseProfile.ClientCertificatePath,
            ClientCertificatePassword = connection?.ClientCertificatePassword is not null
                ? NullIfWhiteSpace(connection.ClientCertificatePassword)
                : baseProfile.ClientCertificatePassword,
            ClientId = NullIfWhiteSpace(connection?.ClientId) ?? CreateDefaultClientId(explorerName),
            CleanStart = connection?.CleanStart ?? baseProfile.CleanStart,
            KeepAlive = TimeSpan.FromSeconds(connection?.KeepAliveSeconds is > 0
                ? connection.KeepAliveSeconds.Value
                : Math.Max(1, (int)baseProfile.KeepAlive.TotalSeconds)),
            Username = connection?.Username is not null
                ? NullIfWhiteSpace(connection.Username)
                : baseProfile.Username,
            Password = password,
            PasswordSecret = passwordSecret
        };
    }

    private static string SubscriptionList(IEnumerable<string>? subscriptions)
    {
        var values = subscriptions?
            .Select(static subscription => subscription.Trim())
            .Where(static subscription => !string.IsNullOrWhiteSpace(subscription))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        return values.Length == 0
            ? LiveMqttWorkspaceService.TopicExplorerMonitorSubscription
            : string.Join(",", values);
    }

    private static string DisplayName(string resourceName, MqttConnectionProfile profile)
        => !string.IsNullOrWhiteSpace(resourceName)
            ? resourceName.Trim()
            : !string.IsNullOrWhiteSpace(profile.Name)
                ? profile.Name.Trim()
                : $"{profile.Host}:{profile.Port}";

    private static string DisplayName(string explorerName, ExplorerDefinition explorer, MqttConnectionProfile? baseProfile)
        => NullIfWhiteSpace(explorer.DisplayName) ??
           NullIfWhiteSpace(explorer.Connection?.Name) ??
           NullIfWhiteSpace(baseProfile?.Name) ??
           explorerName.Trim();

    private static string EndpointLabel(MqttConnectionProfile profile)
    {
        var scheme = profile.UseTls ? "mqtts" : "mqtt";
        return $"{scheme}://{profile.Host}:{profile.Port}";
    }

    private static string ResourceSeed(BrokerEndpointKey key)
        => $"{(key.UseTls ? "mqtts" : "mqtt")}-{NormalizeIdentifier(key.Host)}-{key.Port}";

    private static string CreateDefaultClientId(string seed)
    {
        var normalized = NormalizeIdentifier(seed);
        var clientId = $"fluxmq-topics-{(string.IsNullOrWhiteSpace(normalized) ? "broker" : normalized)}";
        return clientId.Length <= 64 ? clientId : clientId[..64];
    }

    private static string NormalizeIdentifier(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(static c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }

    private static string NormalizeHost(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "localhost"
            : value.Trim().ToLowerInvariant();

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BrokerEndpointKey(string Host, int Port, bool UseTls);
}

public sealed record TopicExplorerMonitorConnection(
    string ExplorerName,
    string DisplayName,
    string Endpoint,
    string ResourceName,
    MqttConnectionProfile Profile,
    string Subscription);
