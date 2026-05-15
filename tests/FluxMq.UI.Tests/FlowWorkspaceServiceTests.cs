using FluxMq.Core.Models;
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
    public async Task ValidateAsync_RejectsEmptyDefinition()
    {
        // Runtime requires at least one workflow — empty definition is Faulted
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Faulted);
        service.Diagnostics.ShouldContain(d => d.Severity == "Error");
    }

    [Fact]
    public void WorkflowNames_ReflectsCurrentDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var service = new FlowWorkspaceService(composer);
        service.WorkflowNames.ShouldBeEmpty();

        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");

        service.WorkflowNames.ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public void AddWorkflow_SetsActiveWorkflowNameWhenFirstAdded()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());

        service.AddWorkflow("first");

        service.ActiveWorkflowName.ShouldBe("first");
    }

    [Fact]
    public void SetActiveWorkflow_SwitchesActiveWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");

        service.SetActiveWorkflow("beta");

        service.ActiveWorkflowName.ShouldBe("beta");
    }

    [Fact]
    public void RemoveWorkflow_FallsBackToFirstRemainingWorkflow()
    {
        var service = new FlowWorkspaceService(new FlowDefinitionComposer());
        service.AddWorkflow("alpha");
        service.AddWorkflow("beta");
        service.SetActiveWorkflow("alpha");

        service.RemoveWorkflow("alpha");

        service.ActiveWorkflowName.ShouldBe("beta");
    }

    [Fact]
    public async Task ValidateAsync_AcceptsWellFormedDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var profile = new MqttConnectionProfile
        {
            Name = "test-broker",
            Host = "localhost",
            Port = 1883,
            ClientId = "test",
            KeepAlive = TimeSpan.FromSeconds(60),
            CleanStart = true
        };
        var service = new FlowWorkspaceService(composer);
        service.SetDefinitionJson(composer.CreateInspectPayloadsDefinition(profile, "#"));

        await service.ValidateAsync();

        service.State.ShouldBe(RuntimeWorkspaceState.Valid);
        service.Diagnostics.ShouldContain(d => d.Code == "Ready");
    }
}
