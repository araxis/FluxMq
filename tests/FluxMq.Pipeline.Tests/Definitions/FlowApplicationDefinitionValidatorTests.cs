using Shouldly;
using FluxMq.Pipeline.Definitions;
using System.Text.Json;

namespace FluxMq.Pipeline.Tests.Definitions;

public sealed class FlowApplicationDefinitionValidatorTests
{
    private readonly ApplicationDefinitionValidator _validator = new();

    [Fact]
    public void Validate_AcceptsApplicationWithSharedResourcesAndMultipleWorkflows()
    {
        var definition = new ApplicationDefinition
        {
            Resources =
            {
                ["localBroker"] = Node("mqtt.connection")
            },
            Workflows =
            {
                ["observeTraffic"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger"),
                        ["metrics"] = NodeWithPort("mqtt.metrics", "Input", "\"source.Output\"")
                    }
                },
                ["recordTraffic"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["recorder"] = NodeWithPort("mqtt.recorder", "Connection", "\"$resources.localBroker.Output\"")
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ReportsDefinitionWithoutWorkflows()
    {
        var result = _validator.Validate(new ApplicationDefinition());

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(ApplicationDefinitionValidationErrorCode.EmptyDefinition);
    }

    [Fact]
    public void Validate_ReportsEmptyWorkflow()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["empty"] = new WorkflowDefinition()
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.ShouldContain(error => error.Code == ApplicationDefinitionValidationErrorCode.EmptyWorkflow);
    }

    [Fact]
    public void Validate_ReportsEmptyNodeType()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["node"] = new NodeDefinition
                        {
                            Type = default
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error => error.Code == ApplicationDefinitionValidationErrorCode.EmptyNodeType);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("node");
    }

    [Fact]
    public void Validate_ReportsMissingSourceNode()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["metrics"] = NodeWithPort("mqtt.metrics", "Input", "\"missing.Output\"")
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error => error.Code == ApplicationDefinitionValidationErrorCode.MissingSourceNode);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("metrics");
        error.PortName.ShouldBe("Input");
    }

    [Fact]
    public void Validate_AllowsLinksFromSharedResources()
    {
        var definition = new ApplicationDefinition
        {
            Resources =
            {
                ["broker"] = Node("mqtt.connection")
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = NodeWithPort("mqtt.trigger", "Connection", "\"$resources.broker.Output\"")
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReportsInvalidLinkShape()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["metrics"] = NodeWithPort("mqtt.metrics", "Input", "123")
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidLink);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("metrics");
        error.PortName.ShouldBe("Input");
    }

    [Fact]
    public void Validate_ReportsEmptySourcePort()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger"),
                        ["metrics"] = NodeWithPort("mqtt.metrics", "Input", "\"source.\"")
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.ShouldContain(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidLink);
    }

    [Fact]
    public void Validate_ReportsDuplicateLinks()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger"),
                        ["metrics"] = NodeWithPort("mqtt.metrics", "Input", "[\"source.Output\", \"source.Output\"]")
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error => error.Code == ApplicationDefinitionValidationErrorCode.DuplicateLink);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("metrics");
        error.PortName.ShouldBe("Input");
    }

    private static NodeDefinition Node(string type) => new()
    {
        Type = new NodeType(type)
    };

    private static NodeDefinition NodeWithPort(string type, string portName, string linkJson) => new()
    {
        Type = new NodeType(type),
        Ports =
        {
            [portName] = JsonDocument.Parse(linkJson).RootElement.Clone()
        }
    };
}
