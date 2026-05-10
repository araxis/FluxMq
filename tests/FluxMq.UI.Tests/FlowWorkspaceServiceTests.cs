using FluxMq.UI.Models;
using FluxMq.UI.Services;
using FluentAssertions;

namespace FluxMq.UI.Tests;

public sealed class FlowWorkspaceServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsCurrentDefinition()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionFactory());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-{Guid.NewGuid():N}.json");
        service.SetFilePath(path);

        try
        {
            var expected = service.DefinitionJson;

            await service.SaveToFileAsync();
            service.SetDefinitionJson("{}");
            await service.LoadFromFileAsync();

            service.DefinitionJson.Should().Be(expected);
            service.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == "Error");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ValidateAsync_ConvertsInvalidJsonToDiagnostic()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionFactory());
        service.SetDefinitionJson("{");

        await service.ValidateAsync();

        service.State.Should().Be(RuntimeWorkspaceState.Faulted);
        service.Diagnostics.Should().Contain(diagnostic => diagnostic.Severity == "Error");
    }

    [Fact]
    public async Task ValidateAsync_AcceptsDefaultDefinition()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionFactory());

        await service.ValidateAsync();

        service.State.Should().Be(RuntimeWorkspaceState.Valid);
        service.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "Ready");
    }
}
