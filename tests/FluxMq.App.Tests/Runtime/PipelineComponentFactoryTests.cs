using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Session;
using FluxMq.App;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using MQTTnet.Protocol;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.App.Tests.Runtime;

public sealed class PipelineComponentFactoryTests
{
    [Fact]
    public void RegisterPipelineComponentFactories_RegistersStableComponentTypes()
    {
        var registry = new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories();

        registry.Factories.Keys.ShouldBe(new[]
        {
            PipelineFlowNodeTypes.Connection,
            PipelineFlowNodeTypes.Trigger,
            PipelineFlowNodeTypes.StoredSessionSource,
            PipelineFlowNodeTypes.ReplaySource,
            PipelineFlowNodeTypes.GeneratedSource,
            PipelineFlowNodeTypes.PayloadInspector,
            PipelineFlowNodeTypes.MqttMetrics,
            PipelineFlowNodeTypes.MqttMetricsSink,
            PipelineFlowNodeTypes.FlowLogger,
            PipelineFlowNodeTypes.MessageFilter,
            PipelineFlowNodeTypes.JsonSchemaValidator,
            PipelineFlowNodeTypes.DynamicMapper,
            PipelineFlowNodeTypes.PublishRequestMapper,
            PipelineFlowNodeTypes.MqttPublisher,
            PipelineFlowNodeTypes.RecordingRequestMapper,
            PipelineFlowNodeTypes.MqttRecorder,
            PipelineFlowNodeTypes.FileWriteRequestMapper,
            PipelineFlowNodeTypes.FileWriter
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task PayloadInspectorFactory_CreatesLinkableRuntimeNode()
    {
        TestSourceNode? source = null;
        TestSinkNode<InspectedMqttMessage>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.inspected-sink"), (address, _) =>
            {
                sink = new TestSinkNode<InspectedMqttMessage>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source"),
                        ["inspect"] = NodeWithPort(PipelineFlowNodeTypes.PayloadInspector, "Input", "\"source.Output\""),
                        ["sink"] = NodeWithPort("test.inspected-sink", "Input", "\"inspect.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/json", Payload = """{"value":1}"""u8.ToArray() });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.ShouldHaveSingleItem();
        sink.Values[0].Envelope.Topic.ShouldBe("factory/json");
        sink.Values[0].Payload.Format.ShouldBe(PayloadFormat.Json);
    }

    [Fact]
    public async Task MqttMetricsFactory_CreatesLinkableRuntimeNode()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.snapshot-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttMetricsSnapshot>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source"),
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MqttMetrics, "Input", "\"source.Output\""),
                        ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = [1, 2] });
        source.Post(new MqttEnvelope { Topic = "factory/two", Payload = [1, 2, 3] });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Count.ShouldBe(2);
        sink.Values[^1].MessageCount.ShouldBe(2);
        sink.Values[^1].TotalPayloadBytes.ShouldBe(5);
    }

    [Fact]
    public async Task FlowLoggerFactory_CreatesLinkableRuntimeNode()
    {
        TestSourceNode? source = null;
        TestSinkNode<FlowLogEntry>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.log-sink"), (address, _) =>
            {
                sink = new TestSinkNode<FlowLogEntry>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source"),
                        ["logger"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.FlowLogger,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["includePayloadPreview"] = JsonDocument.Parse("true").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.log-sink", "Input", "\"logger.Entries\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = """{"value":1}"""u8.ToArray() });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        var entry = sink!.Values.ShouldHaveSingleItem();
        entry.Source.ShouldBe("MqttEnvelope");
        entry.Topic.ShouldBe("factory/one");
        entry.PayloadPreview.ShouldBe("""{"value":1}""");
    }

    [Fact]
    public async Task JsonSchemaValidatorFactory_CreatesLinkableRuntimeNode()
    {
        TestSourceNode? source = null;
        TestSinkNode<JsonSchemaValidationResult>? sink = null;
        const string schemaJson = """
        {
          "type": "object",
          "required": ["status"],
          "properties": {
            "status": { "const": "ok" }
          }
        }
        """;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.validation-sink"), (address, _) =>
            {
                sink = new TestSinkNode<JsonSchemaValidationResult>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source"),
                        ["validator"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.JsonSchemaValidator,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["schemaId"] = JsonDocument.Parse("\"status-schema\"").RootElement.Clone(),
                                ["schema"] = JsonDocument.Parse(JsonSerializer.Serialize(schemaJson)).RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.validation-sink", "Input", "\"validator.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = """{"status":"ok"}"""u8.ToArray() });
        source.Post(new MqttEnvelope { Topic = "factory/two", Payload = """{"status":"fault"}"""u8.ToArray() });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Count.ShouldBe(2);
        sink.Values[0].IsValid.ShouldBeTrue();
        sink.Values[1].IsValid.ShouldBeFalse();
        sink.Values[1].SchemaId.ShouldBe("status-schema");
    }

    [Fact]
    public async Task ConnectionAndTriggerFactories_StartSessionAndFeedWorkflowNodes()
    {
        FakeMqttSession? session = null;
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(_ =>
            {
                session = new FakeMqttSession();
                return session;
            })
            .Register(new NodeType("test.snapshot-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttMetricsSnapshot>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["broker"] = new NodeDefinition
                {
                    Type = PipelineFlowNodeTypes.Connection,
                    Configuration =
                    {
                        ["profile"] = JsonDocument.Parse("""{"name":"factory-broker","host":"localhost","port":1883}""").RootElement.Clone()
                    }
                }
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["trigger"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.Trigger,
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"broker\"").RootElement.Clone(),
                                ["subscriptions"] = JsonDocument.Parse("""["factory/#"]""").RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MqttMetrics, "Input", "\"trigger.Output\""),
                        ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        using var runtime = result.Runtime!;

        await runtime.StartAsync();

        session.ShouldNotBeNull();
        session!.ConnectCalls.ShouldBe(1);
        session.Subscriptions.ShouldContain(subscription =>
            subscription.TopicFilter == "factory/#" &&
            subscription.QualityOfService == MqttQualityOfServiceLevel.AtMostOnce);

        await session.WriteAsync(new MqttEnvelope
        {
            Topic = "factory/one",
            Payload = [1, 2, 3]
        });

        session.CompleteMessages();
        await runtime.Completion;

        sink!.Values.ShouldNotBeEmpty();
        sink.Values[^1].MessageCount.ShouldBe(1);
        sink.Values[^1].TotalPayloadBytes.ShouldBe(3);
    }

    [Fact]
    public async Task GeneratedSourceFactory_CreatesGeneratedSource()
    {
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.snapshot-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttMetricsSnapshot>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["generated"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.GeneratedSource,
                            Configuration =
                            {
                                ["messages"] = JsonDocument.Parse("""
                                [
                                  {"topic":"factory/one","payload":"one"},
                                  {"topic":"factory/two","payload":[1,2,3]}
                                ]
                                """).RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MqttMetrics, "Input", "\"generated.Output\""),
                        ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion;

        sink!.Values.ShouldNotBeEmpty();
        sink.Values[^1].MessageCount.ShouldBe(2);
        sink.Values[^1].TotalPayloadBytes.ShouldBe(6);
    }

    [Fact]
    public async Task DynamicFilterAndJsonataMapper_CanPublishMappedRequestsToConnection()
    {
        FakeMqttSession? session = null;
        const string mapperExpression = """
        {
          "topic": "mirror/" & topic,
          "payload": "mapped:" & payloadText,
          "qos": 1,
          "retain": false
        }
        """;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(_ =>
            {
                session = new FakeMqttSession();
                return session;
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["broker2"] = new NodeDefinition
                {
                    Type = PipelineFlowNodeTypes.Connection,
                    Configuration =
                    {
                        ["profile"] = JsonDocument.Parse("""{"name":"broker-2","host":"localhost","port":1884}""").RootElement.Clone()
                    }
                }
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["traffic"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.GeneratedSource,
                            Configuration =
                            {
                                ["messages"] = JsonDocument.Parse("""
                                [
                                  {"topic":"factory/a","payload":"drop","qos":0},
                                  {"topic":"factory/b","payload":"keep","qos":1}
                                ]
                                """).RootElement.Clone()
                            }
                        },
                        ["filter"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.MessageFilter,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"traffic.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["expression"] = JsonDocument.Parse("\"qos >= 1\"").RootElement.Clone()
                            }
                        },
                        ["map"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.DynamicMapper,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"filter.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"jsonata\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["outputType"] = JsonDocument.Parse("\"MqttPublishRequest\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse(JsonSerializer.Serialize(mapperExpression)).RootElement.Clone()
                            }
                        },
                        ["publisher"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.MqttPublisher,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"map.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"broker2\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        session.ShouldNotBeNull();
        session!.CompleteMessages();
        await runtime.Completion;

        var publish = session.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("mirror/factory/b");
        publish.Payload.ShouldBe("mapped:keep"u8.ToArray());
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        publish.Retain.ShouldBeFalse();
    }

    [Fact]
    public async Task DynamicGenericMapper_CanWriteMappedEnvelopePayloadsToFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fluxmq-runtime-tests", Guid.NewGuid().ToString("N"))
            .Replace('\\', '/');
        var expression = $$"""
        new FileWriteRequest {
          Path = "{{directory}}/" + topic.Replace("/", "_") + ".txt",
          Content = Encoding.UTF8.GetBytes("topic=" + topic + ";payload=" + payloadText),
          Mode = FileWriteMode.Overwrite,
          CreateDirectory = true
        }
        """;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["traffic"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.GeneratedSource,
                            Configuration =
                            {
                                ["messages"] = JsonDocument.Parse("""
                                [
                                  {"topic":"factory/a","payload":"alpha"},
                                  {"topic":"factory/b","payload":"beta"}
                                ]
                                """).RootElement.Clone()
                            }
                        },
                        ["map"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.DynamicMapper,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"traffic.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"dynamic-expresso\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["outputType"] = JsonDocument.Parse("\"FileWriteRequest\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse(JsonSerializer.Serialize(expression)).RootElement.Clone()
                            }
                        },
                        ["writer"] = NodeWithPort(PipelineFlowNodeTypes.FileWriter, "Input", "\"map.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion;

        File.ReadAllText(Path.Combine(directory, "factory_a.txt")).ShouldBe("topic=factory/a;payload=alpha");
        File.ReadAllText(Path.Combine(directory, "factory_b.txt")).ShouldBe("topic=factory/b;payload=beta");
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task StoredSessionSourceFactory_CreatesStoredSessionSource()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository(
            Stored(sessionId, "factory/one", DateTimeOffset.Parse("2026-05-15T10:00:00Z"), 1),
            Stored(sessionId, "factory/two", DateTimeOffset.Parse("2026-05-15T10:00:01Z"), 2));
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(messageRepository: repository)
            .Register(new NodeType("test.snapshot-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttMetricsSnapshot>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["stored"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.StoredSessionSource,
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse($"\"{sessionId}\"").RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MqttMetrics, "Input", "\"stored.Output\""),
                        ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion;

        sink!.Values.ShouldNotBeEmpty();
        sink.Values[^1].MessageCount.ShouldBe(2);
        repository.StreamedSessionIds.ShouldBe(new[] { sessionId });
    }

    [Fact]
    public async Task ReplaySourceFactory_CreatesReplaySource()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository(
            Stored(sessionId, "factory/one", DateTimeOffset.Parse("2026-05-15T10:00:01Z"), 2),
            Stored(sessionId, "factory/two", DateTimeOffset.Parse("2026-05-15T10:00:00Z"), 1));
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(messageRepository: repository)
            .Register(new NodeType("test.snapshot-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttMetricsSnapshot>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["replay"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.ReplaySource,
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse($"\"{sessionId}\"").RootElement.Clone(),
                                ["speed"] = JsonDocument.Parse("1000").RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MqttMetrics, "Input", "\"replay.Output\""),
                        ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion;

        sink!.Values.ShouldNotBeEmpty();
        sink.Values[^1].MessageCount.ShouldBe(2);
        repository.LoadedSessionIds.ShouldBe(new[] { sessionId });
    }

    [Fact]
    public void ReplaySourceFactory_ReturnsBuildErrorWhenReplaySourceHasNoRepository()
    {
        var sessionId = SessionId.New();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["replay"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.ReplaySource,
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse($"\"{sessionId}\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Message.Contains("message repository"));
    }

    [Fact]
    public void StoredSessionSourceFactory_ReturnsBuildErrorWhenStoredSourceHasNoRepository()
    {
        var sessionId = SessionId.New();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["stored"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.StoredSessionSource,
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse($"\"{sessionId}\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Message.Contains("message repository"));
    }

    [Fact]
    public void TriggerFactory_ReturnsBuildErrorWhenConnectionMissing()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["trigger"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.Trigger,
                            Configuration =
                            {
                                ["subscriptions"] = JsonDocument.Parse("""["#"]""").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Message.Contains("connection"));
    }

    [Fact]
    public void RegisteredFactory_ReturnsBuildErrorForInvalidBoundedCapacity()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["inspect"] = new NodeDefinition
                        {
                            Type = PipelineFlowNodeTypes.PayloadInspector,
                            Configuration =
                            {
                                ["boundedCapacity"] = JsonDocument.Parse("0").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error =>
            error.Code == ApplicationRuntimeBuildErrorCode.FactoryFailed &&
            error.Message.Contains("boundedCapacity"));
    }

    private static RuntimeNode SourceNode(NodeAddress address, TestSourceNode node)
        => RuntimeNode.Create(
            address,
            node,
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(new PortName("Output")), node.Output)
            ]);

    private static RuntimeNode SinkNode<T>(NodeAddress address, TestSinkNode<T> node)
        => RuntimeNode.Create(
            address,
            node,
            inputs:
            [
                new InputPort<T>(address.Port(new PortName("Input")), node.Input)
            ]);

    private static NodeDefinition Node(string type) => new()
    {
        Type = new NodeType(type)
    };

    private static NodeDefinition NodeWithPort(NodeType type, string portName, string linkJson) => new()
    {
        Type = type,
        Ports =
        {
            [portName] = JsonDocument.Parse(linkJson).RootElement.Clone()
        }
    };

    private static NodeDefinition NodeWithPort(string type, string portName, string linkJson)
        => NodeWithPort(new NodeType(type), portName, linkJson);

    private static StoredMessage Stored(SessionId sessionId, string topic, DateTimeOffset receivedAt, long sequence) => new()
    {
        SessionId = sessionId,
        Sequence = sequence,
        Topic = topic,
        Payload = [1, 2, 3],
        ReceivedAt = receivedAt
    };

    private sealed class TestSourceNode : IFlowNode
    {
        private readonly BufferBlock<MqttEnvelope> _output = new();
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<MqttEnvelope> Output => _output;
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _output.Completion;

        public void Post(MqttEnvelope value) => _output.Post(value);

        public void Complete()
        {
            _output.Complete();
            _errors.Complete();
        }

        public void Fault(Exception exception)
        {
            ((IDataflowBlock)_output).Fault(exception);
            _errors.Complete();
        }
    }

    private sealed class TestSinkNode<T> : IFlowNode
    {
        private readonly ActionBlock<T> _input;
        private readonly BufferBlock<FlowError> _errors = new();
        private readonly List<T> _values = [];

        public TestSinkNode()
        {
            _input = new ActionBlock<T>(value => _values.Add(value));
        }

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ITargetBlock<T> Input => _input;
        public ISourceBlock<FlowError> Errors => _errors;
        public IReadOnlyList<T> Values => _values;
        public Task Completion => _input.Completion;

        public void Complete()
        {
            _input.Complete();
            _errors.Complete();
        }

        public void Fault(Exception exception)
        {
            ((IDataflowBlock)_input).Fault(exception);
            _errors.Complete();
        }
    }

    private sealed class FakeMqttSession : IMqttSession
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
        private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];

        public MqttConnectionProfile Profile { get; } = new() { Name = "factory-test" };
        public MqttSessionState State { get; private set; } = MqttSessionState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCalls { get; private set; }
        public List<PublishedMessage> Published { get; } = [];
        public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;

        public event EventHandler<MqttSessionState>? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            State = MqttSessionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttSessionState.Disconnected;
            _messages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
        {
            _subscriptions.Add((topicFilter, qos));
            return Task.CompletedTask;
        }

        public Task UnsubscribeAsync(string topicFilter, CancellationToken ct = default) => Task.CompletedTask;

        public Task PublishAsync(string topic, byte[] payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, bool retain = false, CancellationToken ct = default)
        {
            Published.Add(new PublishedMessage(topic, payload, qos, retain));
            return Task.CompletedTask;
        }

        public Task WriteAsync(MqttEnvelope message) => _messages.Writer.WriteAsync(message).AsTask();

        public void CompleteMessages() => _messages.Writer.TryComplete();

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public sealed record PublishedMessage(
            string Topic,
            byte[] Payload,
            MqttQualityOfServiceLevel QualityOfService,
            bool Retain);
    }

    private sealed class FakeMessageRepository(params StoredMessage[] messages) : IMessageRepository
    {
        private readonly IReadOnlyList<StoredMessage> _messages = messages;

        public List<SessionId> LoadedSessionIds { get; } = [];
        public List<SessionId> StreamedSessionIds { get; } = [];

        public void Add(SessionId sessionId, MqttEnvelope envelope)
            => throw new NotSupportedException();

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
            => throw new NotSupportedException();

        public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId)
        {
            LoadedSessionIds.Add(sessionId);
            return GetBySessionCore(sessionId);
        }

        private IReadOnlyList<StoredMessage> GetBySessionCore(SessionId sessionId)
            => _messages
                .Where(message => message.SessionId == sessionId)
                .OrderBy(message => message.ReceivedAt)
                .ThenBy(message => message.Sequence)
                .ToArray();

        public IReadOnlyList<StoredMessage> GetByTopic(string topic)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<StoredMessage> ReadBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamedSessionIds.Add(sessionId);
            foreach (var message in GetBySessionCore(sessionId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<MqttEnvelope> ReadEnvelopesBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var message in ReadBySessionAsync(sessionId, cancellationToken))
            {
                yield return message.ToEnvelope();
            }
        }

        public long CountBySession(SessionId sessionId)
            => _messages.Count(message => message.SessionId == sessionId);
    }
}
