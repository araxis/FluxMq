namespace FluxMq.Scenarios;

public static class ScenarioEventSourceRequirements
{
    public static bool RequiresAttachedEventStream(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var hasScenarioEventSource = false;
        foreach (var step in scenario.Steps.Values)
        {
            if (IsScenarioEventSourceStep(step.Type))
            {
                hasScenarioEventSource = true;
                continue;
            }

            if (IsEventObservingStep(step.Type) && !hasScenarioEventSource)
            {
                return true;
            }
        }

        return false;
    }

    public static string DescribeMissingEventStream(string scenarioName)
        => $"Scenario '{scenarioName}' contains event-observing steps before any scenario event source. Add a scenario mqtt.publisher or mqtt.trigger step before expect.event or when.event, start the app runtime and run the scenario against it, or remove event-observing steps from this scenario.";

    private static bool IsScenarioEventSourceStep(string type)
        => string.Equals(type, ScenarioStepTypes.MqttPublisher, StringComparison.Ordinal) ||
           string.Equals(type, ScenarioStepTypes.MqttTrigger, StringComparison.Ordinal);

    private static bool IsEventObservingStep(string type)
        => string.Equals(type, ScenarioStepTypes.ExpectEvent, StringComparison.Ordinal) ||
           string.Equals(type, ScenarioStepTypes.WhenEvent, StringComparison.Ordinal);
}
