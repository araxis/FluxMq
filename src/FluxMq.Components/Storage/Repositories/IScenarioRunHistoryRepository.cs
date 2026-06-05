using FluxMq.Components.Storage.Models;

namespace FluxMq.Components.Storage.Repositories;

public interface IScenarioRunHistoryRepository
{
    void Add(StoredScenarioRun run);

    IReadOnlyList<StoredScenarioRun> GetRecent(string projectName, string scenarioName, int take = 20);

    bool Delete(Guid id);
}
