using FluxMq.App;
using FluxMq.Core.Models;
using FluxFlow.Components.Secrets.Contracts;
using FluxFlow.Engine.Runtime;
using FluxMq.Scenarios;
using FluxMq.UI.Components.Workspace.Nodes.Routing;
using FluxMq.UI.Models;
using FluxMq.UI.Services;
using Shouldly;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace FluxMq.UI.Tests;

public sealed class FlowDefinitionComposerTests
{
    [Fact]
    public void ComponentCatalog_ExposesDynamicMapperWithoutRequestAliases()
    {
        var catalog = new FlowComponentCatalog();

        catalog.Components.ShouldContain(component => component.Type == "flow.mapper");
        catalog.Components.ShouldContain(component => component.Type == "flow.logger");
        catalog.Components.ShouldContain(component => component.Type == "mqtt.trigger");
        catalog.Components.ShouldContain(component => component.Type == "http.request");
        catalog.Components.ShouldContain(component => component.Type == "payload.inspect");
        catalog.Components.ShouldNotContain(component => component.Type == "mqtt.publish-request");
        catalog.Components.ShouldNotContain(component => component.Type == "mqtt.recording-request");
        catalog.Components.ShouldNotContain(component => component.Type == "file.write-request");

        catalog.Find("mqtt.publish-request").ShouldBeNull();
        catalog.Find("mqtt.recording-request").ShouldBeNull();
        catalog.Find("file.write-request").ShouldBeNull();
        catalog.Find("mqtt.metrics-sink").ShouldBeNull();
    }

    [Fact]
    public void ComponentCatalog_AllDesignerComponentsHaveRegisteredRuntimeFactory()
    {
        var catalogTypes = new FlowComponentCatalog()
            .Components
            .Select(component => component.Type)
            .ToArray();

        var runtimeTypes = new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Factories
            .Keys
            .Select(type => type.Value)
            .ToHashSet(StringComparer.Ordinal);

        catalogTypes
            .Where(type => !runtimeTypes.Contains(type))
            .ShouldBeEmpty();
    }

    [Fact]
    public void ComponentCatalog_ExposesHttpAndPayloadComponents()
    {
        var catalog = new FlowComponentCatalog();

        var http = catalog.Find("http.request").ShouldNotBeNull();
        http.Category.ShouldBe("Actor");
        http.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "HttpRequestInput" && port.IsInput);
        http.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "HttpResponseOutput" && !port.IsInput);
        http.Ports.ShouldContain(port => port.Name == "Errors" && port.ValueType == "HttpErrorOutput" && !port.IsInput);

        var payload = catalog.Find("payload.inspect").ShouldNotBeNull();
        payload.Category.ShouldBe("Mapper");
        payload.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "PayloadInspectionRequest" && port.IsInput);
        payload.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "PayloadInspectionResult" && !port.IsInput);
        payload.Ports.ShouldContain(port => port.Name == "Errors" && port.ValueType == "FlowError" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_TriggerUsesConfiguredBrokerWithoutConnectionPort()
    {
        var catalog = new FlowComponentCatalog();

        var descriptor = catalog.Find("mqtt.trigger").ShouldNotBeNull();

        descriptor.Ports.ShouldNotContain(port => port.Name == "Connection");
        descriptor.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Errors" && port.ValueType == "FlowError" && !port.IsInput);

        var stateTrigger = catalog.Find("mqtt.connection-state-trigger").ShouldNotBeNull();
        stateTrigger.Ports.ShouldNotContain(port => port.Name == "Connection");
        stateTrigger.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "MqttClientStateChanged" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_ExposesFlowLoggerPorts()
    {
        var catalog = new FlowComponentCatalog();

        var descriptor = catalog.Find("flow.logger").ShouldNotBeNull();

        descriptor.DisplayName.ShouldBe("Flow Logger");
        descriptor.Category.ShouldBe("Observer");
        descriptor.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "MqttEnvelope" && port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "FlowErrors" && port.ValueType == "FlowError" && port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Entries" && port.ValueType == "FlowLogEntry" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_ExposesRouteableDecisionPorts()
    {
        var catalog = new FlowComponentCatalog();

        var router = catalog.Find("flow.when").ShouldNotBeNull();
        router.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "Configured input type" && port.IsInput);
        router.Ports.ShouldContain(port => port.Name == "WhenTrue" && port.ValueType == "Configured input type" && !port.IsInput);
        router.Ports.ShouldContain(port => port.Name == "WhenFalse" && port.ValueType == "Configured input type" && !port.IsInput);
        router.Ports.ShouldNotContain(port => port.Name == "Entries");

        var validator = catalog.Find("json.schema-validator").ShouldNotBeNull();
        validator.Ports.ShouldContain(port => port.Name == "Result" && port.ValueType == "JsonSchemaValidationResult" && !port.IsInput);
        validator.Ports.ShouldContain(port => port.Name == "Valid" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        validator.Ports.ShouldContain(port => port.Name == "Invalid" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        validator.Ports.Last().Name.ShouldBe("Errors");

        var assertion = catalog.Find("flow.assert").ShouldNotBeNull();
        assertion.Category.ShouldBe("Assertion");
        assertion.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "Configured input type" && port.IsInput);
        assertion.Ports.ShouldContain(port => port.Name == "Result" && port.ValueType == "FlowAssertionResult" && !port.IsInput);
        assertion.Ports.ShouldContain(port => port.Name == "Passed" && port.ValueType == "Configured input type" && !port.IsInput);
        assertion.Ports.ShouldContain(port => port.Name == "Failed" && port.ValueType == "Configured input type" && !port.IsInput);
        assertion.Ports.ShouldNotContain(port => port.Name == "Entries");
        assertion.Ports.Last().Name.ShouldBe("Errors");
    }

    [Fact]
    public void ComponentCatalog_ExposesRoutingComponents()
    {
        var catalog = new FlowComponentCatalog();

        var switchNode = catalog.Find(RoutingNodeTypes.Switch).ShouldNotBeNull();
        switchNode.Category.ShouldBe("Control");
        switchNode.Ports.ShouldContain(port => port.Name == "Input" && port.IsInput);
        switchNode.Ports.ShouldContain(port => port.Name == "WhenTrue" && !port.IsInput);
        switchNode.Ports.ShouldContain(port => port.Name == "WhenFalse" && !port.IsInput);

        var correlation = catalog.Find(RoutingNodeTypes.Correlation).ShouldNotBeNull();
        correlation.Ports.ShouldContain(port => port.Name == "Input" && port.IsInput);
        correlation.Ports.ShouldContain(port => port.Name == "Matched" && port.ValueType == "FlowCorrelationMatch" && !port.IsInput);
        correlation.Ports.ShouldContain(port => port.Name == "Timeouts" && port.ValueType == "FlowCorrelationTimeout" && !port.IsInput);

        var window = catalog.Find(RoutingNodeTypes.Window).ShouldNotBeNull();
        window.Ports.ShouldContain(port => port.Name == "Input" && port.IsInput);
        window.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowWindow" && !port.IsInput);
        window.Ports.Last().Name.ShouldBe("Errors");

        var join = catalog.Find(RoutingNodeTypes.Join).ShouldNotBeNull();
        join.Ports.ShouldContain(port => port.Name == "Left" && port.IsInput);
        join.Ports.ShouldContain(port => port.Name == "Right" && port.IsInput);
        join.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowJoinResult" && !port.IsInput);
        join.Ports.ShouldContain(port => port.Name == "Timeouts" && port.ValueType == "FlowJoinTimeout" && !port.IsInput);

        var fork = catalog.Find(RoutingNodeTypes.Fork).ShouldNotBeNull();
        fork.Ports.ShouldContain(port => port.Name == "Input" && port.IsInput);
        fork.Ports.ShouldContain(port => port.Name == "A" && !port.IsInput);
        fork.Ports.ShouldContain(port => port.Name == "B" && !port.IsInput);

        var merge = catalog.Find(RoutingNodeTypes.Merge).ShouldNotBeNull();
        merge.Ports.ShouldContain(port => port.Name == "Left" && port.IsInput);
        merge.Ports.ShouldContain(port => port.Name == "Right" && port.IsInput);
        merge.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "FlowMergeItem" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_ExposesMetrics()
    {
        var catalog = new FlowComponentCatalog();

        var descriptor = catalog.Find("mqtt.metrics").ShouldNotBeNull();

        descriptor.DisplayName.ShouldBe("MQTT Metrics");
        descriptor.Ports.ShouldContain(port => port.Name == "Input" && port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Snapshots" && !port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Errors" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_ExposesStateReducer()
    {
        var catalog = new FlowComponentCatalog();

        var descriptor = catalog.Find("state.reducer").ShouldNotBeNull();

        descriptor.DisplayName.ShouldBe("State Reducer");
        descriptor.Category.ShouldBe("State");
        descriptor.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "StateReducerInput" && port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "StateReducerResult" && !port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Errors" && port.ValueType == "FlowError" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_ExposesTimerNodes()
    {
        var catalog = new FlowComponentCatalog();

        var interval = catalog.Find("timer.interval").ShouldNotBeNull();
        interval.Category.ShouldBe("Source");
        interval.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "TimerTick" && !port.IsInput);

        var schedule = catalog.Find("timer.schedule").ShouldNotBeNull();
        schedule.Category.ShouldBe("Source");
        schedule.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "ScheduleTick" && !port.IsInput);

        var delay = catalog.Find("timer.delay").ShouldNotBeNull();
        delay.Category.ShouldBe("Control");
        delay.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "Configured input type" && port.IsInput);
        delay.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "Configured input type" && !port.IsInput);

        var debounce = catalog.Find("timer.debounce").ShouldNotBeNull();
        debounce.Category.ShouldBe("Control");
        debounce.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "Configured input type" && port.IsInput);
        debounce.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "Configured input type" && !port.IsInput);

        var throttle = catalog.Find("timer.throttle").ShouldNotBeNull();
        throttle.Category.ShouldBe("Control");
        throttle.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "Configured input type" && port.IsInput);
        throttle.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "Configured input type" && !port.IsInput);
    }

    [Fact]
    public void ComponentCatalog_ExposesSerializationTransforms()
    {
        var catalog = new FlowComponentCatalog();

        var jsonParse = catalog.Find("json.parse").ShouldNotBeNull();
        jsonParse.DisplayName.ShouldBe("JSON Parse");
        jsonParse.Category.ShouldBe("Serialization");
        jsonParse.Ports.ShouldContain(port => port.Name == "Input" && port.ValueType == "JsonParseRequest" && port.IsInput);
        jsonParse.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "JsonParseResult" && !port.IsInput);
        jsonParse.Ports.ShouldContain(port => port.Name == "Errors" && port.ValueType == "FlowError" && !port.IsInput);

        catalog.Find("json.stringify").ShouldNotBeNull().Category.ShouldBe("Serialization");
        catalog.Find("text.encode").ShouldNotBeNull().Category.ShouldBe("Serialization");
        catalog.Find("text.decode").ShouldNotBeNull().Category.ShouldBe("Serialization");
        catalog.Find("base64.encode").ShouldNotBeNull().Category.ShouldBe("Serialization");
        catalog.Find("base64.decode").ShouldNotBeNull().Category.ShouldBe("Serialization");
    }

    [Fact]
    public void CreateInspectPayloadsDefinition_CreatesHostBuildableDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile
            {
                Name = "local-broker",
                Host = "localhost",
                Port = 1883,
                ClientId = "ui-tests"
            },
            "factory/#");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        using var host = FlowApplicationHost.CreateDefault(configuration);
        var result = host.Build();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
        result.RuntimeBuild!.Runtime!.Workflows.Single().Nodes.Select(node => node.Address.Node.Value)
            .ShouldContain(FlowDefinitionComposer.TriggerNodeName);
    }

    [Fact]
    public void CreateInspectPayloadsDefinition_PlacesBrokerAsResource()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "my-broker", Host = "192.168.1.10", Port = 8883, ClientId = "test" },
            "#");

        using var document = JsonDocument.Parse(json);
        var flowApplication = document.RootElement.GetProperty("FluxMq").GetProperty("FlowApplication");

        var broker = flowApplication
            .GetProperty("resources")
            .GetProperty(FlowDefinitionComposer.BrokerResourceName);

        broker.GetProperty("type").GetString().ShouldBe("mqtt.connection");
        broker.GetProperty("configuration").GetProperty("profile").GetProperty("host").GetString()
            .ShouldBe("192.168.1.10");

        var trigger = flowApplication
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.TriggerNodeName);

        trigger.GetProperty("type").GetString().ShouldBe("mqtt.trigger");
        trigger.GetProperty("configuration").GetProperty("connection").GetString()
            .ShouldBe(FlowDefinitionComposer.BrokerResourceName);
    }

    [Fact]
    public void UpsertBroker_UpdatesConnectionAndTrigger_WithoutRemovingDownstreamNodes()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "first", Host = "localhost", Port = 1883, ClientId = "first-client" },
            "#");

        var updated = composer.UpsertBroker(
            initial,
            new MqttConnectionProfile { Name = "second", Host = "127.0.0.1", Port = 1884, ClientId = "second-client" },
            "devices/#");

        using var document = JsonDocument.Parse(updated);
        var flowApplication = document.RootElement.GetProperty("FluxMq").GetProperty("FlowApplication");

        var broker = flowApplication
            .GetProperty("resources")
            .GetProperty(FlowDefinitionComposer.BrokerResourceName);

        broker.GetProperty("configuration").GetProperty("profile").GetProperty("port").GetInt32()
            .ShouldBe(1884);

        var trigger = flowApplication
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.TriggerNodeName);

        var subscription = trigger.GetProperty("configuration").GetProperty("subscriptions")[0];
        subscription.GetProperty("topicFilter").GetString()
            .ShouldBe("devices/#");
        subscription.GetProperty("qos").GetInt32().ShouldBe(1);
        subscription.GetProperty("receiveRetained").GetBoolean().ShouldBeTrue();
        subscription.GetProperty("retainAsPublished").GetBoolean().ShouldBeTrue();

        flowApplication
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .TryGetProperty(FlowDefinitionComposer.InspectorNodeName, out _)
            .ShouldBeTrue();
    }

    [Fact]
    public void ReadConnectionResources_UsesBrokerMonitorSubscriptionInsteadOfTriggerSubscription()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "resources": {
                "broker1": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": { "name": "broker1", "host": "localhost", "port": 1883 }
                  }
                }
              },
              "workflows": {
                "pipe": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "broker1",
                      "subscriptions": [{ "topicFilter": "factory/#", "qos": 2 }]
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var connection = composer.ReadConnectionResourcesFromDefinition(json).ShouldHaveSingleItem();

        connection.Name.ShouldBe("broker1");
        connection.Subscription.ShouldBe(LiveMqttWorkspaceService.DefaultBrokerMonitorSubscription);
    }

    [Fact]
    public void ConnectionResourceComposer_PreservesPasswordSecretReference()
    {
        var composer = new FlowDefinitionComposer();
        var profile = new MqttConnectionProfile
        {
            Name = "broker1",
            Host = "localhost",
            Port = 1883,
            Username = "tester",
            Password = "plain-password",
            PasswordSecret = new SecretReference
            {
                Name = new SecretName("broker-password"),
                Version = "v1",
                Kind = "mqtt-password"
            }
        };

        var json = composer.UpsertConnectionResource(
            composer.CreateEmptyDefinition(),
            "broker1",
            profile);

        using var doc = JsonDocument.Parse(json);
        var profileJson = doc.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("resources")
            .GetProperty("broker1")
            .GetProperty("configuration")
            .GetProperty("profile");

        profileJson.GetProperty("username").GetString().ShouldBe("tester");
        profileJson.TryGetProperty("password", out _).ShouldBeFalse();
        var passwordSecret = profileJson.GetProperty("passwordSecret");
        passwordSecret.GetProperty("name").GetString().ShouldBe("broker-password");
        passwordSecret.GetProperty("version").GetString().ShouldBe("v1");
        passwordSecret.GetProperty("kind").GetString().ShouldBe("mqtt-password");

        var connection = composer.ReadConnectionResourcesFromDefinition(json).ShouldHaveSingleItem();
        connection.Profile.Password.ShouldBeNull();
        connection.Profile.PasswordSecret.ShouldNotBeNull();
        connection.Profile.PasswordSecret.Name.Value.ShouldBe("broker-password");
        connection.Profile.PasswordSecret.Version.ShouldBe("v1");
        connection.Profile.PasswordSecret.Kind.ShouldBe("mqtt-password");
    }

    [Theory]
    [InlineData("workflow-nodes")]
    [InlineData("node-positions")]
    [InlineData("connection-resources")]
    [InlineData("artifact-names")]
    public void DefinitionReaders_ThrowHelpfulErrorForMalformedJson(string reader)
    {
        var composer = new FlowDefinitionComposer();
        Action action = reader switch
        {
            "workflow-nodes" => () => _ = composer.GetWorkflowNodes("{", "pipe"),
            "node-positions" => () => _ = composer.ReadNodePositions("{"),
            "connection-resources" => () => _ = composer.ReadConnectionResourcesFromDefinition("{"),
            "artifact-names" => () => _ = composer.GetWorkflowNames("{"),
            _ => throw new InvalidOperationException($"Unknown reader '{reader}'.")
        };

        var exception = Should.Throw<InvalidOperationException>(action);

        exception.Message.ShouldContain("flow definition JSON is invalid");
        exception.InnerException.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public void ReadNodePositions_ThrowsWhenPositionShapeIsInvalid()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "Designer": {
              "nodes": {
                "pipe/source": {
                  "x": "left",
                  "y": 20
                }
              }
            }
          }
        }
        """;

        Should.Throw<InvalidOperationException>(() => composer.ReadNodePositions(json));
    }

    [Fact]
    public void AddComponent_WiresInspectorToTriggerOutput()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "mqtt.payload-inspector");

        using var document = JsonDocument.Parse(updated);
        var inspect = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.InspectorNodeName);

        inspect.GetProperty("Input").GetString()
            .ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
    }

    [Fact]
    public void AddComponent_AddsHttpRequestConfigurationAndUsesMapperInput()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var withMapper = composer.AddComponent(initial, "flow.mapper");
        var updated = composer.AddComponent(withMapper, "http.request");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName);

        var http = workflow.GetProperty(FlowDefinitionComposer.HttpRequestNodeName);
        http.GetProperty("type").GetString().ShouldBe("http.request");
        http.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.MapperNodeName}.Output");
        http.GetProperty("configuration").GetProperty("defaultTimeoutMilliseconds").GetInt32().ShouldBe(30000);
        http.GetProperty("configuration").GetProperty("maxResponseBodyBytes").GetInt32().ShouldBe(1048576);
        http.GetProperty("configuration").GetProperty("followRedirects").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void AddComponent_AddsPayloadInspectConfigurationAndUsesMapperInput()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var withMapper = composer.AddComponent(initial, "flow.mapper");
        var updated = composer.AddComponent(withMapper, "payload.inspect");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName);

        var payload = workflow.GetProperty(FlowDefinitionComposer.PayloadInspectNodeName);
        payload.GetProperty("type").GetString().ShouldBe("payload.inspect");
        payload.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.MapperNodeName}.Output");
        payload.GetProperty("configuration").GetProperty("maxPreviewBytes").GetInt32().ShouldBe(1024);
        payload.GetProperty("configuration").GetProperty("maxFormattedChars").GetInt32().ShouldBe(4096);
        payload.GetProperty("configuration").GetProperty("detectBase64").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void AddComponent_AddsExplicitDynamicMapperBetweenSourceAndActor()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var withMapper = composer.AddComponent(initial, "flow.mapper");
        var updated = composer.AddComponent(withMapper, "mqtt.publisher");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName);

        var mapper = workflow.GetProperty(FlowDefinitionComposer.MapperNodeName);
        mapper.GetProperty("type").GetString().ShouldBe("flow.mapper");
        mapper.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        mapper.GetProperty("configuration").GetProperty("outputType").GetString().ShouldBe("MqttPublishRequest");

        var publisher = workflow.GetProperty(FlowDefinitionComposer.PublisherNodeName);
        publisher.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.MapperNodeName}.Output");
        publisher.GetProperty("configuration").GetProperty("connection").GetString()
            .ShouldBe(FlowDefinitionComposer.BrokerResourceName);
        publisher.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32()
            .ShouldBe(1000);
    }

    [Fact]
    public void AddComponent_ConfiguresMapperFromTimerSource()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.AddWorkflow(composer.CreateEmptyDefinition(), FlowDefinitionComposer.DefaultWorkflowName);
        var withTimer = composer.AddComponent(initial, "timer.interval");

        var updated = composer.AddComponent(withTimer, "flow.mapper");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName);

        var mapper = workflow.GetProperty(FlowDefinitionComposer.MapperNodeName);
        mapper.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TimerIntervalNodeName}.Output");
        mapper.GetProperty("configuration").GetProperty("inputType").GetString().ShouldBe("TimerTick");
        mapper.GetProperty("configuration").GetProperty("expression").GetString().ShouldNotBeNull().ShouldContain("timer/");
    }

    [Fact]
    public void AddComponent_ConfiguresDelayFromTimerSource()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.AddWorkflow(composer.CreateEmptyDefinition(), FlowDefinitionComposer.DefaultWorkflowName);
        var withTimer = composer.AddComponent(initial, "timer.interval");

        var updated = composer.AddComponent(withTimer, "timer.delay");

        using var document = JsonDocument.Parse(updated);
        var delay = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.TimerDelayNodeName);

        delay.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TimerIntervalNodeName}.Output");
        delay.GetProperty("configuration").GetProperty("inputType").GetString().ShouldBe("TimerTick");
        delay.GetProperty("configuration").GetProperty("delayMilliseconds").GetInt32().ShouldBe(250);
    }

    [Theory]
    [InlineData("timer.debounce", FlowDefinitionComposer.TimerDebounceNodeName, "quietPeriodMilliseconds")]
    [InlineData("timer.throttle", FlowDefinitionComposer.TimerThrottleNodeName, "intervalMilliseconds")]
    public void AddComponent_ConfiguresTimerInputGateFromTimerSource(
        string componentType,
        string nodeName,
        string durationProperty)
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.AddWorkflow(composer.CreateEmptyDefinition(), FlowDefinitionComposer.DefaultWorkflowName);
        var withTimer = composer.AddComponent(initial, "timer.interval");

        var updated = composer.AddComponent(withTimer, componentType);

        using var document = JsonDocument.Parse(updated);
        var gate = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(nodeName);

        gate.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TimerIntervalNodeName}.Output");
        gate.GetProperty("configuration").GetProperty("inputType").GetString().ShouldBe("TimerTick");
        gate.GetProperty("configuration").GetProperty(durationProperty).GetInt32().ShouldBe(250);
    }

    [Fact]
    public void AddComponent_AddsRoutingSwitchConfigurationAndInputLink()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, RoutingNodeTypes.Switch);

        using var document = JsonDocument.Parse(updated);
        var switchNode = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.SwitchNodeName);

        switchNode.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        var config = switchNode.GetProperty("configuration");
        config.GetProperty("inputType").GetString().ShouldBe("MqttEnvelope");
        config.GetProperty("expression").GetString().ShouldBe("qos >= 1");
        config.GetProperty("routeOutputs").GetProperty("True").GetString().ShouldBe("WhenTrue");
        config.GetProperty("routeOutputs").GetProperty("False").GetString().ShouldBe("WhenFalse");
    }

    [Fact]
    public void AddComponent_AddsRoutingCorrelationConfigurationAndInputLink()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, RoutingNodeTypes.Correlation);

        using var document = JsonDocument.Parse(updated);
        var node = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.CorrelationNodeName);

        node.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        var config = node.GetProperty("configuration");
        config.GetProperty("inputType").GetString().ShouldBe("MqttEnvelope");
        config.GetProperty("keyExpression").GetString().ShouldBe("topic");
        config.GetProperty("sideExpression").GetString().ShouldBe("payloadText");
        config.GetProperty("requestSide").GetString().ShouldBe("request");
        config.GetProperty("responseSide").GetString().ShouldBe("response");
        config.GetProperty("caseSensitive").GetBoolean().ShouldBeTrue();
        config.GetProperty("timeoutMilliseconds").GetInt32().ShouldBe(30000);
        config.GetProperty("maxPending").GetInt32().ShouldBe(1024);
        config.GetProperty("boundedCapacity").GetInt32().ShouldBe(1000);
    }

    [Fact]
    public void AddComponent_AddsRoutingJoinConfigurationWithoutInputLinks()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, RoutingNodeTypes.Join);

        using var document = JsonDocument.Parse(updated);
        var node = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.JoinNodeName);

        node.TryGetProperty("Input", out _).ShouldBeFalse();
        node.TryGetProperty("Left", out _).ShouldBeFalse();
        node.TryGetProperty("Right", out _).ShouldBeFalse();
        var config = node.GetProperty("configuration");
        config.GetProperty("leftInputType").GetString().ShouldBe("MqttEnvelope");
        config.GetProperty("rightInputType").GetString().ShouldBe("MqttEnvelope");
        config.GetProperty("leftKeyExpression").GetString().ShouldBe("topic");
        config.GetProperty("rightKeyExpression").GetString().ShouldBe("topic");
        config.GetProperty("caseSensitive").GetBoolean().ShouldBeTrue();
        config.GetProperty("timeoutMilliseconds").GetInt32().ShouldBe(30000);
        config.GetProperty("maxPending").GetInt32().ShouldBe(1024);
        config.GetProperty("boundedCapacity").GetInt32().ShouldBe(1000);
    }

    [Theory]
    [InlineData("flow.window", FlowDefinitionComposer.WindowNodeName, "maxItems")]
    [InlineData("flow.fork", FlowDefinitionComposer.ForkNodeName, "outputs")]
    [InlineData("flow.merge", FlowDefinitionComposer.MergeNodeName, "inputs")]
    public void AddComponent_AddsRoutingConfiguration(string componentType, string nodeName, string requiredProperty)
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, componentType);

        using var document = JsonDocument.Parse(updated);
        var node = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(nodeName);

        var config = node.GetProperty("configuration");
        config.GetProperty("inputType").GetString().ShouldBe("MqttEnvelope");
        config.GetProperty("boundedCapacity").GetInt32().ShouldBe(1000);

        if (componentType == RoutingNodeTypes.Window)
        {
            config.GetProperty(requiredProperty).GetInt32().ShouldBe(100);
            config.GetProperty("timeMilliseconds").GetInt32().ShouldBe(5000);
            config.GetProperty("emitPartialOnCompletion").GetBoolean().ShouldBeTrue();
            node.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        }
        else
        {
            config.GetProperty(requiredProperty).EnumerateArray().Select(static item => item.GetString())
                .ShouldBe(componentType == RoutingNodeTypes.Fork ? ["A", "B"] : ["Left", "Right"]);
        }

        if (componentType is RoutingNodeTypes.Fork or RoutingNodeTypes.Window)
        {
            node.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        }
        else
        {
            node.TryGetProperty("Input", out _).ShouldBeFalse();
        }
    }

    [Fact]
    public void AddComponent_AddsMetricsRateConfiguration()
    {
        var composer = new FlowDefinitionComposer();

        var updated = composer.AddComponent(composer.CreateEmptyDefinition(), "mqtt.metrics");

        using var document = JsonDocument.Parse(updated);
        var metrics = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty("metrics");

        metrics.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32().ShouldBe(1000);
        metrics.GetProperty("configuration").GetProperty("rateWindowSeconds").GetDouble().ShouldBe(60);
        metrics.GetProperty("configuration").TryGetProperty("metricCardRows", out _).ShouldBeFalse();
        metrics.GetProperty("configuration").GetProperty("metricCardColumns").GetInt32().ShouldBe(4);
        metrics.GetProperty("configuration").GetProperty("displayMetrics")
            .EnumerateArray()
            .Select(static item => item.GetString())
            .ShouldBe(["messages", "currentRate", "averageRate", "payloadBytes"]);
    }

    [Fact]
    public void AddComponent_DoesNotAutoWireSerializationTransformToEnvelopeSource()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "json.parse");

        using var document = JsonDocument.Parse(updated);
        var parser = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty("jsonParser");

        parser.GetProperty("type").GetString().ShouldBe("json.parse");
        parser.TryGetProperty("Input", out _).ShouldBeFalse();
        parser.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32().ShouldBe(1000);
    }

    [Fact]
    public void AddComponent_DoesNotWireActorDirectlyToEnvelopeSource()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "mqtt.publisher");

        using var document = JsonDocument.Parse(updated);
        var publisher = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.PublisherNodeName);

        publisher.TryGetProperty("Input", out _).ShouldBeFalse();
    }

    [Fact]
    public void AddComponent_DoesNotWireStateReducerDirectlyToEnvelopeSource()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "state.reducer");

        using var document = JsonDocument.Parse(updated);
        var reducer = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.StateReducerNodeName);

        reducer.TryGetProperty("Input", out _).ShouldBeFalse();
        reducer.GetProperty("configuration").GetProperty("engine").GetString().ShouldBe("jsonata");
        reducer.GetProperty("configuration").GetProperty("reducer").GetString().ShouldBe("input");
    }

    [Fact]
    public void AddComponent_WiresStateReducerFromStateInputMapper()
    {
        var composer = new FlowDefinitionComposer();
        const string initial = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "mapper": {
                    "type": "flow.mapper",
                    "configuration": {
                      "outputType": "StateReducerInput"
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.AddComponent(initial, "state.reducer", "pipe");

        using var document = JsonDocument.Parse(updated);
        var reducer = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty(FlowDefinitionComposer.StateReducerNodeName);

        reducer.GetProperty("Input").GetString().ShouldBe("mapper.Output");
    }

    [Fact]
    public void AddComponent_DefaultsPublisherToFirstConnectionResource()
    {
        var composer = new FlowDefinitionComposer();
        var initial = """
        {
          "FluxMq": {
            "FlowApplication": {
              "resources": {
                "brokerA": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": {
                      "name": "broker-a",
                      "host": "localhost",
                      "port": 1883,
                      "clientId": "a"
                    }
                  }
                },
                "brokerB": {
                  "type": "mqtt.connection",
                  "configuration": {
                    "profile": {
                      "name": "broker-b",
                      "host": "localhost",
                      "port": 1884,
                      "clientId": "b"
                    }
                  }
                }
              },
              "workflows": {
                "pipe": {}
              }
            }
          }
        }
        """;

        var updated = composer.AddComponent(initial, "mqtt.publisher", "pipe");

        using var document = JsonDocument.Parse(updated);
        var publisher = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty(FlowDefinitionComposer.PublisherNodeName);

        publisher.GetProperty("configuration").GetProperty("connection").GetString()
            .ShouldBe("brokerA");
    }

    [Fact]
    public void AddComponent_CreatesUniqueNamesForRepeatedActors()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var withFirstPublisher = composer.AddComponent(initial, "mqtt.publisher");
        var updated = composer.AddComponent(withFirstPublisher, "mqtt.publisher");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName);

        workflow.GetProperty(FlowDefinitionComposer.PublisherNodeName)
            .GetProperty("type")
            .GetString()
            .ShouldBe("mqtt.publisher");
        workflow.GetProperty($"{FlowDefinitionComposer.PublisherNodeName}2")
            .GetProperty("type")
            .GetString()
            .ShouldBe("mqtt.publisher");
    }

    [Fact]
    public void AddComponent_WiresFlowLoggerToSourceAndAddsConfiguration()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "flow.logger");

        using var document = JsonDocument.Parse(updated);
        var logger = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.LoggerNodeName);

        logger.GetProperty("type").GetString().ShouldBe("flow.logger");
        logger.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        logger.TryGetProperty("FlowErrors", out _).ShouldBeFalse();
        logger.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32()
            .ShouldBe(1000);
        logger.GetProperty("configuration").GetProperty("maxEntries").GetInt32()
            .ShouldBe(500);
    }

    [Fact]
    public void AddComponent_DoesNotAppendNewComponentErrorsToExistingFlowLogger()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var withLogger = composer.AddComponent(initial, "flow.logger");
        var updated = composer.AddComponent(withLogger, "json.schema-validator");

        using var document = JsonDocument.Parse(updated);
        var logger = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.LoggerNodeName);

        logger.TryGetProperty("FlowErrors", out _).ShouldBeFalse();
    }

    [Fact]
    public void AddComponent_ConditionRouterCreatesDefaultExpressionAndBuildableDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "flow.when");

        using var document = JsonDocument.Parse(updated);
        var router = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.RouterNodeName);

        router.GetProperty("configuration").GetProperty("expression").GetString().ShouldBe("qos >= 1");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(updated));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        using var host = FlowApplicationHost.CreateDefault(configuration);
        var result = host.Build();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
    }

    [Fact]
    public void AddComponent_ConnectionStateTriggerCreatesDefaultConnectionAndBuildableDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "mqtt.connection-state-trigger");

        using var document = JsonDocument.Parse(updated);
        var state = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.StateSourceNodeName);

        state.GetProperty("configuration").GetProperty("connection").GetString()
            .ShouldBe(FlowDefinitionComposer.BrokerResourceName);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(updated));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        using var host = FlowApplicationHost.CreateDefault(configuration);
        var result = host.Build();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
    }

    [Fact]
    public void AddComponent_FlowAssertionCreatesDefaultExpressionAndBuildableDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "flow.assert");

        using var document = JsonDocument.Parse(updated);
        var assertion = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(FlowDefinitionComposer.AssertionNodeName);

        assertion.GetProperty("Input").GetString().ShouldBe($"{FlowDefinitionComposer.TriggerNodeName}.Output");
        assertion.GetProperty("configuration").GetProperty("assertionName").GetString().ShouldBe("QoS at least once");
        assertion.GetProperty("configuration").GetProperty("inputType").GetString().ShouldBe("MqttEnvelope");
        assertion.GetProperty("configuration").GetProperty("expression").GetString().ShouldBe("qos >= 1");
        assertion.GetProperty("configuration").GetProperty("failureMessage").GetString().ShouldBe("Expected QoS to be at least 1.");
        assertion.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32().ShouldBe(1000);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(updated));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        using var host = FlowApplicationHost.CreateDefault(configuration);
        var result = host.Build();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
    }

    [Fact]
    public void AddComponent_FlowLoggerWithoutErrorLinksCreatesBuildableDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateInspectPayloadsDefinition(
            new MqttConnectionProfile { Name = "broker", Host = "localhost", Port = 1883, ClientId = "client" },
            "#");

        var updated = composer.AddComponent(initial, "flow.logger");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(updated));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();

        using var host = FlowApplicationHost.CreateDefault(configuration);
        var result = host.Build();

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.RuntimeBuild?.Errors.Select(error => error.Message) ?? []));
    }

    [Theory]
    [InlineData("mqtt.recorder", FlowDefinitionComposer.RecorderNodeName)]
    [InlineData("file.writer", "fileWriter")]
    public void AddComponent_AddsActorBufferConfiguration(string componentType, string nodeName)
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.CreateEmptyDefinition();
        var withWorkflow = composer.AddWorkflow(initial, FlowDefinitionComposer.DefaultWorkflowName);

        var updated = composer.AddComponent(withWorkflow, componentType);

        using var document = JsonDocument.Parse(updated);
        var actor = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(nodeName);

        actor.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32()
            .ShouldBe(1000);
    }

    [Theory]
    [InlineData("generated.source", FlowDefinitionComposer.GeneratedNodeName, "messages")]
    [InlineData("session.source", FlowDefinitionComposer.StoredSourceNodeName, "sessionId")]
    [InlineData("replay.source", FlowDefinitionComposer.ReplayNodeName, "speed")]
    [InlineData("timer.interval", FlowDefinitionComposer.TimerIntervalNodeName, "intervalMilliseconds")]
    [InlineData("timer.schedule", FlowDefinitionComposer.TimerScheduleNodeName, "cron")]
    public void AddComponent_AddsSourceConfiguration(string componentType, string nodeName, string expectedProperty)
    {
        var composer = new FlowDefinitionComposer();
        var initial = composer.AddWorkflow(composer.CreateEmptyDefinition(), FlowDefinitionComposer.DefaultWorkflowName);

        var updated = composer.AddComponent(initial, componentType);

        using var document = JsonDocument.Parse(updated);
        var source = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty(FlowDefinitionComposer.DefaultWorkflowName)
            .GetProperty(nodeName);

        source.GetProperty("type").GetString().ShouldBe(componentType);
        source.GetProperty("configuration").TryGetProperty(expectedProperty, out _).ShouldBeTrue();
        source.GetProperty("configuration").GetProperty("boundedCapacity").GetInt32()
            .ShouldBe(1000);
    }

    [Fact]
    public void GetWorkflowNames_ReturnsEmptyListForEmptyDefinition()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();

        var names = composer.GetWorkflowNames(json);

        names.ShouldBeEmpty();
    }

    [Fact]
    public void GetWorkflowNames_ReturnsWorkflowNamesInOrder()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddWorkflow(json, "alpha");
        json = composer.AddWorkflow(json, "beta");

        var names = composer.GetWorkflowNames(json);

        names.ShouldBe(["alpha", "beta"]);
    }

    [Fact]
    public void GetDashboardAndTestNames_ReturnsArtifactNamesInOrder()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {}
              },
              "dashboards": {
                "ops": {},
                "debug": {}
              },
              "tests": {
                "roundTrip": {},
                "timeout": {}
              }
            }
          }
        }
        """;

        composer.GetDashboardNames(json).ShouldBe(["ops", "debug"]);
        composer.GetTestNames(json).ShouldBe(["roundTrip", "timeout"]);
    }

    [Fact]
    public void AddWorkflow_IsIdempotentForDuplicateName()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddWorkflow(json, "pipe");
        json = composer.AddWorkflow(json, "pipe");

        var names = composer.GetWorkflowNames(json);

        names.ShouldBe(["pipe"]);
    }

    [Fact]
    public void AddDashboard_AddsDefaultGridDashboardWithoutTouchingWorkflows()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddWorkflow(json, "pipe");

        var updated = composer.AddDashboard(json, "ops");

        using var document = JsonDocument.Parse(updated);
        var flowApplication = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication");

        flowApplication.GetProperty("workflows").TryGetProperty("pipe", out _).ShouldBeTrue();

        var dashboard = flowApplication
            .GetProperty("dashboards")
            .GetProperty("ops");

        dashboard.GetProperty("layout")
            .GetProperty("columns")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["320", "*"]);
        dashboard.GetProperty("layout")
            .GetProperty("columnPadding")
            .EnumerateArray()
            .Select(item => item.GetDouble())
            .ShouldBe([0d, 0d]);
        dashboard.GetProperty("layout")
            .GetProperty("rowPadding")
            .EnumerateArray()
            .Select(item => item.GetDouble())
            .ShouldBe([0d, 0d]);
        dashboard.GetProperty("widgets").EnumerateObject().Count().ShouldBe(0);
    }

    [Fact]
    public void GetDashboardLayout_ReadsTracksCellsAndWidgetCount()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "dashboards": {
                "ops": {
                  "layout": {
                    "columns": ["240", "2*", "25%"],
                    "rows": ["96", "*"],
                    "columnPadding": [0, 8, 16],
                    "rowPadding": [4, 12],
                    "cells": {
                      "main": {
                        "row": 0,
                        "column": 1,
                        "rowSpan": 2,
                        "columnSpan": 2,
                        "widget": "latest"
                      }
                    }
                  },
                  "widgets": {
                    "latest": { "type": "payload.latest" }
                  }
                }
              }
            }
          }
        }
        """;

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();

        layout.Columns.ShouldBe(["240", "2*", "25%"]);
        layout.Rows.ShouldBe(["96", "*"]);
        layout.ColumnPadding.ShouldBe([0d, 8d, 16d]);
        layout.RowPadding.ShouldBe([4d, 12d]);
        layout.WidgetCount.ShouldBe(1);
        var cell = layout.Cells.ShouldHaveSingleItem();
        cell.Name.ShouldBe("main");
        cell.Row.ShouldBe(0);
        cell.Column.ShouldBe(1);
        cell.RowSpan.ShouldBe(2);
        cell.ColumnSpan.ShouldBe(2);
        cell.Widget.ShouldBe("latest");
    }

    [Fact]
    public void AddDashboardWidget_AddsWidgetAndAssignsSelectedSlot()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        var updated = composer.AddDashboardWidget(json, "ops", "event.counter", "slot:0:1");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Widgets.Keys.ShouldBe(["eventCounter"]);
        layout.Widgets["eventCounter"].Type.ShouldBe("event.counter");
        layout.Widgets["eventCounter"].Configuration.Keys.ShouldBe(["title", "metric"], ignoreOrder: true);
        layout.Widgets["eventCounter"].Configuration["metric"].ShouldBe("ops.eventCounterMetric");
        layout.Metrics["ops.eventCounterMetric"].Aggregation.ShouldBe("count");
        layout.Bindings["eventCounter"].PrimaryMetric.ShouldBe("ops.eventCounterMetric");

        var cell = layout.Cells.ShouldHaveSingleItem();
        cell.Row.ShouldBe(0);
        cell.Column.ShouldBe(1);
        cell.Widget.ShouldBe("eventCounter");
    }

    [Fact]
    public void AddDashboardWidget_AddsEventRateDefaults()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        var updated = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventRateType, "slot:0:0");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Widgets.Keys.ShouldBe(["eventRate"]);
        var widget = layout.Widgets["eventRate"];
        widget.Type.ShouldBe(DashboardWidgetCatalog.EventRateType);
        widget.Configuration["title"].ShouldBe("Event rate");
        widget.Configuration.Keys.ShouldBe(["title", "metric"], ignoreOrder: true);
        widget.Configuration["metric"].ShouldBe("ops.eventRateMetric");
        layout.Metrics["ops.eventRateMetric"].Aggregation.ShouldBe("rate");
        layout.Bindings["eventRate"].PrimaryMetric.ShouldBe("ops.eventRateMetric");
        layout.Cells.ShouldContain(cell => cell.Widget == "eventRate");
    }

    [Fact]
    public void AddDashboardWidget_AddsVisualDashboardWidgetDefaults()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventGaugeType, "slot:0:0");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventChartType, "slot:0:1");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventTableType, "slot:1:0");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.TopicActivityType, "slot:1:1");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.TopicTreeType, "slot:2:0");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        layout.Widgets["eventGauge"].Configuration["title"].ShouldBe("Event gauge");
        layout.Widgets["eventGauge"].Configuration[DashboardWidgetCatalog.PrimaryMetricKey].ShouldBe(DashboardWidgetCatalog.MetricRecent);
        layout.Widgets["eventGauge"].Configuration[DashboardWidgetCatalog.GaugeStyleKey].ShouldBe(DashboardWidgetCatalog.GaugeStyleRing);
        layout.Widgets["eventGauge"].Configuration.ContainsKey(DashboardWidgetCatalog.DisplayMetricsKey).ShouldBeFalse();
        layout.Widgets["eventGauge"].Configuration.ContainsKey(DashboardWidgetCatalog.MetricCardColumnsKey).ShouldBeFalse();
        layout.Widgets["barChart"].Type.ShouldBe(DashboardWidgetCatalog.BarChartType);
        layout.Widgets["barChart"].Configuration["title"].ShouldBe("Bar chart");
        layout.Widgets["barChart"].Configuration[DashboardWidgetCatalog.PrimaryMetricKey].ShouldBe(DashboardWidgetCatalog.MetricMessages);
        layout.Widgets["barChart"].Configuration[DashboardWidgetCatalog.ChartTypeKey].ShouldBe(DashboardWidgetCatalog.ChartTypeBars);
        layout.Widgets["barChart"].Configuration.ContainsKey(DashboardWidgetCatalog.DisplayMetricsKey).ShouldBeFalse();
        layout.Widgets["barChart"].Configuration.ContainsKey(DashboardWidgetCatalog.MetricCardColumnsKey).ShouldBeFalse();
        layout.Widgets["eventTable"].Configuration["title"].ShouldBe("Event table");
        layout.Widgets["eventTable"].Configuration["eventType"].ShouldBe(string.Empty);
        layout.Widgets["eventTable"].Configuration["status"].ShouldBe(string.Empty);
        layout.Widgets["topicActivity"].Configuration["title"].ShouldBe("Topic activity");
        layout.Widgets["topicActivity"].Configuration["eventType"].ShouldBe(string.Empty);
        layout.Widgets["topicActivity"].Configuration["topicStartsWith"].ShouldBe(string.Empty);
        layout.Widgets["topicTree"].Configuration["title"].ShouldBe("Topic tree");
        layout.Widgets["topicTree"].Configuration[DashboardWidgetCatalog.ExcludeSystemTopicsKey].ShouldBe("true");
    }

    [Fact]
    public void AddDashboardWidget_AppendsUniqueWidgetsWithoutReplacingCells()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardCell(json, "ops");

        json = composer.AddDashboardWidget(json, "ops", "event.counter", "cell");
        json = composer.AddDashboardWidget(json, "ops", "event.counter", "slot:1:0");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        layout.Widgets.Keys.ShouldBe(["eventCounter", "eventCounter2"]);
        layout.Cells.Select(cell => (cell.Name, cell.Widget)).ShouldBe([
            ("cell", "eventCounter"),
            ("cell2", "eventCounter2")
        ]);
    }

    [Fact]
    public void UpdateDashboardWidgetConfiguration_ReplacesWidgetConfiguration()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", "event.counter", "slot:0:0");

        var updated = composer.UpdateDashboardWidgetConfiguration(
            json,
            "ops",
            "eventCounter",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Factory errors",
                ["eventType"] = "mqtt.message.received",
                ["topicStartsWith"] = "factory/",
                ["subjectStartsWith"] = "factory/",
                ["status"] = "failed"
            });

        var widget = composer.GetDashboardLayout(updated, "ops")
            .ShouldNotBeNull()
            .Widgets["eventCounter"];
        widget.Type.ShouldBe("event.counter");
        widget.Configuration["title"].ShouldBe("Factory errors");
        widget.Configuration["eventType"].ShouldBe("mqtt.message.received");
        widget.Configuration["topicStartsWith"].ShouldBe("factory/");
        widget.Configuration["subjectStartsWith"].ShouldBe("factory/");
        widget.Configuration["status"].ShouldBe("failed");
    }

    [Fact]
    public void UpdateDashboardWidgetConfiguration_UnknownWidgetLeavesDefinitionUnchanged()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        var updated = composer.UpdateDashboardWidgetConfiguration(
            json,
            "ops",
            "missing",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Ignored"
            });

        updated.ShouldBe(json);
    }

    [Fact]
    public void UpdateDashboardCellStyle_WritesStyleOnCellNotWidget()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardWidget(json, "ops", "event.counter", "slot:0:0");

        var updated = composer.UpdateDashboardCellStyle(
            json,
            "ops",
            "cell",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["background"] = "#161b24",
                ["borderMode"] = "none",
                ["radius"] = "14"
            });

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        var cell = layout.Cells.ShouldHaveSingleItem();
        cell.Style["background"].ShouldBe("#161b24");
        cell.Style["borderMode"].ShouldBe("none");
        cell.Style["radius"].ShouldBe("14");
        layout.Widgets["eventCounter"].Configuration.Keys.ShouldNotContain("background");
        layout.Widgets["eventCounter"].Configuration.Keys.ShouldNotContain("mainText");
    }

    [Fact]
    public void RemoveDashboardWidget_RemovesWidgetAndClearsCellReference()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddDashboard(json, "ops");
        json = composer.AddDashboardWidget(json, "ops", "event.counter", "slot:0:0");

        var updated = composer.RemoveDashboardWidget(json, "ops", "eventCounter");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Widgets.ContainsKey("eventCounter").ShouldBeFalse();
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 0 && string.IsNullOrWhiteSpace(cell.Widget));
    }

    [Fact]
    public void MoveDashboardWidget_MovesWidgetToOpenSlot()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddDashboard(json, "ops");
        json = composer.AddDashboardWidget(json, "ops", "event.counter", "slot:0:0");

        var updated = composer.MoveDashboardWidget(json, "ops", "eventCounter", "slot:0:1");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 0 && string.IsNullOrWhiteSpace(cell.Widget));
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 1 && cell.Widget == "eventCounter");
    }

    [Fact]
    public void MoveDashboardWidget_SwapsWidgetsWhenTargetCellIsOccupied()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddDashboard(json, "ops");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventCounterType, "slot:0:0");
        json = composer.AddDashboardWidget(json, "ops", DashboardWidgetCatalog.EventRateType, "slot:0:1");

        var updated = composer.MoveDashboardWidget(json, "ops", "eventCounter", "cell2");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 0 && cell.Widget == "eventRate");
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 1 && cell.Widget == "eventCounter");
    }

    [Fact]
    public void UpdateDashboardGridTracks_NormalizesWpfLikeSizes()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        var updated = composer.UpdateDashboardGridTracks(
            json,
            "ops",
            ["320px", "2*", "25%"],
            ["120", "*"]);

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Columns.ShouldBe(["320", "2*", "25%"]);
        layout.Rows.ShouldBe(["120", "*"]);
        layout.ColumnPadding.ShouldBe([0d, 0d, 0d]);
        layout.RowPadding.ShouldBe([0d, 0d]);
    }

    [Fact]
    public void UpdateDashboardTrack_UpdatesSingleTrackSizeAndPadding()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        json = composer.UpdateDashboardTrack(json, "ops", "column", 1, "48%", 12);
        json = composer.UpdateDashboardTrack(json, "ops", "row", 0, "260px", 6.5);

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();
        layout.Columns.ShouldBe(["320", "48%"]);
        layout.Rows.ShouldBe(["260", "*"]);
        layout.ColumnPadding.ShouldBe([0d, 12d]);
        layout.RowPadding.ShouldBe([6.5d, 0d]);
    }

    [Fact]
    public void UpdateDashboardGridTracks_RejectsEmptyAndInvalidTracks()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");

        Should.Throw<FormatException>(() =>
            composer.UpdateDashboardGridTracks(json, "ops", [], ["*"]));

        Should.Throw<FormatException>(() =>
            composer.UpdateDashboardGridTracks(json, "ops", ["120%"], ["*"]));
    }

    [Fact]
    public void AddDashboardCell_FillsOpenSlotsThenAddsRow()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.UpdateDashboardGridTracks(json, "ops", ["*", "*"], ["*"]);

        json = composer.AddDashboardCell(json, "ops");
        json = composer.AddDashboardCell(json, "ops");
        json = composer.AddDashboardCell(json, "ops");

        var layout = composer.GetDashboardLayout(json, "ops").ShouldNotBeNull();

        layout.Rows.ShouldBe(["*", "*"]);
        layout.Cells.Select(cell => (cell.Name, cell.Row, cell.Column))
            .ShouldBe([
                ("cell", 0, 0),
                ("cell2", 0, 1),
                ("cell3", 1, 0)
            ]);
    }

    [Fact]
    public void RemoveDashboardCell_RemovesOnlyRequestedCell()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.AddDashboardCell(json, "ops");
        json = composer.AddDashboardCell(json, "ops");

        var updated = composer.RemoveDashboardCell(json, "ops", "cell");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Cells.Select(cell => cell.Name).ShouldBe(["cell2"]);
    }

    [Fact]
    public void ResizeDashboardGrid_KeepsTrackSizesAndRemovesOverflowCells()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.UpdateDashboardGridTracks(json, "ops", ["320", "2*", "25%"], ["180", "*"]);
        json = composer.UpdateDashboardTrack(json, "ops", "column", 1, "2*", 7);
        json = composer.UpdateDashboardTrack(json, "ops", "row", 0, "180", 5);
        json = composer.AddDashboardCell(json, "ops");
        json = composer.AddDashboardCell(json, "ops");
        json = composer.AddDashboardCell(json, "ops");

        var updated = composer.ResizeDashboardGrid(json, "ops", rowCount: 1, columnCount: 2);

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Columns.ShouldBe(["320", "2*"]);
        layout.Rows.ShouldBe(["180"]);
        layout.ColumnPadding.ShouldBe([0d, 7d]);
        layout.RowPadding.ShouldBe([5d]);
        layout.Cells.Select(cell => cell.Name).ShouldBe(["cell", "cell2"]);
    }

    [Fact]
    public void MergeDashboardCells_CreatesRectangularSpanFromSlots()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.UpdateDashboardGridTracks(json, "ops", ["*", "*"], ["*", "*"]);

        var updated = composer.MergeDashboardCells(
            json,
            "ops",
            [
                DashboardCellSnapshot.Slot(0, 0),
                DashboardCellSnapshot.Slot(0, 1),
                DashboardCellSnapshot.Slot(1, 0),
                DashboardCellSnapshot.Slot(1, 1)
            ]);

        var cell = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull().Cells.ShouldHaveSingleItem();
        cell.Row.ShouldBe(0);
        cell.Column.ShouldBe(0);
        cell.RowSpan.ShouldBe(2);
        cell.ColumnSpan.ShouldBe(2);
    }

    [Fact]
    public void MergeDashboardCells_RejectsNonRectangularSelection()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.UpdateDashboardGridTracks(json, "ops", ["*", "*"], ["*", "*"]);

        var updated = composer.MergeDashboardCells(
            json,
            "ops",
            [DashboardCellSnapshot.Slot(0, 0), DashboardCellSnapshot.Slot(1, 1)]);

        updated.ShouldBe(json);
    }

    [Fact]
    public void SplitDashboardCell_RestoresUnitCells()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.UpdateDashboardGridTracks(json, "ops", ["*", "*"], ["*", "*"]);
        json = composer.MergeDashboardCells(
            json,
            "ops",
            [
                DashboardCellSnapshot.Slot(0, 0),
                DashboardCellSnapshot.Slot(0, 1),
                DashboardCellSnapshot.Slot(1, 0),
                DashboardCellSnapshot.Slot(1, 1)
            ]);

        var updated = composer.SplitDashboardCell(json, "ops", "cell");

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Cells.Count.ShouldBe(4);
        layout.Cells.ShouldAllBe(cell => cell.RowSpan == 1 && cell.ColumnSpan == 1);
    }

    [Fact]
    public void SubdivideDashboardCell_InsertsTracksAndShiftsNeighbors()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.AddDashboard(composer.CreateEmptyDefinition(), "ops");
        json = composer.UpdateDashboardGridTracks(json, "ops", ["80", "1*"], ["50%", "50%"]);
        json = composer.UpdateDashboardTrack(json, "ops", "column", 0, "80", 6);
        json = composer.UpdateDashboardTrack(json, "ops", "row", 0, "50%", 4);
        json = composer.AddDashboardCell(json, "ops");
        json = composer.AddDashboardCell(json, "ops");

        var updated = composer.SubdivideDashboardCell(json, "ops", DashboardCellSnapshot.Slot(0, 0), rowParts: 2, columnParts: 2);

        var layout = composer.GetDashboardLayout(updated, "ops").ShouldNotBeNull();
        layout.Rows.ShouldBe(["25%", "25%", "50%"]);
        layout.Columns.ShouldBe(["40", "40", "*"]);
        layout.RowPadding.ShouldBe([4d, 4d, 0d]);
        layout.ColumnPadding.ShouldBe([6d, 6d, 0d]);
        layout.Cells.Count.ShouldBe(6);
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 2 && cell.RowSpan == 2);
        layout.Cells.ShouldContain(cell => cell.Row == 0 && cell.Column == 0 && cell.RowSpan == 1 && cell.ColumnSpan == 1);
        layout.Cells.ShouldContain(cell => cell.Row == 1 && cell.Column == 1 && cell.RowSpan == 1 && cell.ColumnSpan == 1);
    }

    [Fact]
    public void AddTest_AddsEmptyScenarioArtifact()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();

        var updated = composer.AddTest(json, "roundTrip");

        using var document = JsonDocument.Parse(updated);
        var scenario = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("tests")
            .GetProperty("roundTrip");

        scenario.GetProperty("steps").EnumerateObject().Count().ShouldBe(0);
    }

    [Fact]
    public void GetTestScenario_ReadsOrderedScenarioSteps()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "tests": {
                "t1": {
                  "steps": {
                    "publishSampleRequest": {
                      "type": "mqtt.publisher",
                      "configuration": {
                        "connection": "local-broker",
                        "topic": "fluxmq/sample/request",
                        "payload": {
                          "value": 12
                        },
                        "payloadEncoding": "json",
                        "qos": 1,
                        "retain": false
                      }
                    },
                    "expectMappedPublish": {
                      "type": "expect.event",
                      "configuration": {
                        "eventType": "mqtt.message.published",
                        "topicStartsWith": "test",
                        "timeoutMs": 5000
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var scenario = composer.GetTestScenario(json, "t1").ShouldNotBeNull();

        scenario.Name.ShouldBe("t1");
        scenario.Steps.Select(step => step.Name).ShouldBe(["publishSampleRequest", "expectMappedPublish"]);
        scenario.Steps[0].Type.ShouldBe("mqtt.publisher");
        scenario.Steps[0].Configuration["connection"].ShouldBe("local-broker");
        scenario.Steps[0].Configuration["payload"].ShouldBe("""{"value":12}""");
        scenario.Steps[1].Configuration["timeoutMs"].ShouldBe("5000");
    }

    [Fact]
    public void AddUpdateRemoveScenarioStep_ModifiesScenarioSteps()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddTest(json, "t1");

        json = composer.AddScenarioStep(json, "t1", ScenarioStepTypes.MqttPublisher);
        var scenario = composer.GetTestScenario(json, "t1").ShouldNotBeNull();
        var step = scenario.Steps.ShouldHaveSingleItem();
        step.Name.ShouldBe("publishMessage");
        step.Type.ShouldBe(ScenarioStepTypes.MqttPublisher);
        step.Configuration[ScenarioStepCatalog.TopicKey].ShouldBe("fluxmq/test");
        step.Configuration[ScenarioStepCatalog.PayloadKey].ShouldBe("""{"hello":"fluxmq"}""");
        step.Configuration[ScenarioStepCatalog.PayloadEncodingKey].ShouldBe("json");
        step.Configuration[ScenarioStepCatalog.QosKey].ShouldBe("0");
        step.Configuration[ScenarioStepCatalog.RetainKey].ShouldBe("false");

        json = composer.UpdateScenarioStep(
            json,
            "t1",
            step.Name,
            step.Type,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["connection"] = "local-broker",
                ["topic"] = "factory/request",
                ["payload"] = """{"value":42}""",
                ["payloadEncoding"] = "json",
                ["qos"] = "1",
                ["retain"] = "true"
            });

        scenario = composer.GetTestScenario(json, "t1").ShouldNotBeNull();
        scenario.Steps[0].Configuration["topic"].ShouldBe("factory/request");
        scenario.Steps[0].Configuration["payload"].ShouldBe("""{"value":42}""");
        scenario.Steps[0].Configuration["qos"].ShouldBe("1");
        scenario.Steps[0].Configuration["retain"].ShouldBe("true");

        json = composer.RemoveScenarioStep(json, "t1", step.Name);

        composer.GetTestScenario(json, "t1").ShouldNotBeNull().Steps.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateScenarioStep_WritesEventAttributeFilters()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddTest(json, "t1");
        json = composer.AddScenarioStep(json, "t1", "expect.event");
        var step = composer.GetTestScenario(json, "t1").ShouldNotBeNull().Steps.ShouldHaveSingleItem();

        json = composer.UpdateScenarioStep(
            json,
            "t1",
            step.Name,
            step.Type,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["eventType"] = "json.schema.validated",
                ["topicStartsWith"] = "factory/",
                ["status"] = "valid",
                [DashboardEventFilterCatalog.AttributeFilterKey("qos")] = "1",
                [DashboardEventFilterCatalog.AttributeFilterKey("retain")] = "false",
                [DashboardEventFilterCatalog.AttributeFilterKey("schemaId")] = "temperature",
                ["timeoutMs"] = "2500"
            });

        var scenario = composer.GetTestScenario(json, "t1").ShouldNotBeNull();
        scenario.Steps[0].Configuration[DashboardEventFilterCatalog.AttributeFilterKey("schemaId")]
            .ShouldBe("temperature");

        using var document = JsonDocument.Parse(json);
        var attributes = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("tests")
            .GetProperty("t1")
            .GetProperty("steps")
            .GetProperty(step.Name)
            .GetProperty("configuration")
            .GetProperty("attributes");

        attributes.GetProperty("schemaId").GetString().ShouldBe("temperature");
        attributes.GetProperty("qos").GetString().ShouldBe("1");
        attributes.GetProperty("retain").GetString().ShouldBe("false");
    }

    [Fact]
    public void UpdateScenarioStep_WritesMqttTriggerConfiguration()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddTest(json, "t1");
        json = composer.AddScenarioStep(json, "t1", ScenarioStepTypes.MqttTrigger);
        var step = composer.GetTestScenario(json, "t1").ShouldNotBeNull().Steps.ShouldHaveSingleItem();

        step.Name.ShouldBe("triggerMqtt");
        step.Type.ShouldBe(ScenarioStepTypes.MqttTrigger);
        step.Configuration[ScenarioStepCatalog.SubscriptionsKey].ShouldBe("fluxmq/test/#");
        step.Configuration[ScenarioStepCatalog.QosKey].ShouldBe("1");
        step.Configuration[ScenarioStepCatalog.ReceiveRetainedKey].ShouldBe("false");
        step.Configuration[ScenarioStepCatalog.RetainAsPublishedKey].ShouldBe("true");

        json = composer.UpdateScenarioStep(
            json,
            "t1",
            step.Name,
            step.Type,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["connection"] = "local-broker",
                ["subscriptions"] = "factory/response/+",
                ["qos"] = "2",
                ["receiveRetained"] = "true",
                ["retainAsPublished"] = "false"
            });

        using var document = JsonDocument.Parse(json);
        var configuration = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("tests")
            .GetProperty("t1")
            .GetProperty("steps")
            .GetProperty(step.Name)
            .GetProperty("configuration");

        configuration.GetProperty("connection").GetString().ShouldBe("local-broker");
        configuration.GetProperty("subscriptions").GetString().ShouldBe("factory/response/+");
        configuration.GetProperty("qos").GetInt32().ShouldBe(2);
        configuration.GetProperty("receiveRetained").GetBoolean().ShouldBeTrue();
        configuration.GetProperty("retainAsPublished").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void MoveScenarioStep_ReordersScenarioSteps()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddTest(json, "t1");
        json = composer.AddScenarioStep(json, "t1", ScenarioStepTypes.MqttPublisher);
        json = composer.AddScenarioStep(json, "t1", "expect.event");
        json = composer.AddScenarioStep(json, "t1", "expect.event");

        json = composer.MoveScenarioStep(json, "t1", "expectEvent2", -1);

        composer.GetTestScenario(json, "t1")
            .ShouldNotBeNull()
            .Steps
            .Select(step => step.Name)
            .ShouldBe(["publishMessage", "expectEvent2", "expectEvent"]);

        json = composer.MoveScenarioStep(json, "t1", "publishMessage", 10);

        composer.GetTestScenario(json, "t1")
            .ShouldNotBeNull()
            .Steps
            .Select(step => step.Name)
            .ShouldBe(["expectEvent2", "expectEvent", "publishMessage"]);
    }

    [Fact]
    public void RemoveWorkflow_RemovesNamedWorkflow()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddWorkflow(json, "alpha");
        json = composer.AddWorkflow(json, "beta");

        json = composer.RemoveWorkflow(json, "alpha");

        composer.GetWorkflowNames(json).ShouldBe(["beta"]);
    }

    [Fact]
    public void RemoveWorkflow_IsNoOpForMissingName()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddWorkflow(json, "pipe");

        json = composer.RemoveWorkflow(json, "nonexistent");

        composer.GetWorkflowNames(json).ShouldBe(["pipe"]);
    }

    [Fact]
    public void RemoveDashboard_RemovesNamedDashboard()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddDashboard(json, "ops");
        json = composer.AddDashboard(json, "debug");

        json = composer.RemoveDashboard(json, "ops");

        composer.GetDashboardNames(json).ShouldBe(["debug"]);
    }

    [Fact]
    public void RemoveDashboard_IsNoOpForMissingName()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddDashboard(json, "ops");

        json = composer.RemoveDashboard(json, "missing");

        composer.GetDashboardNames(json).ShouldBe(["ops"]);
    }

    [Fact]
    public void RemoveTest_RemovesNamedTest()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddTest(json, "roundTrip");
        json = composer.AddTest(json, "timeout");

        json = composer.RemoveTest(json, "roundTrip");

        composer.GetTestNames(json).ShouldBe(["timeout"]);
    }

    [Fact]
    public void RemoveTest_IsNoOpForMissingName()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddTest(json, "roundTrip");

        json = composer.RemoveTest(json, "missing");

        composer.GetTestNames(json).ShouldBe(["roundTrip"]);
    }

    [Fact]
    public void AddComponent_UsesTargetWorkflowNameWhenProvided()
    {
        var composer = new FlowDefinitionComposer();
        var json = composer.CreateEmptyDefinition();
        json = composer.AddWorkflow(json, "myPipeline");

        json = composer.AddComponent(json, "mqtt.payload-inspector", "myPipeline");

        using var document = JsonDocument.Parse(json);
        var workflows = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows");

        workflows.TryGetProperty("myPipeline", out var myPipeline).ShouldBeTrue();
        myPipeline.TryGetProperty(FlowDefinitionComposer.InspectorNodeName, out _).ShouldBeTrue();
    }

    [Fact]
    public void UpdateNodeConfiguration_UsesWorkflowScopeWhenNodeNamesRepeat()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pip1": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "connection": "broker1",
                      "subscriptions": ["one/#"]
                    }
                  }
                },
                "pip2": {
                  "trigger": {
                    "type": "mqtt.trigger",
                    "configuration": {
                      "subscriptions": ["two/#"]
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.UpdateNodeConfiguration(
            json,
            "trigger",
            new System.Text.Json.Nodes.JsonObject
            {
                ["connection"] = "broker2",
                ["subscriptions"] = new System.Text.Json.Nodes.JsonArray("two/#"),
                ["boundedCapacity"] = 1000
            },
            "pip2");

        using var document = JsonDocument.Parse(updated);
        var workflows = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows");

        workflows.GetProperty("pip1")
            .GetProperty("trigger")
            .GetProperty("configuration")
            .GetProperty("connection")
            .GetString()
            .ShouldBe("broker1");

        workflows.GetProperty("pip2")
            .GetProperty("trigger")
            .GetProperty("configuration")
            .GetProperty("connection")
            .GetString()
            .ShouldBe("broker2");
    }

    [Fact]
    public void ConnectWorkflowPorts_ReplacesTargetPortLink()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "filter": { "type": "flow.filter", "Input": "trigger.Output" },
                  "inspect": { "type": "mqtt.payload-inspector", "Input": "trigger.Output" }
                }
              }
            }
          }
        }
        """;

        var updated = composer.ConnectWorkflowPorts(json, "pipe", "filter", "Output", "inspect", "Input");

        using var document = JsonDocument.Parse(updated);
        var inspect = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty("inspect");

        inspect.GetProperty("Input").GetString().ShouldBe("filter.Output");
    }

    [Fact]
    public void ConnectWorkflowPorts_CanAppendTargetPortLinks()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "mapper": { "type": "flow.mapper" },
                  "logger": { "type": "flow.logger", "FlowErrors": "trigger.Errors" }
                }
              }
            }
          }
        }
        """;

        var updated = composer.ConnectWorkflowPorts(json, "pipe", "mapper", "Errors", "logger", "FlowErrors", replaceTargetPortLinks: false);

        using var document = JsonDocument.Parse(updated);
        var links = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty("logger")
            .GetProperty("FlowErrors");

        links.EnumerateArray().Select(item => item.GetString())
            .ShouldBe(["trigger.Errors", "mapper.Errors"]);
    }

    [Fact]
    public void UpdateWorkflowPortLinkCondition_ConvertsStringLinkToConditionalObject()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "inspect": { "type": "mqtt.payload-inspector", "Input": "trigger.Output" }
                }
              }
            }
          }
        }
        """;

        var updated = composer.UpdateWorkflowPortLinkCondition(
            json,
            "pipe",
            "trigger",
            "Output",
            "inspect",
            "Input",
            "input.Topic.StartsWith(\"factory/\")");

        using var document = JsonDocument.Parse(updated);
        var input = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty("inspect")
            .GetProperty("Input");

        input.GetProperty("from").GetString().ShouldBe("trigger.Output");
        input.GetProperty("when").GetString().ShouldBe("input.Topic.StartsWith(\"factory/\")");
    }

    [Fact]
    public void UpdateWorkflowPortLinkCondition_UpdatesOnlyMatchingArrayLink()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "filter": { "type": "flow.filter" },
                  "logger": {
                    "type": "flow.logger",
                    "Input": [ "trigger.Output", "filter.Output" ]
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.UpdateWorkflowPortLinkCondition(
            json,
            "pipe",
            "filter",
            "Output",
            "logger",
            "Input",
            "input.Retain == false");

        using var document = JsonDocument.Parse(updated);
        var links = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty("logger")
            .GetProperty("Input")
            .EnumerateArray()
            .ToArray();

        links[0].GetString().ShouldBe("trigger.Output");
        links[1].GetProperty("from").GetString().ShouldBe("filter.Output");
        links[1].GetProperty("when").GetString().ShouldBe("input.Retain == false");
    }

    [Fact]
    public void UpdateWorkflowPortLinkCondition_ClearsConditionBackToStringWhenObjectHasNoOtherMetadata()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": {
                      "from": "trigger.Output",
                      "when": "input.Topic.StartsWith(\"factory/\")"
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.UpdateWorkflowPortLinkCondition(
            json,
            "pipe",
            "trigger",
            "Output",
            "inspect",
            "Input",
            "");

        using var document = JsonDocument.Parse(updated);
        var input = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty("inspect")
            .GetProperty("Input");

        input.GetString().ShouldBe("trigger.Output");
    }

    [Fact]
    public void GetWorkflowPortLinkCondition_ReadsConditionalObject()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": {
                      "from": "trigger.Output",
                      "when": "input.Topic.StartsWith(\"factory/\")"
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var condition = composer.GetWorkflowPortLinkCondition(
            json,
            "pipe",
            "trigger",
            "Output",
            "inspect",
            "Input");

        condition.ShouldBe("input.Topic.StartsWith(\"factory/\")");
    }

    [Fact]
    public void GetWorkflowPortLinkCondition_ReturnsNullForUnconditionalLink()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "inspect": { "type": "mqtt.payload-inspector", "Input": "trigger.Output" }
                }
              }
            }
          }
        }
        """;

        var condition = composer.GetWorkflowPortLinkCondition(
            json,
            "pipe",
            "trigger",
            "Output",
            "inspect",
            "Input");

        condition.ShouldBeNull();
    }

    [Fact]
    public void RemoveWorkflowPortLink_RemovesOnlyMatchingReference()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "mapper": { "type": "flow.mapper" },
                  "logger": {
                    "type": "flow.logger",
                    "FlowErrors": [ "trigger.Errors", "mapper.Errors" ]
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.RemoveWorkflowPortLink(json, "pipe", "trigger", "Errors", "logger", "FlowErrors");

        using var document = JsonDocument.Parse(updated);
        var links = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe")
            .GetProperty("logger")
            .GetProperty("FlowErrors");

        links.EnumerateArray().Select(item => item.GetString())
            .ShouldBe(["mapper.Errors"]);
    }

    [Fact]
    public void RemoveWorkflowNode_RemovesNodeAndReferencesToItsOutputs()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "trigger": { "type": "mqtt.trigger" },
                  "filter": {
                    "type": "flow.filter",
                    "Input": "trigger.Output"
                  },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": "filter.Output"
                  },
                  "logger": {
                    "type": "flow.logger",
                    "Input": [ "trigger.Output", "filter.Output" ],
                    "FlowErrors": [ "trigger.Errors", "filter.Errors" ]
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.RemoveWorkflowNode(json, "pipe", "filter");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe");

        workflow.TryGetProperty("filter", out _).ShouldBeFalse();
        workflow.GetProperty("inspect").TryGetProperty("Input", out _).ShouldBeFalse();

        workflow.GetProperty("logger")
            .GetProperty("Input")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["trigger.Output"]);

        workflow.GetProperty("logger")
            .GetProperty("FlowErrors")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["trigger.Errors"]);
    }

    [Fact]
    public void RenameWorkflowNode_DoesNotOverwriteExistingNode()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "filter": { "type": "flow.filter" },
                  "inspect": { "type": "mqtt.payload-inspector" }
                }
              }
            }
          }
        }
        """;

        var updated = composer.RenameWorkflowNode(json, "pipe", "filter", "inspect");

        updated.ShouldBe(json);
    }

    [Fact]
    public void RenameWorkflowNode_TrimsNamesAndUpdatesReferences()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pipe": {
                  "filter": { "type": "flow.filter" },
                  "inspect": {
                    "type": "mqtt.payload-inspector",
                    "Input": "filter.Output"
                  },
                  "logger": {
                    "type": "flow.logger",
                    "Input": [ "filter.Output", "inspect.Output" ],
                    "FlowErrors": {
                      "from": "filter.Errors",
                      "when": "severity == \"Error\""
                    }
                  }
                }
              }
            }
          }
        }
        """;

        var updated = composer.RenameWorkflowNode(json, "pipe", " filter ", " clean ");

        using var document = JsonDocument.Parse(updated);
        var workflow = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows")
            .GetProperty("pipe");

        workflow.TryGetProperty("filter", out _).ShouldBeFalse();
        workflow.TryGetProperty("clean", out _).ShouldBeTrue();
        workflow.GetProperty("inspect")
            .GetProperty("Input")
            .GetString()
            .ShouldBe("clean.Output");
        workflow.GetProperty("logger")
            .GetProperty("Input")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["clean.Output", "inspect.Output"]);
        workflow.GetProperty("logger")
            .GetProperty("FlowErrors")
            .GetProperty("from")
            .GetString()
            .ShouldBe("clean.Errors");
        workflow.GetProperty("logger")
            .GetProperty("FlowErrors")
            .GetProperty("when")
            .GetString()
            .ShouldBe("severity == \"Error\"");
    }

    [Fact]
    public void RemoveWorkflowNode_UsesWorkflowScopeWhenNodeNamesRepeat()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pip1": {
                  "filter": { "type": "flow.filter" }
                },
                "pip2": {
                  "filter": { "type": "flow.filter" }
                }
              }
            }
          }
        }
        """;

        var updated = composer.RemoveWorkflowNode(json, "pip2", "filter");

        using var document = JsonDocument.Parse(updated);
        var workflows = document.RootElement
            .GetProperty("FluxMq")
            .GetProperty("FlowApplication")
            .GetProperty("workflows");

        workflows.GetProperty("pip1").TryGetProperty("filter", out _).ShouldBeTrue();
        workflows.GetProperty("pip2").TryGetProperty("filter", out _).ShouldBeFalse();
    }
}
