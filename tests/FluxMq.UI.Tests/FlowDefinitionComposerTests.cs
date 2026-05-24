using FluxMq.App;
using FluxMq.Core.Models;
using FluxMq.UI.Services;
using Shouldly;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace FluxMq.UI.Tests;

public sealed class FlowDefinitionComposerTests
{
    [Fact]
    public void ComponentCatalog_ExposesDynamicMapperInsteadOfRequestAliases()
    {
        var catalog = new FlowComponentCatalog();

        catalog.Components.ShouldContain(component => component.Type == "flow.mapper");
        catalog.Components.ShouldContain(component => component.Type == "flow.logger");
        catalog.Components.ShouldContain(component => component.Type == "mqtt.trigger");
        catalog.Components.ShouldNotContain(component => component.Type == "mqtt.publish-request");
        catalog.Components.ShouldNotContain(component => component.Type == "mqtt.recording-request");
        catalog.Components.ShouldNotContain(component => component.Type == "file.write-request");

        catalog.Find("mqtt.publish-request").ShouldNotBeNull();
    }

    [Fact]
    public void ComponentCatalog_TriggerUsesConfiguredBrokerWithoutConnectionPort()
    {
        var catalog = new FlowComponentCatalog();

        var descriptor = catalog.Find("mqtt.trigger").ShouldNotBeNull();

        descriptor.Ports.ShouldNotContain(port => port.Name == "Connection");
        descriptor.Ports.ShouldContain(port => port.Name == "Output" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Errors" && port.ValueType == "FlowError" && !port.IsInput);
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

        var router = catalog.Find("mqtt.condition-router").ShouldNotBeNull();
        router.Ports.ShouldContain(port => port.Name == "WhenTrue" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        router.Ports.ShouldContain(port => port.Name == "WhenFalse" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        router.Ports.ShouldContain(port => port.Name == "Entries" && port.ValueType == "FlowLogEntry" && !port.IsInput);

        var validator = catalog.Find("json.schema-validator").ShouldNotBeNull();
        validator.Ports.ShouldContain(port => port.Name == "Result" && port.ValueType == "JsonSchemaValidationResult" && !port.IsInput);
        validator.Ports.ShouldContain(port => port.Name == "Valid" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        validator.Ports.ShouldContain(port => port.Name == "Invalid" && port.ValueType == "MqttEnvelope" && !port.IsInput);
        validator.Ports.Last().Name.ShouldBe("Errors");
    }

    [Fact]
    public void ComponentCatalog_ResolvesMetricsSinkAlias()
    {
        var catalog = new FlowComponentCatalog();

        var descriptor = catalog.Find("mqtt.metrics-sink").ShouldNotBeNull();

        descriptor.DisplayName.ShouldBe("MQTT Metrics");
        descriptor.Ports.ShouldContain(port => port.Name == "Input" && port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Snapshots" && !port.IsInput);
        descriptor.Ports.ShouldContain(port => port.Name == "Errors" && !port.IsInput);
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

        var updated = composer.AddComponent(initial, "mqtt.condition-router");

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
    [InlineData("replay.source", FlowDefinitionComposer.ReplayNodeName, "speed")]
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
                  "filter": { "type": "mqtt.message-filter", "Input": "trigger.Output" },
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
                    "type": "mqtt.message-filter",
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
    public void RemoveWorkflowNode_UsesWorkflowScopeWhenNodeNamesRepeat()
    {
        var composer = new FlowDefinitionComposer();
        var json = """
        {
          "FluxMq": {
            "FlowApplication": {
              "workflows": {
                "pip1": {
                  "filter": { "type": "mqtt.message-filter" }
                },
                "pip2": {
                  "filter": { "type": "mqtt.message-filter" }
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
