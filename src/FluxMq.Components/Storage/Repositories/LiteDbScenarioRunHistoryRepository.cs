using FluxMq.Components.Storage.Models;

namespace FluxMq.Components.Storage.Repositories;

public sealed class LiteDbScenarioRunHistoryRepository(FluxDbContext ctx) : IScenarioRunHistoryRepository
{
    private readonly FluxDbContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

    public void Add(StoredScenarioRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _ctx.ScenarioRuns.Insert(run);
    }

    public IReadOnlyList<StoredScenarioRun> GetRecent(string projectName, string scenarioName, int take = 20)
        => _ctx.ScenarioRuns
            .Find(run =>
                run.ProjectName == projectName &&
                run.ScenarioName == scenarioName)
            .OrderByDescending(static run => run.FinishedAt)
            .Take(Math.Max(1, take))
            .ToList();

    public bool Delete(Guid id)
        => _ctx.ScenarioRuns.Delete(id);
}
