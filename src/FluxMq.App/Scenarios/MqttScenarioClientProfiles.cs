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
    {
        var value = $"fluxmq-test-{Guid.NewGuid():N}";
        return value.Length <= 23 ? value : value[..23];
    }
}
