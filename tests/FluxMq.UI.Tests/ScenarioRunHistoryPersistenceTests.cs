using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Scenarios;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class ScenarioRunHistoryPersistenceTests
{
    [Fact]
    public async Task RunActiveTestScenarioAsync_PersistsReportSnapshot()
    {
        var history = new InMemoryScenarioRunHistoryRepository();
        await using var service = new FlowWorkspaceService(
            new FlowDefinitionComposer(),
            scenarioRunHistory: history);

        service.AddTest("smoke");
        service.AddTestScenarioStep(ScenarioStepTypes.Delay);

        var result = await service.RunActiveTestScenarioAsync();

        result.ShouldNotBeNull();
        var stored = history.Runs.ShouldHaveSingleItem();
        stored.ProjectName.ShouldBe(service.Name);
        stored.ScenarioName.ShouldBe("smoke");
        stored.Status.ShouldBe("Passed");
        stored.StepCount.ShouldBe(1);
        stored.ReportJson.ShouldContain("\"schemaVersion\"");
        stored.ReportText.ShouldContain("Scenario 'smoke' Passed");
    }

    private sealed class InMemoryScenarioRunHistoryRepository : IScenarioRunHistoryRepository
    {
        public List<StoredScenarioRun> Runs { get; } = [];

        public void Add(StoredScenarioRun run)
            => Runs.Add(run);

        public IReadOnlyList<StoredScenarioRun> GetRecent(string projectName, string scenarioName, int take = 20)
            => Runs
                .Where(run => run.ProjectName == projectName && run.ScenarioName == scenarioName)
                .OrderByDescending(static run => run.FinishedAt)
                .Take(take)
                .ToArray();

        public bool Delete(Guid id)
            => Runs.RemoveAll(run => run.Id == id) > 0;
    }
}
