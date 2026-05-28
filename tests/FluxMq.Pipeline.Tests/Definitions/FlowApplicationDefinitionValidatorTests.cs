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
    public void Validate_AcceptsDashboardsAndTestsAsSeparateAppArtifacts()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger")
                    }
                }
            },
            Dashboards =
            {
                ["ops"] = new DashboardDefinition
                {
                    Layout = new DashboardLayoutDefinition
                    {
                        Columns =
                        [
                            DashboardGridTrackDefinition.Fixed(280),
                            DashboardGridTrackDefinition.Star(2),
                            DashboardGridTrackDefinition.Percent(25)
                        ],
                        Rows =
                        [
                            DashboardGridTrackDefinition.Fixed(96),
                            DashboardGridTrackDefinition.Star()
                        ],
                        Cells =
                        {
                            ["messages"] = new DashboardCellDefinition
                            {
                                Row = 0,
                                Column = 0,
                                ColumnSpan = 3,
                                Widget = "messageRate"
                            }
                        }
                    },
                    Widgets =
                    {
                        ["messageRate"] = new DashboardWidgetDefinition { Type = "metric.card" }
                    }
                }
            },
            Tests =
            {
                ["roundTrip"] = new ScenarioDefinition
                {
                    Steps =
                    {
                        ["expect"] = new ScenarioStepDefinition { Type = "expect.event" }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReportsInvalidDashboardLayoutAndMissingWidget()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger")
                    }
                }
            },
            Dashboards =
            {
                ["ops"] = new DashboardDefinition
                {
                    Layout = new DashboardLayoutDefinition
                    {
                        Columns = [],
                        Rows =
                        [
                            DashboardGridTrackDefinition.Fixed(-1)
                        ],
                        ColumnPadding = [-1],
                        RowPadding = [double.PositiveInfinity],
                        Cells =
                        {
                            ["broken"] = new DashboardCellDefinition
                            {
                                Row = -1,
                                Column = 0,
                                Widget = "missing"
                            }
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.ShouldContain(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidDashboardLayout);
        result.Errors.ShouldContain(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidDashboardCell);
        result.Errors.ShouldContain(error => error.Code == ApplicationDefinitionValidationErrorCode.MissingDashboardWidget);
    }

    [Fact]
    public void Validate_ReportsDashboardCellOutsideDefinedTracks()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger")
                    }
                }
            },
            Dashboards =
            {
                ["ops"] = new DashboardDefinition
                {
                    Layout = new DashboardLayoutDefinition
                    {
                        Columns =
                        [
                            DashboardGridTrackDefinition.Star(),
                            DashboardGridTrackDefinition.Star()
                        ],
                        Rows =
                        [
                            DashboardGridTrackDefinition.Fixed(120)
                        ],
                        Cells =
                        {
                            ["overflow"] = new DashboardCellDefinition
                            {
                                Row = 0,
                                Column = 1,
                                RowSpan = 2,
                                ColumnSpan = 2
                            }
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.Count(error => error.Code == ApplicationDefinitionValidationErrorCode.InvalidDashboardCell)
            .ShouldBe(2);
    }

    [Fact]
    public void Validate_ReportsEmptyScenarioStepType()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger")
                    }
                }
            },
            Tests =
            {
                ["roundTrip"] = new ScenarioDefinition
                {
                    Steps =
                    {
                        ["expect"] = new ScenarioStepDefinition()
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.ShouldContain(error => error.Code == ApplicationDefinitionValidationErrorCode.EmptyScenarioStepType);
    }

    [Fact]
    public void Validate_ReportsUnknownScenarioStepType()
    {
        var definition = new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("mqtt.trigger")
                    }
                }
            },
            Tests =
            {
                ["roundTrip"] = new ScenarioDefinition
                {
                    Steps =
                    {
                        ["custom"] = new ScenarioStepDefinition { Type = "custom.step" }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error => error.Code == ApplicationDefinitionValidationErrorCode.UnknownScenarioStepType);
        error.Message.ShouldContain("Test scenario 'roundTrip' step 'custom'");
        error.Message.ShouldContain("custom.step");
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
