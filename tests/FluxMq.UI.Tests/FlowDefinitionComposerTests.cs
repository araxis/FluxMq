using FluxMq.App;
using FluxMq.Core.Models;
using FluxMq.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace FluxMq.UI.Tests;

public sealed class FlowDefinitionComposerTests
{
    [Fact]
    public void CreateInspectPayloadsDefinition_CreatesHostBuildableDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile
            {
                Name = "local-broker",
                Host = "localhost",
                Port = 1883,
                ClientId = "ui-tests"
            },
            "factory/#");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        using var host = FlowApplicationHost.CreateDefault(configuration);
        var result = host.Build();

        result.IsSuccess.Should().BeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
        result.RuntimeBuild!.Runtime!.Resources.Keys.Select(key => key.Value).Should().Contain("source");
    }

    [Fact]
    public void UpsertComponent_UpdatesSourceWithoutRemovingExistingWorkflowNodes()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "first", Host = "localhost", Port = 1883, ClientId = "first-client" },
            "#");

        var updated = composer.UpsertComponent(
            initial,
            "mqtt.message-source",
            new MqttConnectionProfile { Name = "second", Host = "127.0.0.1", Port = 1884, ClientId = "second-client" },
            "devices/#");

        using var document = JsonDocument.Parse(updated);
        var flowApplication = document.RootElement.GetProperty("FluxMq").GetProperty("FlowApplication");

        flowApplication
            .GetProperty("resources")
            .GetProperty("source")
            .GetProperty("configuration")
            .GetProperty("profile")
            .GetProperty("port")
            .GetInt32()
            .Should()
            .Be(1884);

        flowApplication
            .GetProperty("workflows")
            .GetProperty("inspectPayloads")
            .TryGetProperty("inspect", out _)
            .Should()
            .BeTrue();
    }
}
