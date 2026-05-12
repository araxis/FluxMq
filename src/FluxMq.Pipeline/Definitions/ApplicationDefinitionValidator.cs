namespace FluxMq.Pipeline.Definitions;

public sealed class ApplicationDefinitionValidator
{
    public ApplicationDefinitionValidationResult Validate(ApplicationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<ApplicationDefinitionValidationError>();

        if (definition.Workflows.Count == 0)
        {
            errors.Add(new(
                ApplicationDefinitionValidationErrorCode.EmptyDefinition,
                "Flow application definition must contain at least one workflow."));
        }

        foreach (var resource in definition.Resources)
        {
            if (string.IsNullOrWhiteSpace(resource.Key))
            {
                errors.Add(new(
                    ApplicationDefinitionValidationErrorCode.EmptyResourceName,
                    "Resource name cannot be empty."));
            }

            ValidateNode(resource.Key, resource.Value, errors);
        }

        foreach (var workflow in definition.Workflows)
        {
            if (string.IsNullOrWhiteSpace(workflow.Key))
            {
                errors.Add(new(
                    ApplicationDefinitionValidationErrorCode.EmptyWorkflowName,
                    "Workflow name cannot be empty."));
            }

            if (workflow.Value.Count == 0)
            {
                errors.Add(new(
                    ApplicationDefinitionValidationErrorCode.EmptyWorkflow,
                    $"Workflow '{workflow.Key}' must contain at least one node."));
            }

            var knownNodeNames = definition.Resources.Keys
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var node in workflow.Value)
            {
                if (!string.IsNullOrWhiteSpace(node.Key))
                {
                    knownNodeNames.Add(node.Key);
                }

                ValidateNode(node.Key, node.Value, errors);
            }

            ValidateLinks(workflow.Key, workflow.Value, knownNodeNames, errors);
        }

        return new ApplicationDefinitionValidationResult(errors);
    }

    private static void ValidateNode(
        string nodeName,
        NodeDefinition node,
        List<ApplicationDefinitionValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            errors.Add(new(
                ApplicationDefinitionValidationErrorCode.EmptyNodeName,
                "Flow node name cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(node.Type.Value))
        {
            errors.Add(new(
                ApplicationDefinitionValidationErrorCode.EmptyNodeType,
                $"Flow node '{nodeName}' has an empty node type."));
        }
    }

    private static void ValidateLinks(
        string workflowName,
        IReadOnlyDictionary<string, NodeDefinition> nodes,
        IReadOnlySet<string> knownNodeNames,
        List<ApplicationDefinitionValidationError> errors)
    {
        var knownLinks = new HashSet<LinkKey>();

        foreach (var targetNode in nodes)
        {
            foreach (var port in targetNode.Value.Ports)
            {
                if (string.IsNullOrWhiteSpace(port.Key))
                {
                    errors.Add(new(
                        ApplicationDefinitionValidationErrorCode.EmptyTargetPort,
                        $"Node '{targetNode.Key}' in workflow '{workflowName}' has an empty target port."));
                }

                IReadOnlyList<LinkDefinition> links;

                try
                {
                    links = targetNode.Value.GetPortLinks(port.Key);
                }
                catch (Exception exception) when (exception is FormatException or System.Text.Json.JsonException)
                {
                    errors.Add(new(
                        ApplicationDefinitionValidationErrorCode.InvalidLink,
                        $"Node '{targetNode.Key}' port '{port.Key}' in workflow '{workflowName}' has an invalid link: {exception.Message}"));
                    continue;
                }

                foreach (var link in links)
                {
                    if (string.IsNullOrWhiteSpace(link.From.Port.Value))
                    {
                        errors.Add(new(
                            ApplicationDefinitionValidationErrorCode.EmptySourcePort,
                            $"Node '{targetNode.Key}' port '{port.Key}' in workflow '{workflowName}' has an empty source port."));
                    }

                    if (!knownNodeNames.Contains(link.From.Node.Value))
                    {
                        errors.Add(new(
                            ApplicationDefinitionValidationErrorCode.MissingSourceNode,
                            $"Node '{targetNode.Key}' port '{port.Key}' in workflow '{workflowName}' references missing source node '{link.From.Node}'."));
                    }

                    if (!knownLinks.Add(LinkKey.From(targetNode.Key, port.Key, link)))
                    {
                        errors.Add(new(
                            ApplicationDefinitionValidationErrorCode.DuplicateLink,
                            $"Node '{targetNode.Key}' port '{port.Key}' in workflow '{workflowName}' has a duplicate link from '{link.From}'."));
                    }
                }
            }
        }
    }

    private sealed record LinkKey(
        string TargetNode,
        string TargetPort,
        string SourceNode,
        string SourcePort,
        string? When)
    {
        public static LinkKey From(string targetNode, string targetPort, LinkDefinition link)
            => new(
                targetNode,
                targetPort,
                link.From.Node.Value,
                link.From.Port.Value,
                link.When);
    }
}
