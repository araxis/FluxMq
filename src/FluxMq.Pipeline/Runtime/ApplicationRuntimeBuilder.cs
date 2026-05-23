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
                        error.Message,
                        error.WorkflowName,
                        ToNodeName(error.NodeName),
                        ToPortName(error.PortName)))
                    .ToArray());
        }

        var errors = new List<ApplicationRuntimeBuildError>();
        var workflowLinks = new Dictionary<string, List<IDisposable>>();
        var linkedTargets = new HashSet<RuntimeNode>();

        var resourceNodes = CreateNodes(null, definition.Resources, errors);
        var workflowNodes = definition.Workflows.ToDictionary(
            workflow => workflow.Key,
            workflow => (IReadOnlyDictionary<NodeName, RuntimeNode>)CreateNodes(workflow.Key, workflow.Value.Nodes, errors, resourceNodes));

        if (errors.Count == 0)
        {
            foreach (var key in workflowNodes.Keys)
                workflowLinks[key] = [];

            LinkWorkflows(definition, resourceNodes, workflowNodes, workflowLinks, linkedTargets, errors);
        }

        if (errors.Count > 0)
        {
            DisposeCreatedNodes(resourceNodes, workflowNodes, workflowLinks.Values.SelectMany(l => l).ToList());
            return ApplicationRuntimeBuildResult.Failed(validation, errors);
        }

        var resources = resourceNodes.Values.ToArray();
        var workflows = workflowNodes
            .Select(kvp =>
            {
                var nodes = kvp.Value.Values.ToArray();
                var entryNodes = nodes.Where(n => !linkedTargets.Contains(n)).ToArray();
                return new Workflow(new WorkflowName(kvp.Key), nodes, workflowLinks[kvp.Key], entryNodes);
            })
            .ToArray();
        var resourceEntryNodes = resources.Where(n => !linkedTargets.Contains(n)).ToArray();

        return ApplicationRuntimeBuildResult.Succeeded(
            new ApplicationRuntime(resources, workflows, resourceEntryNodes),
            validation);
    }

    private IReadOnlyDictionary<NodeName, RuntimeNode> CreateNodes(
        string? workflowName,
        IReadOnlyDictionary<string, NodeDefinition> definitions,
        List<ApplicationRuntimeBuildError> errors,
        IReadOnlyDictionary<NodeName, RuntimeNode>? resources = null)
    {
        var nodes = new Dictionary<NodeName, RuntimeNode>();
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
                var runtimeNode = factory(new RuntimeNodeFactoryContext(
                    nodeName,
                    definition.Value,
                    workflowName,
                    resourceView));
                nodes.Add(nodeName, runtimeNode with { Phase = definition.Value.Phase });
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
        Dictionary<string, List<IDisposable>> workflowLinks,
        HashSet<RuntimeNode> linkedTargets,
        List<ApplicationRuntimeBuildError> errors)
    {
        foreach (var workflowDefinition in definition.Workflows)
        {
            var workflowName = workflowDefinition.Key;
            var workflowNodes = workflows[workflowName];
            var links = workflowLinks[workflowName];

            foreach (var targetDefinition in workflowDefinition.Value.Nodes)
            {
                var targetName = new NodeName(targetDefinition.Key);
                var targetNode = workflowNodes[targetName];

                foreach (var portLinks in targetDefinition.Value.GetAllPortLinks(workflowName))
                {
                    var targetPortName = new PortName(portLinks.Key);
                    var input = targetNode.FindInput(targetPortName);
                    if (input is null)
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
                        if (!TryFindSource(link.From, workflows, resources, out var sourceNode))
                        {
                            continue;
                        }

                        var output = sourceNode.FindOutput(link.From.Port);
                        if (output is null)
                        {
                            errors.Add(new(
                                ApplicationRuntimeBuildErrorCode.MissingOutputPort,
                                $"Node '{sourceNode.Address}' does not expose output port '{link.From.Port}'.",
                                workflowName,
                                sourceNode.Address.Node,
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
        PortAddress source,
        IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> workflows,
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        out RuntimeNode sourceNode)
    {
        IReadOnlyDictionary<NodeName, RuntimeNode>? scope = source.Scope == WellKnownScopes.Resources
            ? resources
            : workflows.GetValueOrDefault(source.Scope);

        if (scope is null)
        {
            sourceNode = null!;
            return false;
        }

        return scope.TryGetValue(source.Node, out sourceNode!);
    }

    private static void DisposeCreatedNodes(
        IReadOnlyDictionary<NodeName, RuntimeNode> resources,
        IReadOnlyDictionary<string, IReadOnlyDictionary<NodeName, RuntimeNode>> workflows,
        List<IDisposable> links)
    {
        foreach (var link in links)
            link.Dispose();

        foreach (var disposable in workflows.Values.SelectMany(wf => wf.Values).Select(n => n.Node).OfType<IDisposable>())
            disposable.Dispose();

        foreach (var disposable in resources.Values.Select(n => n.Node).OfType<IDisposable>())
            disposable.Dispose();
    }

    private static NodeName? ToNodeName(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new NodeName(value);

    private static PortName? ToPortName(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new PortName(value);
}
