using FluxMq.Core.Ids;
using FluxMq.Core.Models;

namespace FluxMq.App.Scenarios;

internal static class MqttScenarioClientProfiles
{
    public static MqttConnectionProfile Create(MqttConnectionProfile profile)
        => profile with
        {
            Id = ConnectionProfileId.New(),
            ClientId = CreateScenarioClientId(),
            CleanStart = true
        };

    private static string CreateScenarioClientId()
        => $"fluxmq-test-{Guid.NewGuid():N}";
}
