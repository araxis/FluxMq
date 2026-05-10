using FluentAssertions;
using FluxMq.Pipeline.Definitions;
using System.Text.Json;

namespace FluxMq.Pipeline.Tests.Definitions;

public sealed class FlowApplicationDefinitionValidatorTests
{
    private readonly FlowApplicationDefinitionValidator _validator = new();

    [Fact]
    public void Validate_AcceptsApplicationWithSharedResourcesAndMultipleWorkflows()
    {
        var definition = new FlowApplicationDefinition
        {
            Resources =
            {
                ["localBroker"] = Node("mqtt.connection")
            },
            Workflows =
            {
                ["observeTraffic"] = new()
                {
                    ["source"] = Node("mqtt.trigger"),
                    ["metrics"] = NodeWithPort("mqtt.metrics-sink", "Input", "\"source.Output\"")
                },
                ["recordTraffic"] = new()
                {
                    ["recorder"] = NodeWithPort("mqtt.recording-sink", "Connection", "\"localBroker.Output\"")
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReportsDefinitionWithoutWorkflows()
    {
        var result = _validator.Validate(new FlowApplicationDefinition());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(FlowApplicationDefinitionValidationErrorCode.EmptyDefinition);
    }

    [Fact]
    public void Validate_ReportsEmptyWorkflow()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["empty"] = []
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == FlowApplicationDefinitionValidationErrorCode.EmptyWorkflow);
    }

    [Fact]
    public void Validate_ReportsEmptyNodeType()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["node"] = new FlowNodeDefinition
                    {
                        Type = default
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == FlowApplicationDefinitionValidationErrorCode.EmptyNodeType);
    }

    [Fact]
    public void Validate_ReportsMissingSourceNode()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["metrics"] = NodeWithPort("mqtt.metrics-sink", "Input", "\"missing.Output\"")
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == FlowApplicationDefinitionValidationErrorCode.MissingSourceNode);
    }

    [Fact]
    public void Validate_AllowsLinksFromSharedResources()
    {
        var definition = new FlowApplicationDefinition
        {
            Resources =
            {
                ["broker"] = Node("mqtt.connection")
            },
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = NodeWithPort("mqtt.trigger", "Connection", "\"broker.Output\"")
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReportsInvalidLinkShape()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["metrics"] = NodeWithPort("mqtt.metrics-sink", "Input", "123")
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == FlowApplicationDefinitionValidationErrorCode.InvalidLink);
    }

    [Fact]
    public void Validate_ReportsEmptySourcePort()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("mqtt.trigger"),
                    ["metrics"] = NodeWithPort("mqtt.metrics-sink", "Input", "\"source.\"")
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == FlowApplicationDefinitionValidationErrorCode.InvalidLink);
    }

    [Fact]
    public void Validate_ReportsDuplicateLinks()
    {
        var definition = new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("mqtt.trigger"),
                    ["metrics"] = NodeWithPort("mqtt.metrics-sink", "Input", "[\"source.Output\", \"source.Output\"]")
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == FlowApplicationDefinitionValidationErrorCode.DuplicateLink);
    }

    private static FlowNodeDefinition Node(string type) => new()
    {
        Type = new FlowNodeType(type)
    };

    private static FlowNodeDefinition NodeWithPort(string type, string portName, string linkJson) => new()
    {
        Type = new FlowNodeType(type),
        Ports =
        {
            [portName] = JsonDocument.Parse(linkJson).RootElement.Clone()
        }
    };
}
