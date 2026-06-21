using FluxMq.App.Definitions;
using FluxMq.Core.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class TopicExplorerMonitorResolverTests
{
    [Fact]
    public void Resolve_CollapsesConnectionResourcesByBrokerEndpoint()
    {
        var monitors = TopicExplorerMonitorResolver.Resolve(
            new Dictionary<string, ExplorerDefinition>(StringComparer.Ordinal),
            [
                ("local-broker", new MqttConnectionProfile
                {
                    Name = "local-broker",
                    Host = "localhost",
                    Port = 1883,
                    ClientId = "app-client-a"
                }, "#"),
                ("local-broker2", new MqttConnectionProfile
                {
                    Name = "local-broker2",
                    Host = "localhost",
                    Port = 1883,
                    ClientId = "app-client-b"
                }, "#")
            ]);

        var monitor = monitors.ShouldHaveSingleItem();
        monitor.DisplayName.ShouldBe("local-broker");
        monitor.Endpoint.ShouldBe("mqtt://localhost:1883");
        monitor.ResourceName.ShouldBe("topics:mqtt-localhost-1883");
        monitor.Profile.ClientId.ShouldBe("fluxmq-topics-mqtt-localhost-1883");
        monitor.Subscription.ShouldBe(LiveMqttWorkspaceService.TopicExplorerMonitorSubscription);
    }

    [Fact]
    public void Resolve_UsesConfiguredExplorerConnectionResourceAndOverrides()
    {
        var explorers = new Dictionary<string, ExplorerDefinition>(StringComparer.Ordinal)
        {
            ["local"] = new()
            {
                Type = ExplorerDefinition.MqttTopicsType,
                DisplayName = "Local broker",
                ConnectionResource = "runtime",
                Connection = new ExplorerConnectionDefinition
                {
                    ClientId = "topics-local",
                    UseTls = true,
                    AllowUntrustedCertificates = true,
                    CaCertificatePath = "certs/root.pem",
                    ClientCertificatePath = "certs/client.pfx",
                    ClientCertificatePassword = "cert-pass",
                    CleanStart = false,
                    KeepAliveSeconds = 30,
                    Username = "viewer",
                    Password = "viewer-password"
                },
                Subscriptions = ["#", "$SYS/#"]
            }
        };

        var monitors = TopicExplorerMonitorResolver.Resolve(
            explorers,
            [
                ("runtime", new MqttConnectionProfile
                {
                    Name = "Runtime broker",
                    Host = "broker.local",
                    Port = 1883,
                    ClientId = "runtime-client",
                    Username = "runtime-user",
                    Password = "runtime-password",
                    CleanStart = true,
                    KeepAlive = TimeSpan.FromSeconds(60)
                }, "#")
            ]);

        var monitor = monitors.ShouldHaveSingleItem();
        monitor.ExplorerName.ShouldBe("local");
        monitor.DisplayName.ShouldBe("Local broker");
        monitor.ResourceName.ShouldBe("topics:local");
        monitor.Endpoint.ShouldBe("mqtts://broker.local:1883");
        monitor.Profile.Name.ShouldBe("Local broker");
        monitor.Profile.ClientId.ShouldBe("topics-local");
        monitor.Profile.UseTls.ShouldBeTrue();
        monitor.Profile.AllowUntrustedCertificates.ShouldBeTrue();
        monitor.Profile.CaCertificatePath.ShouldBe("certs/root.pem");
        monitor.Profile.ClientCertificatePath.ShouldBe("certs/client.pfx");
        monitor.Profile.ClientCertificatePassword.ShouldBe("cert-pass");
        monitor.Profile.CleanStart.ShouldBeFalse();
        monitor.Profile.KeepAlive.ShouldBe(TimeSpan.FromSeconds(30));
        monitor.Profile.Username.ShouldBe("viewer");
        monitor.Profile.Password.ShouldBe("viewer-password");
        monitor.Subscription.ShouldBe("#,$SYS/#");
    }
}
