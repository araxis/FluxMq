using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class FlowWorkspaceServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsCurrentDefinition()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var path = Path.Combine(Path.GetTempPath(), $"fluxmq-{Guid.NewGuid():N}.json");
        service.SetFilePath(path);

        try
        {
            var expected = service.DefinitionJson;

            await service.SaveToFileAsync();
            service.SetDefinitionJson("{}");
            await service.LoadFromFileAsync();

            service.DefinitionJson.ShouldBe(expected);
            service.Diagnostics.ShouldNotContain(diagnostic => diagnostic.Severity == "Error");
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
    public async Task ValidateAsync_DoesNotChangeDefinitionRevision()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var revision = service.DefinitionRevision;

        await service.ValidateAsync();

        service.DefinitionRevision.ShouldBe(revision);
    }

    [Fact]
    public void SetDefinitionJson_ChangesDefinitionRevisionOnlyWhenContentChanges()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        var initialRevision = service.DefinitionRevision;
        var json = service.DefinitionJson;

        service.SetDefinitionJson(json);
        service.DefinitionRevision.ShouldBe(initialRevision);

        service.SetDefinitionJson("{}");
        service.DefinitionRevision.ShouldBe(initialRevision + 1);
    }

    [Fact]
    public async Task ValidateAsync_ConvertsInvalidJsonToDiagnostic()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.SetDefinitionJson("{");

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Faulted);
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Severity == "Error");
    }

    [Fact]
    public async Task ValidateAsync_AcceptsDefaultDefinition()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Valid);
        service.Diagnostics.ShouldContain(diagnostic => diagnostic.Code == "Ready");
    }
}
