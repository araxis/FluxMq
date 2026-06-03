using FluxMq.Scenarios;
using FluxMq.UI.Models;
using System.Text.Json.Nodes;

namespace FluxMq.UI.Services;

public sealed partial class FlowDefinitionComposer
{
    /// <summary>Adds an empty test scenario artifact with the given name if it does not already exist.</summary>
    public string AddTest(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var tests = GetOrCreateObject(flowApplication, "tests");
        if (!tests.ContainsKey(name))
        {
            tests[name] = new JsonObject
            {
                ["steps"] = new JsonObject()
            };
        }

        return root.ToJsonString(Options);
    }

    /// <summary>Removes a test scenario by name, leaving the definition unchanged if it doesn't exist.</summary>
    public string RemoveTest(string json, string name)
    {
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is JsonObject tests)
        {
            tests.Remove(name);
        }

        return root.ToJsonString(Options);
    }

    public TestScenarioSnapshot? GetTestScenario(string json, string testName)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return null;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario)
        {
            return null;
        }

        var steps = scenario["steps"] as JsonObject ?? new JsonObject();
        return new TestScenarioSnapshot(testName, ReadScenarioSteps(steps));
    }

    public string AddScenarioStep(string json, string testName, string stepType)
    {
        if (string.IsNullOrWhiteSpace(testName))
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(stepType)
            ? ScenarioStepTypes.ExpectEvent
            : stepType.Trim();
        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        var tests = GetOrCreateObject(flowApplication, "tests");
        var scenario = GetOrCreateObject(tests, testName);
        var steps = GetOrCreateObject(scenario, "steps");
        var stepName = MakeUniqueScenarioStepName(steps, FlowScenarioStepDefinitionFactory.NamePrefix(normalizedType));
        steps[stepName] = FlowScenarioStepDefinitionFactory.CreateStep(flowApplication, normalizedType);

        return root.ToJsonString(Options);
    }

    public string UpdateScenarioStep(
        string json,
        string testName,
        string stepName,
        string stepType,
        IReadOnlyDictionary<string, string> configuration)
    {
        if (string.IsNullOrWhiteSpace(testName) ||
            string.IsNullOrWhiteSpace(stepName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario)
        {
            return json;
        }

        var steps = GetOrCreateObject(scenario, "steps");
        if (steps[stepName] is not JsonObject step)
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(stepType)
            ? ReadString(step, "type") ?? ScenarioStepTypes.ExpectEvent
            : stepType.Trim();
        step["type"] = normalizedType;
        step["configuration"] = FlowScenarioStepDefinitionFactory.CreateConfiguration(normalizedType, configuration);
        return root.ToJsonString(Options);
    }

    public string RemoveScenarioStep(string json, string testName, string stepName)
    {
        if (string.IsNullOrWhiteSpace(testName) ||
            string.IsNullOrWhiteSpace(stepName))
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario ||
            scenario["steps"] is not JsonObject steps ||
            !steps.Remove(stepName))
        {
            return json;
        }

        return root.ToJsonString(Options);
    }

    public string MoveScenarioStep(string json, string testName, string stepName, int offset)
    {
        if (string.IsNullOrWhiteSpace(testName) ||
            string.IsNullOrWhiteSpace(stepName) ||
            offset == 0)
        {
            return json;
        }

        var root = ParseOrCreate(json);
        var flowApplication = GetFlowApplication(root);
        if (flowApplication["tests"] is not JsonObject tests ||
            tests[testName] is not JsonObject scenario ||
            scenario["steps"] is not JsonObject steps)
        {
            return json;
        }

        var entries = steps
            .Select(step => (step.Key, Value: step.Value?.DeepClone()))
            .ToList();
        var currentIndex = entries.FindIndex(step => string.Equals(step.Key, stepName, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            return json;
        }

        var targetIndex = Math.Clamp(currentIndex + offset, 0, entries.Count - 1);
        if (targetIndex == currentIndex)
        {
            return json;
        }

        var moved = entries[currentIndex];
        entries.RemoveAt(currentIndex);
        entries.Insert(targetIndex, moved);

        var reordered = new JsonObject();
        foreach (var (key, value) in entries)
        {
            reordered[key] = value;
        }

        scenario["steps"] = reordered;
        return root.ToJsonString(Options);
    }

    private static IReadOnlyList<ScenarioStepSnapshot> ReadScenarioSteps(JsonObject steps)
    {
        var result = new List<ScenarioStepSnapshot>();
        foreach (var step in steps)
        {
            if (step.Value is not JsonObject stepObject)
            {
                continue;
            }

            var configuration = stepObject["configuration"] as JsonObject ?? new JsonObject();
            result.Add(new ScenarioStepSnapshot(
                step.Key,
                ReadString(stepObject, "type") ?? string.Empty,
                ReadConfigurationStrings(configuration)));
        }

        return result;
    }

    private static string MakeUniqueScenarioStepName(JsonObject steps, string preferred)
    {
        if (!steps.ContainsKey(preferred))
        {
            return preferred;
        }

        var index = 2;
        while (steps.ContainsKey($"{preferred}{index}"))
        {
            index++;
        }

        return $"{preferred}{index}";
    }
}
