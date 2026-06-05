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
            tests[name] = CreateScenario();
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

        var phases = scenario["phases"] as JsonObject;
        if (phases is null || phases.Count == 0)
        {
            var steps = scenario["steps"] as JsonObject ?? new JsonObject();
            return new TestScenarioSnapshot(testName, ReadScenarioSteps(steps));
        }

        return new TestScenarioSnapshot(testName, ReadScenarioPhases(phases));
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
        EnsureScenarioShape(scenario);
        var steps = GetOrCreateScenarioPhaseSteps(scenario, normalizedType);
        var stepName = MakeUniqueScenarioStepName(steps, FlowScenarioStepDefinitionFactory.NamePrefix(normalizedType));
        steps[stepName] = FlowScenarioStepDefinitionFactory.CreateStep(flowApplication, normalizedType);
        SyncScenarioFlatSteps(scenario);

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

        EnsureScenarioShape(scenario);
        if (FindScenarioStep(scenario, stepName) is not { Step: JsonObject step })
        {
            return json;
        }

        var normalizedType = string.IsNullOrWhiteSpace(stepType)
            ? ReadString(step, "type") ?? ScenarioStepTypes.ExpectEvent
            : stepType.Trim();
        step["type"] = normalizedType;
        step["configuration"] = FlowScenarioStepDefinitionFactory.CreateConfiguration(normalizedType, configuration);
        SyncScenarioFlatSteps(scenario);
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
            tests[testName] is not JsonObject scenario)
        {
            return json;
        }

        EnsureScenarioShape(scenario);
        if (FindScenarioStep(scenario, stepName) is not { Steps: JsonObject steps } ||
            !steps.Remove(stepName))
        {
            return json;
        }

        SyncScenarioFlatSteps(scenario);
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
            tests[testName] is not JsonObject scenario)
        {
            return json;
        }

        EnsureScenarioShape(scenario);
        var phases = scenario["phases"] as JsonObject;
        var entries = ReadScenarioStepEntries(scenario).ToList();
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

        if (phases is null)
        {
            var reordered = new JsonObject();
            foreach (var entry in entries)
            {
                reordered[entry.Key] = entry.Value;
            }

            scenario["steps"] = reordered;
            return root.ToJsonString(Options);
        }

        var movedPhase = targetIndex > 0
            ? entries[targetIndex - 1].PhaseName
            : entries.Count > 1
                ? entries[1].PhaseName
                : moved.PhaseName;
        ClearPhaseSteps(phases);
        foreach (var entry in entries)
        {
            var phaseName = string.Equals(entry.Key, moved.Key, StringComparison.Ordinal)
                ? movedPhase
                : entry.PhaseName;
            var phase = phases[phaseName] as JsonObject;
            if (phase is null)
            {
                phase = CreatePhase(FormatPhaseTitle(phaseName), phaseName, PhaseOrder(phaseName));
                phases[phaseName] = phase;
            }

            var phaseSteps = GetOrCreateObject(phase, "steps");
            phaseSteps[entry.Key] = entry.Value;
        }

        SyncScenarioFlatSteps(scenario);
        return root.ToJsonString(Options);
    }

    private static JsonObject CreateScenario()
        => new()
        {
            ["version"] = 2,
            ["phases"] = new JsonObject
            {
                ["setup"] = CreatePhase("Setup", "setup", 0),
                ["stimulus"] = CreatePhase("Stimulus", "stimulus", 1),
                ["observe"] = CreatePhase("Observe", "observe", 2),
                ["assert"] = CreatePhase("Assert", "assert", 3),
                ["cleanup"] = CreatePhase("Cleanup", "cleanup", 4)
            },
            ["runProfile"] = new JsonObject
            {
                ["mode"] = "local",
                ["timeoutMs"] = 30000,
                ["stopOnFailure"] = true
            },
            ["steps"] = new JsonObject(),
            ["runHistory"] = new JsonArray(),
            ["reportSnapshots"] = new JsonArray()
        };

    private static JsonObject CreatePhase(string title, string kind, int order)
        => new()
        {
            ["title"] = title,
            ["kind"] = kind,
            ["order"] = order,
            ["steps"] = new JsonObject()
        };

    private static void EnsureScenarioShape(JsonObject scenario)
    {
        scenario["version"] = 2;
        if (scenario["phases"] is not JsonObject phases || phases.Count == 0)
        {
            var legacySteps = scenario["steps"] as JsonObject;
            phases = new JsonObject();
            if (legacySteps is { Count: > 0 })
            {
                phases["imported"] = new JsonObject
                {
                    ["title"] = "Imported",
                    ["kind"] = "imported",
                    ["order"] = 0,
                    ["steps"] = CloneObject(legacySteps)
                };
            }
            else
            {
                phases["setup"] = CreatePhase("Setup", "setup", 0);
                phases["stimulus"] = CreatePhase("Stimulus", "stimulus", 1);
                phases["observe"] = CreatePhase("Observe", "observe", 2);
                phases["assert"] = CreatePhase("Assert", "assert", 3);
                phases["cleanup"] = CreatePhase("Cleanup", "cleanup", 4);
            }

            scenario["phases"] = phases;
        }

        if (scenario["runProfile"] is not JsonObject)
        {
            scenario["runProfile"] = new JsonObject
            {
                ["mode"] = "local",
                ["timeoutMs"] = 30000,
                ["stopOnFailure"] = true
            };
        }

        if (scenario["runHistory"] is not JsonArray)
        {
            scenario["runHistory"] = new JsonArray();
        }

        if (scenario["reportSnapshots"] is not JsonArray)
        {
            scenario["reportSnapshots"] = new JsonArray();
        }

        SyncScenarioFlatSteps(scenario);
    }

    private static JsonObject GetOrCreateScenarioPhaseSteps(JsonObject scenario, string stepType)
    {
        var phases = GetOrCreateObject(scenario, "phases");
        var phaseName = ScenarioStepCatalog.Shared.Describe(stepType).DefaultPhase;
        if (phases[phaseName] is not JsonObject phase)
        {
            phase = CreatePhase(FormatPhaseTitle(phaseName), phaseName, PhaseOrder(phaseName));
            phases[phaseName] = phase;
        }

        return GetOrCreateObject(phase, "steps");
    }

    private static (JsonObject Phase, JsonObject Steps, JsonObject Step)? FindScenarioStep(
        JsonObject scenario,
        string stepName)
    {
        if (scenario["phases"] is JsonObject phases)
        {
            foreach (var phaseEntry in phases.OrderBy(static phase => PhaseOrder(phase.Key)))
            {
                if (phaseEntry.Value is not JsonObject phase ||
                    phase["steps"] is not JsonObject steps ||
                    steps[stepName] is not JsonObject step)
                {
                    continue;
                }

                return (phase, steps, step);
            }
        }

        if (scenario["steps"] is JsonObject legacySteps &&
            legacySteps[stepName] is JsonObject legacyStep)
        {
            return (scenario, legacySteps, legacyStep);
        }

        return null;
    }

    private static void SyncScenarioFlatSteps(JsonObject scenario)
    {
        if (scenario["phases"] is not JsonObject phases)
        {
            if (scenario["steps"] is not JsonObject)
            {
                scenario["steps"] = new JsonObject();
            }

            return;
        }

        var steps = new JsonObject();
        foreach (var phaseEntry in phases.OrderBy(static phase => PhaseOrder(phase.Key)))
        {
            if (phaseEntry.Value is not JsonObject phase ||
                phase["steps"] is not JsonObject phaseSteps)
            {
                continue;
            }

            foreach (var step in phaseSteps)
            {
                steps[step.Key] = step.Value?.DeepClone();
            }
        }

        scenario["steps"] = steps;
    }

    private static IEnumerable<(string Key, JsonNode? Value, string PhaseName)> ReadScenarioStepEntries(JsonObject scenario)
    {
        if (scenario["phases"] is JsonObject phases)
        {
            foreach (var phaseEntry in phases.OrderBy(static phase => PhaseOrder(phase.Key)))
            {
                if (phaseEntry.Value is not JsonObject phase ||
                    phase["steps"] is not JsonObject phaseSteps)
                {
                    continue;
                }

                foreach (var step in phaseSteps)
                {
                    yield return (step.Key, step.Value?.DeepClone(), phaseEntry.Key);
                }
            }

            yield break;
        }

        if (scenario["steps"] is JsonObject steps)
        {
            foreach (var step in steps)
            {
                yield return (step.Key, step.Value?.DeepClone(), "main");
            }
        }
    }

    private static void ClearPhaseSteps(JsonObject phases)
    {
        foreach (var phaseEntry in phases)
        {
            if (phaseEntry.Value is JsonObject phase)
            {
                phase["steps"] = new JsonObject();
            }
        }
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

    private static IReadOnlyList<ScenarioPhaseSnapshot> ReadScenarioPhases(JsonObject phases)
    {
        var result = new List<ScenarioPhaseSnapshot>();
        foreach (var phase in phases.OrderBy(static phase => PhaseOrder(phase.Key)))
        {
            if (phase.Value is not JsonObject phaseObject)
            {
                continue;
            }

            var steps = phaseObject["steps"] as JsonObject ?? new JsonObject();
            result.Add(new ScenarioPhaseSnapshot(
                phase.Key,
                ReadString(phaseObject, "title") ?? FormatPhaseTitle(phase.Key),
                ReadString(phaseObject, "kind") ?? phase.Key,
                ReadScenarioSteps(steps)));
        }

        return result;
    }

    private static JsonObject CloneObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var item in source)
        {
            result[item.Key] = item.Value?.DeepClone();
        }

        return result;
    }

    private static string FormatPhaseTitle(string value)
        => string.IsNullOrWhiteSpace(value)
            ? "Phase"
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static int PhaseOrder(string phaseName)
        => phaseName switch
        {
            "setup" => 0,
            "stimulus" => 1,
            "observe" => 2,
            "assert" => 3,
            "cleanup" => 4,
            "imported" => 0,
            _ => 10
        };

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
