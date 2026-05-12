using FluentAssertions;
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
        var result = _validator.Validate(new ApplicationDefinition());

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ApplicationDefinitionValidationErrorCode.EmptyDefinition);
    }

    [Fact]
    public void Validate_ReportsEmptyWorkflow()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["empty"] = []
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == ApplicationDefinitionValidationErrorCode.EmptyWorkflow);
    }

    [Fact]
    public void Validate_ReportsEmptyNodeType()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["node"] = new NodeDefinition
                    {
                        Type = default
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Should().Contain(error => error.Code == ApplicationDefinitionValidationErrorCode.EmptyNodeType);
    }

    [Fact]
    public void Validate_ReportsMissingSourceNode()
    {
        var definition = new ApplicationDefinition
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

        result.Errors.Should().Contain(error => error.Code == ApplicationDefinitionValidationErrorCode.MissingSourceNode);
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
        var definition = new ApplicationDefinition
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

        result.Errors.Should().Contain(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidLink);
    }

    [Fact]
    public void Validate_ReportsEmptySourcePort()
    {
        var definition = new ApplicationDefinition
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

        result.Errors.Should().Contain(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidLink);
    }

    [Fact]
    public void Validate_ReportsDuplicateLinks()
    {
        var definition = new ApplicationDefinition
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

        result.Errors.Should().Contain(error => error.Code == ApplicationDefinitionValidationErrorCode.DuplicateLink);
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
