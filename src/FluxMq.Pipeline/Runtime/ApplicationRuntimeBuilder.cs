using FluxMq.Pipeline.Definitions;

namespace FluxMq.Pipeline.Runtime;

public sealed class ApplicationRuntimeBuilder
{
    private readonly RuntimeNodeFactoryRegistry _factories;
    private readonly ApplicationDefinitionValidator _validator;

    public ApplicationRuntimeBuilder(
        RuntimeNodeFactoryRegistry factories,
        ApplicationDefinitionValidator? validator = null)
    {
        _factories = factories;
        _validator = validator ?? new ApplicationDefinitionValidator();
    }

    public ApplicationRuntimeBuildResult Build(ApplicationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var validation = _validator.Validate(definition);
        if (!validation.IsValid)
        {
            return ApplicationRuntimeBuildResult.Failed(
                validation,
                validation.Errors
                    .Select(error => new ApplicationRuntimeBuildError(
                        ApplicationRuntimeBuildErrorCode.ValidationFailed,
                        error.Message))
                    .ToArray());
        }

        var errors = new List<ApplicationRuntimeBuildError>();
        var links = new List<IDisposable>();
        var linkedTargets = new HashSet<RuntimeNode>();

        var resources = CreateNodes(null, definition.Resources, errors);
        var workflows = definition.Workflows.ToDictionary(
            workflow => workflow.Key,
            workflow => (IReadOnlyDictionary<NodeName, RuntimeNode>)CreateNodes(workflow.Key, workflow.Value, errors, resources));

        if (errors.Count == 0)
        {
            LinkWorkflows(definition, resources, workflows, links, linkedTargets, errors);
        }

        if (errors.Count > 0)
        {
            DisposeCreatedNodes(resources, workflows, links);
            return ApplicationRuntimeBuildResult.Failed(validation, errors);
        }

        return ApplicationRuntimeBuildResult.Succeeded(
            new ApplicationRuntime(resources, workflows, links, FindEntryNodes(resources, workflows, linkedTargets)),
            validation);
    }

    private IReadOnlyDictionary<NodeName, RuntimeNode> CreateNodes(
        string? workflowName,
        IReadOnlyDictionary<string, NodeDefinition> definitions,
        List<ApplicationRuntimeBuildError> errors,
        IReadOnlyDictionary<NodeName, RuntimeNode>? resources = null)
    {
        var nodes = new Dictionary<NodeName, RuntimeNode>();
        // Resources see themselves so a resource may reference an earlier-built resource.
        var resourceView = resources ?? nodes;

        foreach (var definition in definitions)
        {
            var nodeName = new NodeName(definition.Key);

            if (!_factories.TryGetFactory(definition.Value.Type, out var factory))
            {
                errors.Add(new(
                    ApplicationRuntimeBuildErrorCode.UnknownNodeType,
                    $"No flow node factory is registered for type '{definition.Value.Type}'.",
                    workflowName,
                    nodeName));
                continue;
            }

            try
            {
                nodes.Add(nodeName, factory(new RuntimeNodeFactoryContext(
                    nodeName,
                    definition.Value,
                    workflowName,
                    resourceView)));
            }
            catch (Exception exception)
            {
                errors.Add(new(
                    ApplicationRuntimeBuildErrorCode.FactoryFailed,
                    $"Factory for node '{nodeName}' failed: {exception.Message}",
                    workflowName,
                    nodeName));
            }
        }

        return nodes;
    }

    private static void LinkWorkflows(
        ApplicationDefinition definition,
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> workflows,
        List<IDisposable> links,
        HashSet<RuntimeNode> linkedTargets,
        List<ApplicationRuntimeBuildError> errors)
    {
        foreach (var workflowDefinition in definition.Workflows)
        {
            var workflowName = workflowDefinition.Key;
            var workflowNodes = workflows[workflowName];

            foreach (var targetDefinition in workflowDefinition.Value)
            {
                var targetName = new NodeName(targetDefinition.Key);
                var targetNode = workflowNodes[targetName];

                foreach (var portLinks in targetDefinition.Value.GetAllPortLinks())
                {
                    var targetPortName = new PortName(portLinks.Key);
                    if (!targetNode.Inputs.TryGetValue(targetPortName, out var input))
                    {
                        errors.Add(new(
                            ApplicationRuntimeBuildErrorCode.MissingInputPort,
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
                                ApplicationRuntimeBuildErrorCode.MissingOutputPort,
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
        NodeName sourceName,
        IReadOnlyDictionary<NodeName, RuntimeNode> workflowNodes,
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        out RuntimeNode sourceNode)
    {
        if (workflowNodes.TryGetValue(sourceName, out sourceNode!))
        {
            return true;
        }

        return resources.TryGetValue(sourceName, out sourceNode!);
    }

    private static void DisposeCreatedNodes(
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> workflows,
        List<IDisposable> links)
    {
        using var runtime = new ApplicationRuntime(resources, workflows, links);
    }

    private static IReadOnlyList<RuntimeNode> FindEntryNodes(
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> workflows,
        HashSet<RuntimeNode> linkedTargets)
    {
        var nodes = resources.Values
            .Concat(workflows.Values.SelectMany(workflow => workflow.Values))
            .Where(node => !linkedTargets.Contains(node))
            .ToArray();

        return nodes;
    }
}
