using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class FlowApplicationRuntimeBuilder
{
    private readonly FlowRuntimeNodeFactoryRegistry _factories;
    private readonly FlowApplicationDefinitionValidator _validator;

    public FlowApplicationRuntimeBuilder(
        FlowRuntimeNodeFactoryRegistry factories,
        FlowApplicationDefinitionValidator? validator = null)
    {
        _factories = factories;
        _validator = validator ?? new FlowApplicationDefinitionValidator();
    }

    public FlowApplicationRuntimeBuildResult Build(FlowApplicationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var validation = _validator.Validate(definition);
        if (!validation.IsValid)
        {
            return FlowApplicationRuntimeBuildResult.Failed(
                validation,
                validation.Errors
                    .Select(error => new FlowApplicationRuntimeBuildError(
                        FlowApplicationRuntimeBuildErrorCode.ValidationFailed,
                        error.Message))
                    .ToArray());
        }

        var errors = new List<FlowApplicationRuntimeBuildError>();
        var links = new List<IDisposable>();
        var linkedTargets = new HashSet<FlowRuntimeNode>();

        var resources = CreateNodes(null, definition.Resources, errors);
        var workflows = definition.Workflows.ToDictionary(
            workflow => workflow.Key,
            workflow => (IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>)CreateNodes(workflow.Key, workflow.Value, errors, resources));

        if (errors.Count == 0)
        {
            LinkWorkflows(definition, resources, workflows, links, linkedTargets, errors);
        }

        if (errors.Count > 0)
        {
            DisposeCreatedNodes(resources, workflows, links);
            return FlowApplicationRuntimeBuildResult.Failed(validation, errors);
        }

        return FlowApplicationRuntimeBuildResult.Succeeded(
            new FlowApplicationRuntime(resources, workflows, links, FindEntryNodes(resources, workflows, linkedTargets)),
            validation);
    }

    private IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> CreateNodes(
        string? workflowName,
        IReadOnlyDictionary<string, FlowNodeDefinition> definitions,
        List<FlowApplicationRuntimeBuildError> errors,
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>? resources = null)
    {
        var nodes = new Dictionary<FlowNodeName, FlowRuntimeNode>();
        // Resources see themselves so a resource may reference an earlier-built resource.
        var resourceView = resources ?? nodes;

        foreach (var definition in definitions)
        {
            var nodeName = new FlowNodeName(definition.Key);

            if (!_factories.TryGetFactory(definition.Value.Type, out var factory))
            {
                errors.Add(new(
                    FlowApplicationRuntimeBuildErrorCode.UnknownNodeType,
                    $"No flow node factory is registered for type '{definition.Value.Type}'.",
                    workflowName,
                    nodeName));
                continue;
            }

            try
            {
                nodes.Add(nodeName, factory(new FlowRuntimeNodeFactoryContext(
                    nodeName,
                    definition.Value,
                    workflowName,
                    resourceView)));
            }
            catch (Exception exception)
            {
                errors.Add(new(
                    FlowApplicationRuntimeBuildErrorCode.FactoryFailed,
                    $"Factory for node '{nodeName}' failed: {exception.Message}",
                    workflowName,
                    nodeName));
            }
        }

        return nodes;
    }

    private static void LinkWorkflows(
        FlowApplicationDefinition definition,
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>> workflows,
        List<IDisposable> links,
        HashSet<FlowRuntimeNode> linkedTargets,
        List<FlowApplicationRuntimeBuildError> errors)
    {
        foreach (var workflowDefinition in definition.Workflows)
        {
            var workflowName = workflowDefinition.Key;
            var workflowNodes = workflows[workflowName];

            foreach (var targetDefinition in workflowDefinition.Value)
            {
                var targetName = new FlowNodeName(targetDefinition.Key);
                var targetNode = workflowNodes[targetName];

                foreach (var portLinks in targetDefinition.Value.GetAllPortLinks())
                {
                    var targetPortName = new FlowPortName(portLinks.Key);
                    if (!targetNode.Inputs.TryGetValue(targetPortName, out var input))
                    {
                        errors.Add(new(
                            FlowApplicationRuntimeBuildErrorCode.MissingInputPort,
                            $"Node '{targetName}' does not expose input port '{targetPortName}'.",
                            workflowName,
                            targetName,
                            targetPortName));
                        continue;
                    }

                    foreach (var link in portLinks.Value)
                    {
                        if (!TryFindSource(link.From.Node, workflowNodes, resources, out var sourceNode))
                        {
                            continue;
                        }

                        if (!sourceNode.Outputs.TryGetValue(link.From.Port, out var output))
                        {
                            errors.Add(new(
                                FlowApplicationRuntimeBuildErrorCode.MissingOutputPort,
                                $"Node '{sourceNode.Name}' does not expose output port '{link.From.Port}'.",
                                workflowName,
                                sourceNode.Name,
                                link.From.Port));
                            continue;
                        }

                        var disposable = output.TryLinkTo(input, propagateCompletion: true, out var error);
                        if (error is not null)
                        {
                            errors.Add(error with
                            {
                                WorkflowName = workflowName,
                                NodeName = targetName,
                                PortName = targetPortName
                            });
                            continue;
                        }

                        if (disposable is not null)
                        {
                            links.Add(disposable);
                            linkedTargets.Add(targetNode);
                        }
                    }
                }
            }
        }
    }

    private static bool TryFindSource(
        FlowNodeName sourceName,
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> workflowNodes,
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> resources,
        out FlowRuntimeNode sourceNode)
    {
        if (workflowNodes.TryGetValue(sourceName, out sourceNode!))
        {
            return true;
        }

        return resources.TryGetValue(sourceName, out sourceNode!);
    }

    private static void DisposeCreatedNodes(
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>> workflows,
        List<IDisposable> links)
    {
        using var runtime = new FlowApplicationRuntime(resources, workflows, links);
    }

    private static IReadOnlyList<FlowRuntimeNode> FindEntryNodes(
        IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<FlowNodeName, FlowRuntimeNode>> workflows,
        HashSet<FlowRuntimeNode> linkedTargets)
    {
        var nodes = resources.Values
            .Concat(workflows.Values.SelectMany(workflow => workflow.Values))
            .Where(node => !linkedTargets.Contains(node))
            .ToArray();

        return nodes;
    }
}
