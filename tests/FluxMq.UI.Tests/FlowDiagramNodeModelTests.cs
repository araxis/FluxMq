using Shouldly;
using FluxMq.UI.Components.Diagram;
using FluxMq.UI.Components.Workspace.Nodes.ConditionRouter;
using FluxMq.UI.Components.Workspace.Nodes.ConnectionStateTrigger;
using FluxMq.UI.Components.Workspace.Nodes.DynamicMapper;
using FluxMq.UI.Components.Workspace.Nodes.FlowAssertion;
using FluxMq.UI.Components.Workspace.Nodes.Http;
using FluxMq.UI.Components.Workspace.Nodes.JsonSchemaValidator;
using FluxMq.UI.Components.Workspace.Nodes.MessageFilter;
using FluxMq.UI.Components.Workspace.Nodes.MetricNode;
using FluxMq.UI.Components.Workspace.Nodes.PayloadInspector;
using FluxMq.UI.Components.Workspace.Nodes.Payloads;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Components.Workspace.Nodes.StateReducer;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Blazor.Diagrams.Core.Models;
using System.Text.Json.Nodes;
using DiagramPoint = Blazor.Diagrams.Core.Geometry.Point;

namespace FluxMq.UI.Tests;

public sealed class FlowDiagramNodeModelTests
{
    [Fact]
    public void SetActivity_StoresLatestActivityText()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.SetActivity("Connected | 3 messages");

        model.ActivityText.ShouldBe("Connected | 3 messages");
    }

    [Fact]
    public void SetDiagnostics_StoresNodeDiagnosticsAndPrimarySeverity()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var warning = new WorkspaceDiagnostic("Warning", "Definition", "Check", "Check this node.", "workflow1", "source");
        var error = new WorkspaceDiagnostic("Error", "RuntimeBuild", "FactoryFailed", "Factory failed.", "workflow1", "source");

        model.SetDiagnostics([warning, error]);

        model.Diagnostics.ShouldBe([warning, error]);
        model.PrimaryDiagnostic.ShouldBe(error);

        model.SetDiagnostics([]);
        model.Diagnostics.ShouldBeEmpty();
        model.PrimaryDiagnostic.ShouldBeNull();
    }

    [Fact]
    public void NewNode_DefaultsToCollapsed()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.IsCollapsed.ShouldBeTrue();
    }

    [Fact]
    public void Toggle_SwitchesBetweenCollapsedAndExpandedState()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        // Default: collapsed.
        model.Toggle();
        model.IsCollapsed.ShouldBeFalse();

        model.Toggle();
        model.IsCollapsed.ShouldBeTrue();
    }

    [Fact]
    public void SetCollapsed_AppliesRequestedState()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        model.SetCollapsed(true);
        model.IsCollapsed.ShouldBeTrue();

        model.SetCollapsed(false);
        model.IsCollapsed.ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_PreservesDescriptorDirectionIndependentlyOfSide()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var leftOutput = new FlowPortModel(model, PortAlignment.Left, "Output", representsInput: false);
        var rightInput = new FlowPortModel(model, PortAlignment.Right, "Input", representsInput: true);

        leftOutput.RepresentsInput.ShouldBeFalse();
        rightInput.RepresentsInput.ShouldBeTrue();
    }

    [Fact]
    public void FlowPortModel_RejectsSameDirectionLinks()
    {
        var first = new FlowDiagramNodeModel(
            "workflow1.first",
            new DiagramPoint(10, 20),
            "first",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);
        var second = new FlowDiagramNodeModel(
            "workflow1.second",
            new DiagramPoint(40, 20),
            "second",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var firstOutput = new FlowPortModel(first, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");
        var secondOutput = new FlowPortModel(second, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");

        firstOutput.CanAttachTo(secondOutput).ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_RejectsMismatchedValueTypes()
    {
        var source = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);
        var publisher = new FlowDiagramNodeModel(
            "workflow1.publisher",
            new DiagramPoint(40, 20),
            "publisher",
            "mqtt.publisher",
            descriptor: null,
            isResource: false);

        var output = new FlowPortModel(source, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");
        var input = new FlowPortModel(publisher, PortAlignment.Left, "Input", representsInput: true, valueType: "MqttPublishRequest");

        output.CanCarryValueTo(input).ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_FlagsErrorPortsForDistinctStyling()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var errors = new FlowPortModel(model, PortAlignment.Right, "Errors", representsInput: false, valueType: "FlowError");
        var output = new FlowPortModel(model, PortAlignment.Right, "Output", representsInput: false, valueType: "MqttEnvelope");

        errors.IsErrorPort.ShouldBeTrue();
        output.IsErrorPort.ShouldBeFalse();
    }

    [Fact]
    public void DynamicMapperNodeModel_UsesConfiguredOutputTypeForPortType()
    {
        var descriptor = new ComponentPortDescriptor("Output", "Configured output type", IsInput: false);
        var model = new DynamicMapperNodeModel(
            "workflow1.mapper",
            new DiagramPoint(10, 20),
            "mapper",
            descriptor: null,
            isResource: false)
        {
            OutputContract = DynamicMapperNodeModel.OutputContractTyped,
            OutputType = "FileWriteRequest"
        };

        model.ResolvePortValueType(descriptor).ShouldBe("FileWriteRequest");
    }

    [Fact]
    public void DynamicMapperNodeModel_NormalizesAndPersistsExistingConfiguration()
    {
        var model = new DynamicMapperNodeModel(
            "workflow1.mapper",
            new DiagramPoint(10, 20),
            "mapper",
            descriptor: null,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["engine"] = "jsonata",
            ["inputType"] = "UnknownInput",
            ["outputType"] = "FileWriteRequest",
            ["outputContract"] = "unknown-contract",
            ["outputSchemaPath"] = "schemas/file-write.json",
            ["expression"] = "   "
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "engine",
            "inputType",
            "outputType",
            "outputContract",
            "outputSchemaPath",
            "expression"
        ], ignoreOrder: true);
        config["engine"]!.GetValue<string>().ShouldBe("jsonata");
        config["inputType"]!.GetValue<string>().ShouldBe("MqttEnvelope");
        config["outputType"]!.GetValue<string>().ShouldBe("FileWriteRequest");
        config["outputContract"]!.GetValue<string>().ShouldBe(DynamicMapperNodeModel.OutputContractTyped);
        config["outputSchemaPath"]!.GetValue<string>().ShouldBe("schemas/file-write.json");
        config["expression"]!.GetValue<string>().ShouldBe(
            DynamicMapperNodeModel.DefaultExpression("FileWriteRequest", "jsonata", "MqttEnvelope"));
    }

    [Fact]
    public void JsonSchemaValidatorNodeModel_PersistsInlineAndFileSchemaConfigurations()
    {
        var model = new JsonSchemaValidatorNodeModel(
            "workflow1.validator",
            new DiagramPoint(10, 20),
            "validator",
            descriptor: null,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["schemaId"] = " payload-object ",
            ["schema"] = """
                {
                  "type": "object",
                  "required": ["topic"]
                }
                """
        });

        var inlineConfig = model.BuildConfiguration();

        inlineConfig.Select(static item => item.Key).ShouldBe([
            "schemaId",
            "schema"
        ], ignoreOrder: true);
        inlineConfig["schemaId"]!.GetValue<string>().ShouldBe("payload-object");
        inlineConfig["schema"]!.GetValue<string>().ShouldContain("\"required\": [\"topic\"]");
        inlineConfig.ContainsKey("schemaPath").ShouldBeFalse();

        model.LoadConfiguration(new JsonObject
        {
            ["schemaId"] = " file-schema ",
            ["schemaPath"] = " schemas/payload.json "
        });

        var fileConfig = model.BuildConfiguration();

        fileConfig.Select(static item => item.Key).ShouldBe([
            "schemaId",
            "schemaPath"
        ], ignoreOrder: true);
        fileConfig["schemaId"]!.GetValue<string>().ShouldBe("file-schema");
        fileConfig["schemaPath"]!.GetValue<string>().ShouldBe("schemas/payload.json");
        fileConfig.ContainsKey("schema").ShouldBeFalse();
    }

    [Fact]
    public void MessageFilterNodeModel_PersistsPatternsAndOptionalExpression()
    {
        var model = new MessageFilterNodeModel(
            "workflow1.filter",
            new DiagramPoint(10, 20),
            "filter",
            descriptor: null,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["patterns"] = new JsonArray("factory/#", "$SYS/#", ""),
            ["expression"] = "topic.StartsWith(\"factory/\")"
        });

        var expressionConfig = model.BuildConfiguration();

        expressionConfig.Select(static item => item.Key).ShouldBe([
            "patterns",
            "expression"
        ], ignoreOrder: true);
        expressionConfig["patterns"]!.AsArray().Select(static item => item!.GetValue<string>()).ShouldBe([
            "factory/#",
            "$SYS/#"
        ]);
        expressionConfig["expression"]!.GetValue<string>().ShouldBe("topic.StartsWith(\"factory/\")");

        model.Expression = string.Empty;

        var patternConfig = model.BuildConfiguration();

        patternConfig.Select(static item => item.Key).ShouldBe([
            "patterns"
        ], ignoreOrder: true);
        patternConfig["patterns"]!.AsArray().Select(static item => item!.GetValue<string>()).ShouldBe([
            "factory/#",
            "$SYS/#"
        ]);
        patternConfig.ContainsKey("expression").ShouldBeFalse();
    }

    [Fact]
    public void FlowPortModel_AnchorsLinksToVisibleHandleCenter()
    {
        var model = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);

        var port = new FlowPortModel(model, PortAlignment.Left, "Input")
        {
            Initialized = true,
            Position = new DiagramPoint(100, 200),
            Size = new Blazor.Diagrams.Core.Geometry.Size(12, 12)
        };

        port.GetShape().GetPointAtAngle(180).ShouldBe(new DiagramPoint(106, 206));
        port.GetShape().GetPointAtAngle(0).ShouldBe(new DiagramPoint(106, 206));
    }

    [Fact]
    public void FlowLinkVisuals_BuildsCompactConditionLabel()
    {
        var label = FlowLinkVisuals.ConditionLabel(
            " input.Topic.StartsWith(\"factory/\") \r\n && input.Payload.Length > 0 ");

        label.ShouldBe("when: input.Topic.StartsWith(\"factory/\") && input.Payload.Length > 0");
    }

    [Fact]
    public void FlowLinkVisuals_TruncatesLongConditionLabel()
    {
        var label = FlowLinkVisuals.ConditionLabel(new string('x', 100)).ShouldNotBeNull();

        label.ShouldStartWith("when: ");
        label.ShouldEndWith("...");
        label.Length.ShouldBe(78);
    }

    [Fact]
    public void FlowLinkVisuals_PrefersConditionStyleOverErrorStyle()
    {
        FlowLinkVisuals.ColorFor(hasCondition: true, isError: true)
            .ShouldBe(FlowLinkVisuals.ConditionalColor);
        FlowLinkVisuals.WidthFor(hasCondition: true)
            .ShouldBe(FlowLinkVisuals.ConditionalWidth);
    }

    [Fact]
    public void FlowDiagramLinkModel_TogglesSelectionStyle()
    {
        var source = new FlowDiagramNodeModel(
            "workflow1.source",
            new DiagramPoint(10, 20),
            "source",
            "mqtt.trigger",
            descriptor: null,
            isResource: false);
        var target = new FlowDiagramNodeModel(
            "workflow1.target",
            new DiagramPoint(220, 20),
            "target",
            "mqtt.publisher",
            descriptor: null,
            isResource: false);
        var sourcePort = new FlowPortModel(source, PortAlignment.Right, "Output", representsInput: false);
        var targetPort = new FlowPortModel(target, PortAlignment.Left, "Input", representsInput: true);
        var link = new FlowDiagramLinkModel(sourcePort, targetPort);

        link.SetNormalStyle(FlowLinkVisuals.ConditionalColor, FlowLinkVisuals.ConditionalWidth);
        link.ApplySelectionStyle(selected: true);

        link.Color.ShouldBe(FlowLinkVisuals.ConditionalColor);
        link.Width.ShouldBe(FlowLinkVisuals.SelectedWidth);

        link.ApplySelectionStyle(selected: false);

        link.Color.ShouldBe(FlowLinkVisuals.ConditionalColor);
        link.Width.ShouldBe(FlowLinkVisuals.ConditionalWidth);
    }

    [Fact]
    public void FlowNodeModelFactory_CreatesMetricsNodeModel()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.metrics").ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            "workflow1.metrics",
            new DiagramPoint(10, 20),
            "metrics",
            "mqtt.metrics",
            descriptor,
            isResource: false);

        model.NodeType.ShouldBe("mqtt.metrics");
        model.DisplayName.ShouldBe("MQTT Metrics");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.IsInput);
    }

    [Fact]
    public void FlowNodeModelFactory_CreatesLegacyPayloadInspectorNodeModel()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.payload-inspector").ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            "workflow1.inspect",
            new DiagramPoint(10, 20),
            "inspect",
            "mqtt.payload-inspector",
            descriptor,
            isResource: false);

        model.ShouldBeOfType<PayloadInspectorNodeModel>();
        model.NodeType.ShouldBe("mqtt.payload-inspector");
        model.DisplayName.ShouldBe("Payload Inspector");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.ValueType == "MqttEnvelope");
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && port.ValueType == "InspectedMqttMessage");
    }

    [Fact]
    public void FlowNodeModelFactory_CreatesStateReducerNodeModel()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("state.reducer").ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            "workflow1.stateReducer",
            new DiagramPoint(10, 20),
            "stateReducer",
            "state.reducer",
            descriptor,
            isResource: false);

        model.ShouldBeOfType<StateReducerNodeModel>();
        model.NodeType.ShouldBe("state.reducer");
        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.ValueType == "StateReducerInput");
    }

    [Fact]
    public void ConnectionStateTriggerNodeModel_PersistsConnectionConfiguration()
    {
        var model = new ConnectionStateTriggerNodeModel(
            "workflow1.connectionState",
            new DiagramPoint(10, 20),
            "connectionState",
            descriptor: null,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["connection"] = " local-broker "
        });

        var configured = model.BuildConfiguration();

        configured.Select(static item => item.Key).ShouldBe([
            "connection"
        ]);
        configured["connection"]!.GetValue<string>().ShouldBe("local-broker");

        model.LoadConfiguration(new JsonObject
        {
            ["connection"] = ""
        });

        var fallback = model.BuildConfiguration();

        fallback.Select(static item => item.Key).ShouldBe([
            "connection"
        ]);
        fallback["connection"]!.GetValue<string>().ShouldBe(FlowDefinitionComposer.BrokerResourceName);
    }

    [Theory]
    [InlineData("flow.switch", typeof(RoutingSwitchNodeModel))]
    [InlineData("flow.correlation", typeof(RoutingCorrelationNodeModel))]
    [InlineData("flow.window", typeof(RoutingWindowNodeModel))]
    [InlineData("flow.join", typeof(RoutingJoinNodeModel))]
    [InlineData("flow.fork", typeof(RoutingForkNodeModel))]
    [InlineData("flow.merge", typeof(RoutingMergeNodeModel))]
    public void FlowNodeModelFactory_CreatesRoutingNodeModels(string nodeType, Type expectedType)
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(nodeType).ShouldNotBeNull();

        var model = FlowNodeModelFactory.Create(
            $"workflow1.{nodeType}",
            new DiagramPoint(10, 20),
            "routing",
            nodeType,
            descriptor,
            isResource: false);

        model.ShouldBeOfType(expectedType);
    }

    [Fact]
    public void ConditionRouterNodeModel_PersistsConfigurationAndConfiguredPorts()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("flow.when").ShouldNotBeNull();
        var model = new ConditionRouterNodeModel(
            "workflow1.when",
            new DiagramPoint(10, 20),
            "when",
            descriptor,
            isResource: false)
        {
            InputType = "NumberMetricReading",
            Expression = " value > 50 "
        };

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("NumberMetricReading");
        config["expression"]!.GetValue<string>().ShouldBe("value > 50");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(1000);

        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("NumberMetricReading");
        model.ResolvePortValueType(new ComponentPortDescriptor("WhenTrue", "Configured input type", IsInput: false))
            .ShouldBe("NumberMetricReading");
        model.ResolvePortValueType(new ComponentPortDescriptor("WhenFalse", "Configured input type", IsInput: false))
            .ShouldBe("NumberMetricReading");
    }

    [Fact]
    public void ConditionRouterNodeModel_NormalizesInvalidInputAndBlankExpression()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("flow.when").ShouldNotBeNull();
        var model = new ConditionRouterNodeModel(
            "workflow1.when",
            new DiagramPoint(10, 20),
            "when",
            descriptor,
            isResource: false)
        {
            InputType = "bad",
            Expression = " "
        };

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("MqttEnvelope");
        config["expression"]!.GetValue<string>().ShouldBe("qos >= 1");

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "NumberMetricReading",
            ["expression"] = ""
        });

        model.InputType.ShouldBe("NumberMetricReading");
        model.Expression.ShouldBe("value > 10");
        model.BuildConfiguration()["expression"]!.GetValue<string>().ShouldBe("value > 10");
    }

    [Fact]
    public void RoutingSwitchNodeModel_BuildsRoutesAndPorts()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(RoutingNodeTypes.Switch).ShouldNotBeNull();
        var model = new RoutingSwitchNodeModel(
            "workflow1.switch",
            new DiagramPoint(10, 20),
            "switch",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "MqttPublishRequest",
            ["expression"] = "retain == true",
            ["routes"] = new JsonArray("True", "False"),
            ["routeOutputs"] = new JsonObject
            {
                ["True"] = "Retained",
                ["False"] = "Live"
            },
            ["emitRouteEnvelope"] = true,
            ["boundedCapacity"] = 64
        });

        model.PortDescriptors.ShouldContain(port => port.Name == "Retained" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Live" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Routed" && !port.IsInput);
        model.ResolvePortValueType(new ComponentPortDescriptor("Retained", "Configured input type", IsInput: false))
            .ShouldBe("MqttPublishRequest");

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("MqttPublishRequest");
        config["expression"]!.GetValue<string>().ShouldBe("retain == true");
        config["emitRouteEnvelope"]!.GetValue<bool>().ShouldBeTrue();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
        config["routeOutputs"]!.AsObject()["True"]!.GetValue<string>().ShouldBe("Retained");
    }

    [Fact]
    public void RoutingSwitchNodeModel_NormalizesBlankRoutesAndExpression()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find(RoutingNodeTypes.Switch).ShouldNotBeNull();
        var model = new RoutingSwitchNodeModel(
            "workflow1.switch",
            new DiagramPoint(10, 20),
            "switch",
            descriptor,
            isResource: false)
        {
            InputType = "bad",
            Expression = " ",
            Routes = [],
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe(RoutingSwitchNodeModel.DefaultInputType);
        config["expression"]!.GetValue<string>().ShouldBe(RoutingSwitchNodeModel.DefaultExpression);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(RoutingSwitchNodeModel.DefaultBoundedCapacity);
        config["routes"]!.AsArray().Select(static node => node!.GetValue<string>()).ShouldBe(["True", "False"]);
        config["routeOutputs"]!.AsObject()["True"]!.GetValue<string>().ShouldBe("WhenTrue");

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "unknown",
            ["expression"] = "",
            ["routes"] = new JsonArray("", "yes"),
            ["routeOutputs"] = new JsonObject
            {
                ["yes"] = "123 bad"
            },
            ["boundedCapacity"] = -5
        });

        model.InputType.ShouldBe(RoutingSwitchNodeModel.DefaultInputType);
        model.Expression.ShouldBe(RoutingSwitchNodeModel.DefaultExpression);
        model.BoundedCapacity.ShouldBe(RoutingSwitchNodeModel.DefaultBoundedCapacity);
        model.Routes.ShouldContain(route => route.Key == "yes" && route.OutputPort == "P123bad");
        model.PortDescriptors.ShouldContain(port => port.Name == "P123bad" && !port.IsInput);
    }

    [Fact]
    public void RoutingFanNodeModels_BuildConfiguredPorts()
    {
        var catalog = new FlowComponentCatalog();
        var fork = new RoutingForkNodeModel(
            "workflow1.fork",
            new DiagramPoint(10, 20),
            "fork",
            catalog.Find(RoutingNodeTypes.Fork).ShouldNotBeNull(),
            isResource: false);
        var merge = new RoutingMergeNodeModel(
            "workflow1.merge",
            new DiagramPoint(10, 20),
            "merge",
            catalog.Find(RoutingNodeTypes.Merge).ShouldNotBeNull(),
            isResource: false);

        fork.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "TimerTick",
            ["outputs"] = new JsonArray("Audit", "Dashboard"),
            ["boundedCapacity"] = 32
        });
        merge.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "TimerTick",
            ["inputs"] = new JsonArray("Primary", "Replay"),
            ["boundedCapacity"] = 16
        });

        fork.PortDescriptors.ShouldContain(port => port.Name == "Audit" && !port.IsInput);
        fork.ResolvePortValueType(new ComponentPortDescriptor("Audit", "Configured input type", IsInput: false))
            .ShouldBe("TimerTick");
        fork.BuildConfiguration()["outputs"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe(["Audit", "Dashboard"]);

        merge.PortDescriptors.ShouldContain(port => port.Name == "Primary" && port.IsInput);
        merge.ResolvePortValueType(new ComponentPortDescriptor("Primary", "Configured input type", IsInput: true))
            .ShouldBe("TimerTick");
        merge.BuildConfiguration()["inputs"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe(["Primary", "Replay"]);
    }

    [Fact]
    public void RoutingFanNodeModels_NormalizePortsAndBuffer()
    {
        var catalog = new FlowComponentCatalog();
        var fork = new RoutingForkNodeModel(
            "workflow1.fork",
            new DiagramPoint(10, 20),
            "fork",
            catalog.Find(RoutingNodeTypes.Fork).ShouldNotBeNull(),
            isResource: false)
        {
            InputType = "bad",
            Outputs = ["Input", "Dashboard View", "Dashboard View", "2nd"],
            BoundedCapacity = 0
        };
        var merge = new RoutingMergeNodeModel(
            "workflow1.merge",
            new DiagramPoint(10, 20),
            "merge",
            catalog.Find(RoutingNodeTypes.Merge).ShouldNotBeNull(),
            isResource: false)
        {
            InputType = "bad",
            Inputs = ["Output", "Left Side", "Left Side", "3rd"],
            BoundedCapacity = -1
        };

        var forkConfig = fork.BuildConfiguration();
        forkConfig["inputType"]!.GetValue<string>().ShouldBe(RoutingForkNodeModel.DefaultInputType);
        forkConfig["outputs"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe(["DashboardView", "P2nd"]);
        forkConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(RoutingForkNodeModel.DefaultBoundedCapacity);

        var mergeConfig = merge.BuildConfiguration();
        mergeConfig["inputType"]!.GetValue<string>().ShouldBe(RoutingMergeNodeModel.DefaultInputType);
        mergeConfig["inputs"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe(["LeftSide", "P3rd"]);
        mergeConfig["boundedCapacity"]!.GetValue<int>().ShouldBe(RoutingMergeNodeModel.DefaultBoundedCapacity);

        fork.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "unknown",
            ["outputs"] = new JsonArray("", "Errors", "Audit Trail"),
            ["boundedCapacity"] = -5
        });
        merge.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "unknown",
            ["inputs"] = new JsonArray("", "Errors", "Replay Stream"),
            ["boundedCapacity"] = -5
        });

        fork.InputType.ShouldBe(RoutingForkNodeModel.DefaultInputType);
        fork.Outputs.ShouldBe(["AuditTrail"]);
        fork.BoundedCapacity.ShouldBe(RoutingForkNodeModel.DefaultBoundedCapacity);
        fork.PortDescriptors.ShouldContain(port => port.Name == "AuditTrail" && !port.IsInput);

        merge.InputType.ShouldBe(RoutingMergeNodeModel.DefaultInputType);
        merge.Inputs.ShouldBe(["ReplayStream"]);
        merge.BoundedCapacity.ShouldBe(RoutingMergeNodeModel.DefaultBoundedCapacity);
        merge.PortDescriptors.ShouldContain(port => port.Name == "ReplayStream" && port.IsInput);
    }

    [Fact]
    public void RoutingWindowNodeModel_BuildsConfigurationAndPorts()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingWindowNodeModel(
            "workflow1.window",
            new DiagramPoint(10, 20),
            "window",
            catalog.Find(RoutingNodeTypes.Window).ShouldNotBeNull(),
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "TimerTick",
            ["maxItems"] = 25,
            ["timeMilliseconds"] = 0,
            ["emitPartialOnCompletion"] = false,
            ["boundedCapacity"] = 64
        });

        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.ValueType == "TimerTick" && port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowWindow" && !port.IsInput);
        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("TimerTick");
        model.ResolvePortValueType(new ComponentPortDescriptor("Output", "FlowWindow", IsInput: false))
            .ShouldBe("FlowWindow");

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("TimerTick");
        config["maxItems"]!.GetValue<int>().ShouldBe(25);
        config["timeMilliseconds"]!.GetValue<int>().ShouldBe(0);
        config["emitPartialOnCompletion"]!.GetValue<bool>().ShouldBeFalse();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
    }

    [Fact]
    public void RoutingWindowNodeModel_NormalizesBoundaryAndBuffer()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingWindowNodeModel(
            "workflow1.window",
            new DiagramPoint(10, 20),
            "window",
            catalog.Find(RoutingNodeTypes.Window).ShouldNotBeNull(),
            isResource: false)
        {
            InputType = "bad",
            MaxItems = 0,
            TimeMilliseconds = 0,
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe(RoutingWindowNodeModel.DefaultInputType);
        config["maxItems"]!.GetValue<int>().ShouldBe(RoutingWindowNodeModel.DefaultMaxItems);
        config["timeMilliseconds"]!.GetValue<int>().ShouldBe(0);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(RoutingWindowNodeModel.DefaultBoundedCapacity);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "unknown",
            ["maxItems"] = -5,
            ["timeMilliseconds"] = -1,
            ["boundedCapacity"] = -10
        });

        model.InputType.ShouldBe(RoutingWindowNodeModel.DefaultInputType);
        model.MaxItems.ShouldBe(RoutingWindowNodeModel.DefaultMaxItems);
        model.TimeMilliseconds.ShouldBe(RoutingWindowNodeModel.DefaultTimeMilliseconds);
        model.BoundedCapacity.ShouldBe(RoutingWindowNodeModel.DefaultBoundedCapacity);
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowWindow" && !port.IsInput);
    }

    [Fact]
    public void RoutingCorrelationNodeModel_BuildsConfigurationAndPorts()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingCorrelationNodeModel(
            "workflow1.correlation",
            new DiagramPoint(10, 20),
            "correlation",
            catalog.Find(RoutingNodeTypes.Correlation).ShouldNotBeNull(),
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "MqttPublishRequest",
            ["keyExpression"] = "topic",
            ["sideExpression"] = "payloadText",
            ["requestSide"] = "request",
            ["responseSide"] = "response",
            ["caseSensitive"] = false,
            ["timeoutMilliseconds"] = 15000,
            ["maxPending"] = 128,
            ["boundedCapacity"] = 64
        });

        model.PortDescriptors.ShouldContain(port => port.Name == "Input" && port.ValueType == "MqttPublishRequest" && port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Matched" && port.ValueType == "FlowCorrelationMatch" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Timeouts" && port.ValueType == "FlowCorrelationTimeout" && !port.IsInput);
        model.ResolvePortValueType(new ComponentPortDescriptor("Input", "Configured input type", IsInput: true))
            .ShouldBe("MqttPublishRequest");
        model.ResolvePortValueType(new ComponentPortDescriptor("Matched", "FlowCorrelationMatch", IsInput: false))
            .ShouldBe("FlowCorrelationMatch");

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe("MqttPublishRequest");
        config["keyExpression"]!.GetValue<string>().ShouldBe("topic");
        config["sideExpression"]!.GetValue<string>().ShouldBe("payloadText");
        config["requestSide"]!.GetValue<string>().ShouldBe("request");
        config["responseSide"]!.GetValue<string>().ShouldBe("response");
        config["caseSensitive"]!.GetValue<bool>().ShouldBeFalse();
        config["timeoutMilliseconds"]!.GetValue<int>().ShouldBe(15000);
        config["maxPending"]!.GetValue<int>().ShouldBe(128);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
    }

    [Fact]
    public void RoutingCorrelationNodeModel_NormalizesConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingCorrelationNodeModel(
            "workflow1.correlation",
            new DiagramPoint(10, 20),
            "correlation",
            catalog.Find(RoutingNodeTypes.Correlation).ShouldNotBeNull(),
            isResource: false)
        {
            InputType = "bad",
            KeyExpression = " ",
            SideExpression = "",
            RequestSide = "same",
            ResponseSide = "same",
            CaseSensitive = true,
            TimeoutMilliseconds = 0,
            MaxPending = 0,
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();
        config["inputType"]!.GetValue<string>().ShouldBe(RoutingCorrelationNodeModel.DefaultInputType);
        config["keyExpression"]!.GetValue<string>().ShouldBe(RoutingCorrelationNodeModel.DefaultKeyExpression);
        config["sideExpression"]!.GetValue<string>().ShouldBe(RoutingCorrelationNodeModel.DefaultSideExpression);
        config["requestSide"]!.GetValue<string>().ShouldBe("same");
        config["responseSide"]!.GetValue<string>().ShouldBe(RoutingCorrelationNodeModel.DefaultResponseSide);
        config["timeoutMilliseconds"]!.GetValue<int>().ShouldBe(RoutingCorrelationNodeModel.DefaultTimeoutMilliseconds);
        config["maxPending"]!.GetValue<int>().ShouldBe(RoutingCorrelationNodeModel.DefaultMaxPending);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(RoutingCorrelationNodeModel.DefaultBoundedCapacity);

        model.LoadConfiguration(new JsonObject
        {
            ["inputType"] = "unknown",
            ["keyExpression"] = "",
            ["sideExpression"] = "",
            ["requestSide"] = "",
            ["responseSide"] = "request",
            ["timeoutMilliseconds"] = -1,
            ["maxPending"] = -2,
            ["boundedCapacity"] = -3
        });

        model.InputType.ShouldBe(RoutingCorrelationNodeModel.DefaultInputType);
        model.KeyExpression.ShouldBe(RoutingCorrelationNodeModel.DefaultKeyExpression);
        model.SideExpression.ShouldBe(RoutingCorrelationNodeModel.DefaultSideExpression);
        model.RequestSide.ShouldBe(RoutingCorrelationNodeModel.DefaultRequestSide);
        model.ResponseSide.ShouldBe(RoutingCorrelationNodeModel.DefaultResponseSide);
        model.TimeoutMilliseconds.ShouldBe(RoutingCorrelationNodeModel.DefaultTimeoutMilliseconds);
        model.MaxPending.ShouldBe(RoutingCorrelationNodeModel.DefaultMaxPending);
        model.BoundedCapacity.ShouldBe(RoutingCorrelationNodeModel.DefaultBoundedCapacity);
        model.PortDescriptors.ShouldContain(port => port.Name == "Matched" && port.ValueType == "FlowCorrelationMatch" && !port.IsInput);
    }

    [Fact]
    public void RoutingJoinNodeModel_BuildsConfigurationAndPorts()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingJoinNodeModel(
            "workflow1.join",
            new DiagramPoint(10, 20),
            "join",
            catalog.Find(RoutingNodeTypes.Join).ShouldNotBeNull(),
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["leftInputType"] = "TimerTick",
            ["rightInputType"] = "MqttEnvelope",
            ["leftKeyExpression"] = "topic",
            ["rightKeyExpression"] = "topic",
            ["caseSensitive"] = true,
            ["timeoutMilliseconds"] = 12000,
            ["maxPending"] = 64,
            ["boundedCapacity"] = 32
        });

        model.PortDescriptors.ShouldContain(port => port.Name == "Left" && port.ValueType == "TimerTick" && port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Right" && port.ValueType == "MqttEnvelope" && port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowJoinResult" && !port.IsInput);
        model.PortDescriptors.ShouldContain(port => port.Name == "Timeouts" && port.ValueType == "FlowJoinTimeout" && !port.IsInput);
        model.ResolvePortValueType(new ComponentPortDescriptor("Left", "Configured left input type", IsInput: true))
            .ShouldBe("TimerTick");
        model.ResolvePortValueType(new ComponentPortDescriptor("Right", "Configured right input type", IsInput: true))
            .ShouldBe("MqttEnvelope");

        var config = model.BuildConfiguration();
        config["leftInputType"]!.GetValue<string>().ShouldBe("TimerTick");
        config["rightInputType"]!.GetValue<string>().ShouldBe("MqttEnvelope");
        config["leftKeyExpression"]!.GetValue<string>().ShouldBe("topic");
        config["rightKeyExpression"]!.GetValue<string>().ShouldBe("topic");
        config["caseSensitive"]!.GetValue<bool>().ShouldBeTrue();
        config["timeoutMilliseconds"]!.GetValue<int>().ShouldBe(12000);
        config["maxPending"]!.GetValue<int>().ShouldBe(64);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(32);
    }

    [Fact]
    public void RoutingJoinNodeModel_NormalizesConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var model = new RoutingJoinNodeModel(
            "workflow1.join",
            new DiagramPoint(10, 20),
            "join",
            catalog.Find(RoutingNodeTypes.Join).ShouldNotBeNull(),
            isResource: false)
        {
            LeftInputType = "bad",
            RightInputType = "bad",
            LeftKeyExpression = " ",
            RightKeyExpression = "",
            TimeoutMilliseconds = 0,
            MaxPending = 0,
            BoundedCapacity = 0
        };

        var config = model.BuildConfiguration();
        config["leftInputType"]!.GetValue<string>().ShouldBe(RoutingJoinNodeModel.DefaultLeftInputType);
        config["rightInputType"]!.GetValue<string>().ShouldBe(RoutingJoinNodeModel.DefaultRightInputType);
        config["leftKeyExpression"]!.GetValue<string>().ShouldBe(RoutingJoinNodeModel.DefaultLeftKeyExpression);
        config["rightKeyExpression"]!.GetValue<string>().ShouldBe(RoutingJoinNodeModel.DefaultRightKeyExpression);
        config["timeoutMilliseconds"]!.GetValue<int>().ShouldBe(RoutingJoinNodeModel.DefaultTimeoutMilliseconds);
        config["maxPending"]!.GetValue<int>().ShouldBe(RoutingJoinNodeModel.DefaultMaxPending);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(RoutingJoinNodeModel.DefaultBoundedCapacity);

        model.LoadConfiguration(new JsonObject
        {
            ["leftInputType"] = "unknown",
            ["rightInputType"] = "unknown",
            ["leftKeyExpression"] = "",
            ["rightKeyExpression"] = "",
            ["timeoutMilliseconds"] = -1,
            ["maxPending"] = -2,
            ["boundedCapacity"] = -3
        });

        model.LeftInputType.ShouldBe(RoutingJoinNodeModel.DefaultLeftInputType);
        model.RightInputType.ShouldBe(RoutingJoinNodeModel.DefaultRightInputType);
        model.LeftKeyExpression.ShouldBe(RoutingJoinNodeModel.DefaultLeftKeyExpression);
        model.RightKeyExpression.ShouldBe(RoutingJoinNodeModel.DefaultRightKeyExpression);
        model.TimeoutMilliseconds.ShouldBe(RoutingJoinNodeModel.DefaultTimeoutMilliseconds);
        model.MaxPending.ShouldBe(RoutingJoinNodeModel.DefaultMaxPending);
        model.BoundedCapacity.ShouldBe(RoutingJoinNodeModel.DefaultBoundedCapacity);
        model.PortDescriptors.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowJoinResult" && !port.IsInput);
    }

    [Fact]
    public void FlowAssertionNodeModel_BuildsAssertionConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("flow.assert").ShouldNotBeNull();
        var model = new FlowAssertionNodeModel(
            "workflow1.assertion",
            new DiagramPoint(10, 20),
            "assertion",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["assertionName"] = "Topic starts with factory",
            ["inputType"] = "MqttPublishRequest",
            ["expression"] = "topic.StartsWith(\"factory/\")",
            ["failureMessage"] = "Topic did not match factory.",
            ["boundedCapacity"] = 250
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "assertionName",
            "inputType",
            "expression",
            "failureMessage",
            "boundedCapacity"
        ], ignoreOrder: true);
        config["assertionName"]!.GetValue<string>().ShouldBe("Topic starts with factory");
        config["inputType"]!.GetValue<string>().ShouldBe("MqttPublishRequest");
        config["expression"]!.GetValue<string>().ShouldBe("topic.StartsWith(\"factory/\")");
        config["failureMessage"]!.GetValue<string>().ShouldBe("Topic did not match factory.");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(250);
        model.ResolvePortValueType(new ComponentPortDescriptor("Passed", "Configured input type", IsInput: false))
            .ShouldBe("MqttPublishRequest");
    }

    [Fact]
    public void FlowAssertionNodeModel_NormalizesInvalidAssertionConfiguration()
    {
        var model = new FlowAssertionNodeModel(
            "workflow1.assertion",
            new DiagramPoint(10, 20),
            "assertion",
            descriptor: null,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["assertionName"] = "",
            ["inputType"] = "UnknownInput",
            ["expression"] = "",
            ["failureMessage"] = "",
            ["boundedCapacity"] = 0
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "assertionName",
            "inputType",
            "expression",
            "failureMessage",
            "boundedCapacity"
        ], ignoreOrder: true);
        config["assertionName"]!.GetValue<string>().ShouldBe(FlowAssertionNodeModel.DefaultAssertionName);
        config["inputType"]!.GetValue<string>().ShouldBe(FlowAssertionNodeModel.DefaultInputType);
        config["expression"]!.GetValue<string>().ShouldBe(FlowAssertionNodeModel.DefaultExpression);
        config["failureMessage"]!.GetValue<string>().ShouldBe(FlowAssertionNodeModel.DefaultFailureMessage);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(FlowAssertionNodeModel.DefaultBoundedCapacity);
    }

    [Fact]
    public void FlowAssertionNodeModel_UsesSharedAssertionInputTypes()
    {
        FlowAssertionNodeModel.InputTypes.ShouldBe(FlowContractTypeNames.AssertionInputTypes);
        FlowAssertionNodeModel.InputTypes.ShouldContain("StateReducerResult");

        FlowAssertionNodeModel.NormalizeInputType("FluxFlow.Components.State.Contracts.StateReducerResult")
            .ShouldBe("StateReducerResult");
    }

    [Fact]
    public void HttpRequestNodeModel_NormalizesAndPersistsExistingConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("http.request").ShouldNotBeNull();
        var model = new HttpRequestNodeModel(
            "workflow1.http",
            new DiagramPoint(10, 20),
            "http",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["baseUrl"] = " https://api.example.test/ ",
            ["defaultHeaders"] = new JsonObject
            {
                ["Authorization"] = "Bearer token",
                ["X-Trace"] = "true"
            },
            ["defaultTimeoutMilliseconds"] = 0,
            ["maxResponseBodyBytes"] = -1,
            ["followRedirects"] = false,
            ["treatNonSuccessStatusAsError"] = true,
            ["boundedCapacity"] = "0"
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "defaultTimeoutMilliseconds",
            "maxResponseBodyBytes",
            "followRedirects",
            "treatNonSuccessStatusAsError",
            "boundedCapacity",
            "baseUrl",
            "defaultHeaders"
        ], ignoreOrder: true);
        config["baseUrl"]!.GetValue<string>().ShouldBe("https://api.example.test/");
        config["defaultTimeoutMilliseconds"]!.GetValue<int>().ShouldBe(HttpRequestNodeModel.DefaultTimeoutMilliseconds);
        config["maxResponseBodyBytes"]!.GetValue<int>().ShouldBe(HttpRequestNodeModel.DefaultMaxResponseBodyBytes);
        config["followRedirects"]!.GetValue<bool>().ShouldBeFalse();
        config["treatNonSuccessStatusAsError"]!.GetValue<bool>().ShouldBeTrue();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(HttpRequestNodeModel.DefaultBoundedCapacity);

        var headers = config["defaultHeaders"]!.AsObject();
        headers["Authorization"]!.GetValue<string>().ShouldBe("Bearer token");
        headers["X-Trace"]!.GetValue<string>().ShouldBe("true");
    }

    [Fact]
    public void PayloadInspectNodeModel_NormalizesAndPersistsExistingConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("payload.inspect").ShouldNotBeNull();
        var model = new PayloadInspectNodeModel(
            "workflow1.payloadInspect",
            new DiagramPoint(10, 20),
            "payloadInspect",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["maxPreviewBytes"] = 0,
            ["maxFormattedChars"] = -1,
            ["detectBase64"] = false,
            ["formatJson"] = false,
            ["formatXml"] = true,
            ["boundedCapacity"] = "0"
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "maxPreviewBytes",
            "maxFormattedChars",
            "detectBase64",
            "formatJson",
            "formatXml",
            "boundedCapacity"
        ], ignoreOrder: true);
        config["maxPreviewBytes"]!.GetValue<int>().ShouldBe(PayloadInspectNodeModel.DefaultMaxPreviewBytes);
        config["maxFormattedChars"]!.GetValue<int>().ShouldBe(PayloadInspectNodeModel.DefaultMaxFormattedChars);
        config["detectBase64"]!.GetValue<bool>().ShouldBeFalse();
        config["formatJson"]!.GetValue<bool>().ShouldBeFalse();
        config["formatXml"]!.GetValue<bool>().ShouldBeTrue();
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(PayloadInspectNodeModel.DefaultBoundedCapacity);
    }

    [Fact]
    public void StateReducerNodeModel_BuildsReducerConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("state.reducer").ShouldNotBeNull();
        var model = new StateReducerNodeModel(
            "workflow1.stateReducer",
            new DiagramPoint(10, 20),
            "stateReducer",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["engine"] = "dynamic-expresso",
            ["keyExpression"] = "topic",
            ["reducer"] = "state == null ? input : state",
            ["expressionName"] = "latest",
            ["boundedCapacity"] = 64,
            ["maxKeys"] = 32
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "engine",
            "reducer",
            "boundedCapacity",
            "maxKeys",
            "keyExpression",
            "expressionName"
        ], ignoreOrder: true);
        config["engine"]!.GetValue<string>().ShouldBe("dynamic-expresso");
        config["keyExpression"]!.GetValue<string>().ShouldBe("topic");
        config["reducer"]!.GetValue<string>().ShouldBe("state == null ? input : state");
        config["expressionName"]!.GetValue<string>().ShouldBe("latest");
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(64);
        config["maxKeys"]!.GetValue<int>().ShouldBe(32);
    }

    [Fact]
    public void StateReducerNodeModel_NormalizesInvalidReducerConfiguration()
    {
        var model = new StateReducerNodeModel(
            "workflow1.stateReducer",
            new DiagramPoint(10, 20),
            "stateReducer",
            descriptor: null,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["engine"] = "unknown",
            ["keyExpression"] = "",
            ["reducer"] = "",
            ["expressionName"] = "",
            ["boundedCapacity"] = 0,
            ["maxKeys"] = -1
        });

        var config = model.BuildConfiguration();

        config.Select(static item => item.Key).ShouldBe([
            "engine",
            "reducer",
            "boundedCapacity",
            "maxKeys"
        ], ignoreOrder: true);
        config["engine"]!.GetValue<string>().ShouldBe(StateReducerNodeModel.DefaultEngine);
        config["reducer"]!.GetValue<string>().ShouldBe(StateReducerNodeModel.DefaultReducer);
        config["boundedCapacity"]!.GetValue<int>().ShouldBe(StateReducerNodeModel.DefaultBoundedCapacity);
        config["maxKeys"]!.GetValue<int>().ShouldBe(StateReducerNodeModel.DefaultMaxKeys);
        config.ContainsKey("keyExpression").ShouldBeFalse();
        config.ContainsKey("expressionName").ShouldBeFalse();
    }

    [Fact]
    public void MqttMetricsNodeModel_BuildsRateConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.metrics").ShouldNotBeNull();
        var model = new MqttMetricsNodeModel(
            "workflow1.metrics",
            new DiagramPoint(10, 20),
            "metrics",
            descriptor,
            isResource: false);

        model.LoadConfiguration(new JsonObject
        {
            ["boundedCapacity"] = 250,
            ["rateWindowSeconds"] = 15,
            ["metricCardColumns"] = 3,
            ["displayMetrics"] = new JsonArray(
                MqttMetricsNodeModel.MetricMessages,
                MqttMetricsNodeModel.MetricPayloadBytes,
                MqttMetricsNodeModel.MetricTopics,
                MqttMetricsNodeModel.MetricCurrentRate,
                MqttMetricsNodeModel.MetricAverageRate,
                MqttMetricsNodeModel.MetricRetained,
                MqttMetricsNodeModel.MetricAveragePayload)
        });

        var config = model.BuildConfiguration();

        config["boundedCapacity"]!.GetValue<int>().ShouldBe(250);
        config["rateWindowSeconds"]!.GetValue<double>().ShouldBe(15);
        config["metricCardColumns"]!.GetValue<int>().ShouldBe(3);
        config.ContainsKey("metricCardRows").ShouldBeFalse();
        config["displayMetrics"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe([
                MqttMetricsNodeModel.MetricMessages,
                MqttMetricsNodeModel.MetricPayloadBytes,
                MqttMetricsNodeModel.MetricTopics,
                MqttMetricsNodeModel.MetricCurrentRate,
                MqttMetricsNodeModel.MetricAverageRate,
                MqttMetricsNodeModel.MetricRetained,
                MqttMetricsNodeModel.MetricAveragePayload
            ]);
    }

    [Fact]
    public void MqttMetricsNodeModel_NormalizesInvalidConfiguration()
    {
        var catalog = new FlowComponentCatalog();
        var descriptor = catalog.Find("mqtt.metrics").ShouldNotBeNull();
        var model = new MqttMetricsNodeModel(
            "workflow1.metrics",
            new DiagramPoint(10, 20),
            "metrics",
            descriptor,
            isResource: false)
        {
            BoundedCapacity = 0,
            RateWindowSeconds = double.NaN,
            MetricCardColumns = 99,
            DisplayMetrics = ["unknown", "", MqttMetricsNodeModel.MetricTopics, MqttMetricsNodeModel.MetricTopics]
        };

        var config = model.BuildConfiguration();

        config["boundedCapacity"]!.GetValue<int>().ShouldBe(MqttMetricsNodeModel.DefaultBoundedCapacity);
        config["rateWindowSeconds"]!.GetValue<double>().ShouldBe(MqttMetricsNodeModel.DefaultRateWindowSeconds);
        config["metricCardColumns"]!.GetValue<int>().ShouldBe(MqttMetricsNodeModel.MaxMetricCardColumns);
        config["displayMetrics"]!.AsArray().Select(static node => node!.GetValue<string>())
            .ShouldBe([MqttMetricsNodeModel.MetricTopics]);

        model.LoadConfiguration(new JsonObject
        {
            ["boundedCapacity"] = -1,
            ["rateWindowSeconds"] = -2,
            ["metricCardColumns"] = -3,
            ["displayMetrics"] = new JsonArray("bad")
        });

        model.BoundedCapacity.ShouldBe(MqttMetricsNodeModel.DefaultBoundedCapacity);
        model.RateWindowSeconds.ShouldBe(MqttMetricsNodeModel.DefaultRateWindowSeconds);
        model.MetricCardColumns.ShouldBe(MqttMetricsNodeModel.MinMetricCardColumns);
        model.DisplayMetrics.ShouldBe(MqttMetricsNodeModel.DefaultDisplayMetrics);
    }
}
