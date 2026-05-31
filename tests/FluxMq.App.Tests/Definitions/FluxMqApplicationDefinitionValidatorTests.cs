using Shouldly;
using FluxFlow.Engine.Definitions;
using FluxMq.App.Definitions;
using FluxMq.Scenarios;
using System.Text.Json;

namespace FluxMq.App.Tests.Definitions;

public sealed class FluxMqApplicationDefinitionValidatorTests
{
    private readonly FluxMqApplicationDefinitionValidator _validator = new();

    [Fact]
    public void Validate_AcceptsApplicationWithSharedResourcesAndMultipleWorkflows()
    {
        var definition = new FluxMqApplicationDefinition
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
                        ["trigger"] = Node("mqtt.trigger"),
                        ["mapper"] = NodeWithPort("flow.mapper", "Input", "\"trigger.Output\""),
                        ["recorder"] = NodeWithPort("mqtt.recorder", "Input", "\"mapper.Output\"")
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
        var result = _validator.Validate(new FluxMqApplicationDefinition());

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(FluxMqApplicationDefinitionValidationErrorCode.EmptyDefinition);
    }

    [Fact]
    public void Validate_ReportsEmptyWorkflow()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Workflows =
            {
                ["empty"] = new WorkflowDefinition()
            }
        };

        var result = _validator.Validate(definition);

        result.Errors.ShouldContain(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.EmptyWorkflow);
    }

    [Fact]
    public void Validate_ReportsEmptyNodeType()
    {
        var definition = new FluxMqApplicationDefinition
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

        var error = result.Errors.Single(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.EmptyNodeType);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("node");
    }

    [Fact]
    public void Validate_ReportsMissingSourceNode()
    {
        var definition = new FluxMqApplicationDefinition
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

        var error = result.Errors.Single(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.MissingSourceNode);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("metrics");
        error.PortName.ShouldBe("Input");
    }

    [Fact]
    public void Validate_AllowsLinksFromSharedResources()
    {
        var definition = new FluxMqApplicationDefinition
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
                        ["sink"] = NodeWithPort("test.sink", "Input", "\"$resources.broker.Output\"")
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
        var definition = new FluxMqApplicationDefinition
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
        var definition = new FluxMqApplicationDefinition
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

        result.Errors.ShouldContain(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardLayout);
        result.Errors.ShouldContain(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardCell);
        result.Errors.ShouldContain(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.MissingDashboardWidget);
    }

    [Fact]
    public void Validate_ReportsDashboardCellOutsideDefinedTracks()
    {
        var definition = new FluxMqApplicationDefinition
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

        result.Errors.Count(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidDashboardCell)
            .ShouldBe(2);
    }

    [Fact]
    public void Validate_ReportsEmptyScenarioStepType()
    {
        var definition = new FluxMqApplicationDefinition
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

        result.Errors.ShouldContain(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.EmptyScenarioStepType);
    }

    [Fact]
    public void Validate_ReportsUnknownScenarioStepType()
    {
        var definition = new FluxMqApplicationDefinition
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

        var error = result.Errors.Single(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.UnknownScenarioStepType);
        error.Message.ShouldContain("Test scenario 'roundTrip' step 'custom'");
        error.Message.ShouldContain("custom.step");
    }

    [Fact]
    public void Validate_AcceptsMqttPublisherScenarioStepWithAppConnection()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Resources =
            {
                ["local-broker"] = Node("mqtt.connection")
            },
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
                        ["publish"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.MqttPublisher,
                            Configuration = Config(
                                ("connection", "local-broker"),
                                ("topic", "fluxmq/sample/request"),
                                ("payloadEncoding", "json"),
                                ("payload", new { value = 12 }),
                                ("qos", 1),
                                ("retain", false))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AcceptsMqttTriggerScenarioStepWithAppConnection()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Resources =
            {
                ["local-broker"] = Node("mqtt.connection")
            },
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
                        ["trigger"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.MqttTrigger,
                            Configuration = Config(
                                ("connection", "local-broker"),
                                ("subscriptions", "fluxmq/sample/#"),
                                ("qos", 1),
                                ("receiveRetained", false),
                                ("retainAsPublished", true))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReportsMqttPublisherScenarioStepMissingConnectionResource()
    {
        var definition = new FluxMqApplicationDefinition
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
                        ["publish"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.MqttPublisher,
                            Configuration = Config(
                                ("connection", "missing-broker"),
                                ("topic", "fluxmq/sample/request"))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.MissingScenarioStepResource);
        error.Message.ShouldContain("roundTrip");
        error.Message.ShouldContain("publish");
        error.Message.ShouldContain("missing-broker");
    }

    [Fact]
    public void Validate_ReportsInvalidMqttPublisherScenarioStepConfiguration()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Resources =
            {
                ["local-broker"] = Node("mqtt.connection")
            },
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
                        ["publish"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.MqttPublisher,
                            Configuration = Config(
                                ("connection", "local-broker"),
                                ("topic", "fluxmq/sample/request"),
                                ("payloadEncoding", "base64"),
                                ("payload", "not-base64"),
                                ("qos", 3),
                                ("retain", "false"))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var errors = result.Errors
            .Where(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration)
            .Select(error => error.Message)
            .ToArray();
        errors.ShouldContain(message => message.Contains("payload", StringComparison.Ordinal));
        errors.ShouldContain(message => message.Contains("qos", StringComparison.Ordinal));
        errors.ShouldContain(message => message.Contains("retain", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsMqttPublisherScenarioStepPayloadEncodingOutsideCatalog()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Resources =
            {
                ["local-broker"] = Node("mqtt.connection")
            },
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
                        ["publish"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.MqttPublisher,
                            Configuration = Config(
                                ("connection", "local-broker"),
                                ("topic", "fluxmq/sample/request"),
                                ("payloadEncoding", "utf8"),
                                ("payload", "hello"))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var error = result.Errors.Single(error =>
            error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration);
        error.Message.ShouldContain("payloadEncoding");
        error.Message.ShouldContain("json");
        error.Message.ShouldContain("bytes");
    }

    [Fact]
    public void Validate_ReportsInvalidMqttTriggerScenarioStepConfiguration()
    {
        var definition = new FluxMqApplicationDefinition
        {
            Resources =
            {
                ["local-broker"] = Node("mqtt.connection")
            },
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
                        ["trigger"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.MqttTrigger,
                            Configuration = Config(
                                ("connection", "local-broker"),
                                ("subscriptions", ""),
                                ("qos", 3),
                                ("receiveRetained", "false"),
                                ("retainAsPublished", "true"))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var errors = result.Errors
            .Where(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration)
            .Select(error => error.Message)
            .ToArray();
        errors.ShouldContain(message => message.Contains("subscriptions", StringComparison.Ordinal));
        errors.ShouldContain(message => message.Contains("qos", StringComparison.Ordinal));
        errors.ShouldContain(message => message.Contains("receiveRetained", StringComparison.Ordinal));
        errors.ShouldContain(message => message.Contains("retainAsPublished", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsInvalidExpectEventScenarioStepConfiguration()
    {
        var definition = new FluxMqApplicationDefinition
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
                        ["expect"] = new ScenarioStepDefinition
                        {
                            Type = ScenarioStepTypes.ExpectEvent,
                            Configuration = Config(
                                ("timeoutMs", 0),
                                ("attributes", new { nested = new { value = 1 } }))
                        }
                    }
                }
            }
        };

        var result = _validator.Validate(definition);

        var errors = result.Errors
            .Where(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidScenarioStepConfiguration)
            .Select(error => error.Message)
            .ToArray();
        errors.ShouldContain(message => message.Contains("timeoutMs", StringComparison.Ordinal));
        errors.ShouldContain(message => message.Contains("attributes.nested", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsInvalidLinkShape()
    {
        var definition = new FluxMqApplicationDefinition
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

        var error = result.Errors.Single(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidLink);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName.ShouldBe("metrics");
        error.PortName.ShouldBe("Input");
    }

    [Fact]
    public void Validate_ReportsEmptySourcePort()
    {
        var definition = new FluxMqApplicationDefinition
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

        result.Errors.ShouldContain(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.InvalidLink);
    }

    [Fact]
    public void Validate_ReportsDuplicateLinks()
    {
        var definition = new FluxMqApplicationDefinition
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

        var error = result.Errors.Single(error => error.Code == FluxMqApplicationDefinitionValidationErrorCode.DuplicateLink);
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

    private static Dictionary<string, JsonElement> Config(params (string Key, object? Value)[] values)
    {
        var configuration = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            configuration[key] = JsonSerializer.SerializeToElement(value);
        }

        return configuration;
    }
}
