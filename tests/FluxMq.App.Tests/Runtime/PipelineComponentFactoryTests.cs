using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Mqtt;
using FluxMq.App;
using FluxMq.Components.Assertions;
using FluxMq.Components.JsonSchema;
using FluxMq.Components.Logging;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxFlow.Components.Http.Contracts;
using FluxFlow.Components.Payloads.Contracts;
using FluxFlow.Components.Routing;
using FluxFlow.Components.Routing.Contracts;
using FluxFlow.Components.State.Contracts;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using FluxFlow.Components.Serialization.Contracts;
using FluxFlow.Components.Storage;
using FluxFlow.Components.Storage.Contracts;
using FluxFlow.Components.Timers.Contracts;
using MQTTnet.Protocol;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using FlowPayloadInspectionRequest = FluxFlow.Components.Payloads.Contracts.PayloadInspectionRequest;
using FlowPayloadInspectionResult = FluxFlow.Components.Payloads.Contracts.PayloadInspectionResult;
using FlowPayloadKind = FluxFlow.Components.Payloads.Contracts.PayloadKind;

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
            FluxMqNodeTypes.Connection,
            FluxMqNodeTypes.Trigger,
            FluxMqNodeTypes.ConnectionStateTrigger,
            FluxMqNodeTypes.StoredSessionSource,
            FluxMqNodeTypes.ReplaySource,
            FluxMqNodeTypes.GeneratedSource,
            FluxMqNodeTypes.PayloadInspect,
            FluxMqNodeTypes.PayloadInspector,
            FluxMqNodeTypes.HttpRequest,
            FluxMqNodeTypes.MqttMetrics,
            FluxMqNodeTypes.FlowLogger,
            FluxMqNodeTypes.MessageFilter,
            FluxMqNodeTypes.ConditionRouter,
            FluxMqNodeTypes.FlowAssertion,
            FluxMqNodeTypes.JsonSchemaValidator,
            FluxMqNodeTypes.DynamicMapper,
            FluxMqNodeTypes.StateReducer,
            RoutingComponentTypes.Switch,
            RoutingComponentTypes.Correlation,
            RoutingComponentTypes.Window,
            RoutingComponentTypes.Join,
            RoutingComponentTypes.Fork,
            RoutingComponentTypes.Merge,
            new NodeType("json.parse"),
            new NodeType("json.stringify"),
            new NodeType("text.encode"),
            new NodeType("text.decode"),
            new NodeType("base64.encode"),
            new NodeType("base64.decode"),
            FluxMqNodeTypes.MqttPublisher,
            FluxMqNodeTypes.MqttRecorder,
            FluxMqNodeTypes.FileWriter,
            StorageComponentTypes.Put,
            StorageComponentTypes.Get,
            StorageComponentTypes.Delete,
            StorageComponentTypes.Query,
            FluxMqNodeTypes.TimerInterval,
            FluxMqNodeTypes.TimerSchedule,
            FluxMqNodeTypes.TimerDelay,
            FluxMqNodeTypes.TimerDebounce,
            FluxMqNodeTypes.TimerThrottle
        }, ignoreOrder: true);
    }

    [Fact]
    public async Task PayloadInspectFactory_CreatesLinkableRuntimeNode()
    {
        TestValueSourceNode<FlowPayloadInspectionRequest>? source = null;
        TestSinkNode<FlowPayloadInspectionResult>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.payload-source"), (address, _) =>
            {
                source = new TestValueSourceNode<FlowPayloadInspectionRequest>();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.payload-sink"), (address, _) =>
            {
                sink = new TestSinkNode<FlowPayloadInspectionResult>();
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
                        ["source"] = Node("test.payload-source"),
                        ["inspect"] = NodeWithPort(FluxMqNodeTypes.PayloadInspect, "Input", "\"source.Output\""),
                        ["sink"] = NodeWithPort("test.payload-sink", "Input", "\"inspect.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new FlowPayloadInspectionRequest
        {
            Text = """{"value":1}""",
            ContentType = "application/json"
        });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sink!.Values.ShouldHaveSingleItem();
        sink.Values[0].Kind.ShouldBe(FlowPayloadKind.JsonObject);
        sink.Values[0].TextPreview.ShouldBe("""{"value":1}""");
    }

    [Fact]
    public async Task HttpRequestFactory_CreatesLinkableRuntimeNode()
    {
        TestValueSourceNode<HttpRequestInput>? source = null;
        TestSinkNode<HttpErrorOutput>? errors = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.http-source"), (address, _) =>
            {
                source = new TestValueSourceNode<HttpRequestInput>();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.http-errors"), (address, _) =>
            {
                errors = new TestSinkNode<HttpErrorOutput>();
                return SinkNode(address, errors);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.http-source"),
                        ["request"] = NodeWithPort(FluxMqNodeTypes.HttpRequest, "Input", "\"source.Output\""),
                        ["errors"] = NodeWithPort("test.http-errors", "Input", "\"request.Errors\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new HttpRequestInput { Method = "GET", Url = "::not-a-url::" });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        errors!.Values.ShouldHaveSingleItem();
        errors.Values[0].Kind.ShouldBe(HttpErrorKind.InvalidUrl);
    }

    [Fact]
    public async Task DynamicMapperFactory_CanCreateHttpRequestInput()
    {
        TestSourceNode? source = null;
        TestSinkNode<HttpRequestInput>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.http-input-sink"), (address, _) =>
            {
                sink = new TestSinkNode<HttpRequestInput>();
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
                        ["mapper"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.DynamicMapper,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"jsonata\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["outputType"] = JsonDocument.Parse("\"HttpRequestInput\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse("""
                                    "{ \"method\": \"POST\", \"url\": \"https://example.test/messages\", \"body\": payloadText, \"contentType\": \"application/json\" }"
                                    """).RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.http-input-sink", "Input", "\"mapper.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new MqttEnvelope { Topic = "factory/json", Payload = """{"value":1}"""u8.ToArray() });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sink!.Values.ShouldHaveSingleItem();
        sink.Values[0].Method.ShouldBe("POST");
        sink.Values[0].Url.ShouldBe("https://example.test/messages");
        sink.Values[0].Body.ShouldBe("""{"value":1}""");
    }

    [Fact]
    public async Task SerializationFactory_CreatesLinkableRuntimeNode()
    {
        TestValueSourceNode<JsonStringifyRequest>? source = null;
        TestSinkNode<JsonStringifyResult>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.stringify-source"), (address, _) =>
            {
                source = new TestValueSourceNode<JsonStringifyRequest>();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.stringify-sink"), (address, _) =>
            {
                sink = new TestSinkNode<JsonStringifyResult>();
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
                        ["source"] = Node("test.stringify-source"),
                        ["stringify"] = NodeWithPort("json.stringify", "Input", "\"source.Output\""),
                        ["sink"] = NodeWithPort("test.stringify-sink", "Input", "\"stringify.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new JsonStringifyRequest
        {
            Value = new Dictionary<string, object?>
            {
                ["value"] = 12
            }
        });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var output = sink!.Values.ShouldHaveSingleItem();
        output.Text.ShouldBe("""{"value":12}""");
        output.ByteCount.ShouldBe(12);
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
                        ["inspect"] = NodeWithPort(FluxMqNodeTypes.PayloadInspector, "Input", "\"source.Output\""),
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
    public async Task PayloadInspectorFactory_CompletesWhenOutputIsUnlinked()
    {
        TestSourceNode? source = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
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
                        ["inspect"] = NodeWithPort(FluxMqNodeTypes.PayloadInspector, "Input", "\"source.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/json", Payload = """{"value":1}"""u8.ToArray() });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ApplicationRuntimeBuilder_RoutesConditionalLinks()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttEnvelope>? factorySink = null;
        TestSinkNode<MqttEnvelope>? alertSink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.factory-sink"), (address, _) =>
            {
                factorySink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, factorySink);
            })
            .Register(new NodeType("test.alert-sink"), (address, _) =>
            {
                alertSink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, alertSink);
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
                        ["factorySink"] = NodeWithPort(
                            "test.factory-sink",
                            "Input",
                            """{ "from": "source.Output", "when": "input.Topic.StartsWith(\"factory/\")" }"""),
                        ["alertSink"] = NodeWithPort(
                            "test.alert-sink",
                            "Input",
                            """{ "from": "source.Output", "when": "input.Topic.StartsWith(\"alerts/\")" }""")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(result.Errors.FirstOrDefault()?.Message);

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = [1] });
        source.Post(new MqttEnvelope { Topic = "alerts/one", Payload = [2] });
        source.Post(new MqttEnvelope { Topic = "other/one", Payload = [3] });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        factorySink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/one"]);
        alertSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["alerts/one"]);
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
                        ["metrics"] = NodeWithPort(FluxMqNodeTypes.MqttMetrics, "Input", "\"source.Output\""),
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
                            Type = FluxMqNodeTypes.FlowLogger,
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
    public async Task ConditionRouterFactory_RoutesEnvelopesToTrueAndFalsePorts()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttEnvelope>? trueSink = null;
        TestSinkNode<MqttEnvelope>? falseSink = null;
        TestSinkNode<FlowLogEntry>? logSink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                if (address.Node.Value == "trueSink")
                {
                    trueSink = new TestSinkNode<MqttEnvelope>();
                    return SinkNode(address, trueSink);
                }

                falseSink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, falseSink);
            })
            .Register(new NodeType("test.log-sink"), (address, _) =>
            {
                logSink = new TestSinkNode<FlowLogEntry>();
                return SinkNode(address, logSink);
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
                        ["router"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.ConditionRouter,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["expression"] = JsonDocument.Parse("\"qos >= 1\"").RootElement.Clone()
                            }
                        },
                        ["trueSink"] = NodeWithPort("test.envelope-sink", "Input", "\"router.WhenTrue\""),
                        ["falseSink"] = NodeWithPort("test.envelope-sink", "Input", "\"router.WhenFalse\""),
                        ["logSink"] = NodeWithPort("test.log-sink", "Input", "\"router.Entries\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/qos0", Payload = [], QualityOfService = MqttQualityOfServiceLevel.AtMostOnce });
        source.Post(new MqttEnvelope { Topic = "factory/qos1", Payload = [], QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        trueSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/qos1"]);
        falseSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/qos0"]);
        logSink!.Values.Select(entry => entry.Message).ShouldBe([
            "Routed input to WhenFalse.",
            "Routed input to WhenTrue."
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task RoutingSwitchFactory_RoutesWithFluxMqExpressionContext()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttEnvelope>? trueSink = null;
        TestSinkNode<MqttEnvelope>? falseSink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                if (address.Node.Value == "trueSink")
                {
                    trueSink = new TestSinkNode<MqttEnvelope>();
                    return SinkNode(address, trueSink);
                }

                falseSink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, falseSink);
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
                        ["router"] = new NodeDefinition
                        {
                            Type = RoutingComponentTypes.Switch,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse("\"qos >= 1\"").RootElement.Clone(),
                                ["routes"] = JsonDocument.Parse("""["True","False"]""").RootElement.Clone(),
                                ["routeOutputs"] = JsonDocument.Parse("""{"True":"WhenTrue","False":"WhenFalse"}""").RootElement.Clone()
                            }
                        },
                        ["trueSink"] = NodeWithPort("test.envelope-sink", "Input", "\"router.WhenTrue\""),
                        ["falseSink"] = NodeWithPort("test.envelope-sink", "Input", "\"router.WhenFalse\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new MqttEnvelope { Topic = "factory/qos0", Payload = [], QualityOfService = MqttQualityOfServiceLevel.AtMostOnce });
        source.Post(new MqttEnvelope { Topic = "factory/qos1", Payload = [], QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        trueSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/qos1"]);
        falseSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/qos0"]);
    }

    [Fact]
    public async Task RoutingCorrelationFactory_MatchesRequestAndResponse()
    {
        TestSourceNode? source = null;
        TestSinkNode<FlowCorrelationMatch<MqttEnvelope>>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.correlation-sink"), (address, _) =>
            {
                sink = new TestSinkNode<FlowCorrelationMatch<MqttEnvelope>>();
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
                        ["correlation"] = new NodeDefinition
                        {
                            Type = RoutingComponentTypes.Correlation,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["keyExpression"] = JsonDocument.Parse("\"topic\"").RootElement.Clone(),
                                ["sideExpression"] = JsonDocument.Parse("\"payloadText\"").RootElement.Clone(),
                                ["requestSide"] = JsonDocument.Parse("\"request\"").RootElement.Clone(),
                                ["responseSide"] = JsonDocument.Parse("\"response\"").RootElement.Clone(),
                                ["timeoutMilliseconds"] = JsonDocument.Parse("30000").RootElement.Clone(),
                                ["maxPending"] = JsonDocument.Parse("8").RootElement.Clone(),
                                ["boundedCapacity"] = JsonDocument.Parse("64").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.correlation-sink", "Input", "\"correlation.Matched\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new MqttEnvelope { Topic = "factory/42", Payload = Encoding.UTF8.GetBytes("request") });
        source.Post(new MqttEnvelope { Topic = "factory/42", Payload = Encoding.UTF8.GetBytes("response") });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var match = sink!.Values.ShouldHaveSingleItem();
        match.Key.ShouldBe("factory/42");
        Encoding.UTF8.GetString(match.Request.Payload).ShouldBe("request");
        Encoding.UTF8.GetString(match.Response.Payload).ShouldBe("response");
    }

    [Fact]
    public async Task RoutingJoinFactory_JoinsLeftAndRightByKey()
    {
        TestSourceNode? leftSource = null;
        TestSourceNode? rightSource = null;
        TestSinkNode<FlowJoinResult<MqttEnvelope, MqttEnvelope>>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                if (address.Node.Value == "leftSource")
                {
                    leftSource = new TestSourceNode();
                    return SourceNode(address, leftSource);
                }

                rightSource = new TestSourceNode();
                return SourceNode(address, rightSource);
            })
            .Register(new NodeType("test.join-sink"), (address, _) =>
            {
                sink = new TestSinkNode<FlowJoinResult<MqttEnvelope, MqttEnvelope>>();
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
                        ["leftSource"] = Node("test.source"),
                        ["rightSource"] = Node("test.source"),
                        ["join"] = new NodeDefinition
                        {
                            Type = RoutingComponentTypes.Join,
                            Ports =
                            {
                                ["Left"] = JsonDocument.Parse("\"leftSource.Output\"").RootElement.Clone(),
                                ["Right"] = JsonDocument.Parse("\"rightSource.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["leftInputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["rightInputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["leftKeyExpression"] = JsonDocument.Parse("\"topic\"").RootElement.Clone(),
                                ["rightKeyExpression"] = JsonDocument.Parse("\"topic\"").RootElement.Clone(),
                                ["timeoutMilliseconds"] = JsonDocument.Parse("30000").RootElement.Clone(),
                                ["maxPending"] = JsonDocument.Parse("8").RootElement.Clone(),
                                ["boundedCapacity"] = JsonDocument.Parse("64").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.join-sink", "Input", "\"join.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        leftSource!.Post(new MqttEnvelope { Topic = "factory/42", Payload = Encoding.UTF8.GetBytes("left") });
        rightSource!.Post(new MqttEnvelope { Topic = "factory/42", Payload = Encoding.UTF8.GetBytes("right") });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var joined = sink!.Values.ShouldHaveSingleItem();
        joined.Key.ShouldBe("factory/42");
        Encoding.UTF8.GetString(joined.Left.Payload).ShouldBe("left");
        Encoding.UTF8.GetString(joined.Right.Payload).ShouldBe("right");
    }

    [Fact]
    public async Task RoutingWindowFactory_EmitsCountWindows()
    {
        TestSourceNode? source = null;
        TestSinkNode<FlowWindow<MqttEnvelope>>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.window-sink"), (address, _) =>
            {
                sink = new TestSinkNode<FlowWindow<MqttEnvelope>>();
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
                        ["window"] = new NodeDefinition
                        {
                            Type = RoutingComponentTypes.Window,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["maxItems"] = JsonDocument.Parse("2").RootElement.Clone(),
                                ["timeMilliseconds"] = JsonDocument.Parse("0").RootElement.Clone(),
                                ["emitPartialOnCompletion"] = JsonDocument.Parse("false").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.window-sink", "Input", "\"window.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = [] });
        source.Post(new MqttEnvelope { Topic = "factory/two", Payload = [] });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var window = sink!.Values.ShouldHaveSingleItem();
        window.Sequence.ShouldBe(1);
        window.Count.ShouldBe(2);
        window.Reason.ShouldBe(FlowWindowEmitReason.Count);
        window.Items.Select(envelope => envelope.Topic).ShouldBe(["factory/one", "factory/two"]);
    }

    [Fact]
    public async Task RoutingForkFactory_CopiesInputsToConfiguredOutputs()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttEnvelope>? auditSink = null;
        TestSinkNode<MqttEnvelope>? dashboardSink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                if (address.Node.Value == "auditSink")
                {
                    auditSink = new TestSinkNode<MqttEnvelope>();
                    return SinkNode(address, auditSink);
                }

                dashboardSink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, dashboardSink);
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
                        ["fork"] = new NodeDefinition
                        {
                            Type = RoutingComponentTypes.Fork,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["outputs"] = JsonDocument.Parse("""["Audit","Dashboard"]""").RootElement.Clone()
                            }
                        },
                        ["auditSink"] = NodeWithPort("test.envelope-sink", "Input", "\"fork.Audit\""),
                        ["dashboardSink"] = NodeWithPort("test.envelope-sink", "Input", "\"fork.Dashboard\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = [] });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        auditSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/one"]);
        dashboardSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/one"]);
    }

    [Fact]
    public async Task RoutingMergeFactory_CombinesConfiguredInputsWithSourcePort()
    {
        TestSourceNode? leftSource = null;
        TestSourceNode? rightSource = null;
        TestSinkNode<FlowMergeItem<MqttEnvelope>>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                if (address.Node.Value == "leftSource")
                {
                    leftSource = new TestSourceNode();
                    return SourceNode(address, leftSource);
                }

                rightSource = new TestSourceNode();
                return SourceNode(address, rightSource);
            })
            .Register(new NodeType("test.merge-sink"), (address, _) =>
            {
                sink = new TestSinkNode<FlowMergeItem<MqttEnvelope>>();
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
                        ["leftSource"] = Node("test.source"),
                        ["rightSource"] = Node("test.source"),
                        ["merge"] = new NodeDefinition
                        {
                            Type = RoutingComponentTypes.Merge,
                            Ports =
                            {
                                ["Left"] = JsonDocument.Parse("\"leftSource.Output\"").RootElement.Clone(),
                                ["Right"] = JsonDocument.Parse("\"rightSource.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["inputs"] = JsonDocument.Parse("""["Left","Right"]""").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.merge-sink", "Input", "\"merge.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        leftSource!.Post(new MqttEnvelope { Topic = "factory/left", Payload = [] });
        rightSource!.Post(new MqttEnvelope { Topic = "factory/right", Payload = [] });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Select(item => (item.Source, item.Value.Topic))
            .ShouldBe([("Left", "factory/left"), ("Right", "factory/right")], ignoreOrder: true);
    }

    [Fact]
    public async Task FlowAssertionFactory_RoutesResultsAndWritesEntries()
    {
        TestSourceNode? source = null;
        TestSinkNode<FlowAssertionResult>? resultSink = null;
        TestSinkNode<MqttEnvelope>? passedSink = null;
        TestSinkNode<MqttEnvelope>? failedSink = null;
        TestSinkNode<FlowLogEntry>? logSink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.assertion-sink"), (address, _) =>
            {
                resultSink = new TestSinkNode<FlowAssertionResult>();
                return SinkNode(address, resultSink);
            })
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                if (address.Node.Value == "passedSink")
                {
                    passedSink = new TestSinkNode<MqttEnvelope>();
                    return SinkNode(address, passedSink);
                }

                failedSink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, failedSink);
            })
            .Register(new NodeType("test.log-sink"), (address, _) =>
            {
                logSink = new TestSinkNode<FlowLogEntry>();
                return SinkNode(address, logSink);
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
                        ["assertion"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.FlowAssertion,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["assertionName"] = JsonDocument.Parse("\"QoS at least once\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse("\"qos >= 1\"").RootElement.Clone(),
                                ["failureMessage"] = JsonDocument.Parse("\"Expected QoS to be at least 1.\"").RootElement.Clone()
                            }
                        },
                        ["resultSink"] = NodeWithPort("test.assertion-sink", "Input", "\"assertion.Result\""),
                        ["passedSink"] = NodeWithPort("test.envelope-sink", "Input", "\"assertion.Passed\""),
                        ["failedSink"] = NodeWithPort("test.envelope-sink", "Input", "\"assertion.Failed\""),
                        ["logSink"] = NodeWithPort("test.log-sink", "Input", "\"assertion.Entries\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/qos0", Payload = [], QualityOfService = MqttQualityOfServiceLevel.AtMostOnce });
        source.Post(new MqttEnvelope { Topic = "factory/qos1", Payload = [], QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        resultSink!.Values.Select(value => value.Passed).ShouldBe([false, true]);
        passedSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/qos1"]);
        failedSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/qos0"]);
        logSink!.Values.Select(entry => entry.Message).ShouldBe([
            "Assertion failed: QoS at least once.",
            "Assertion passed: QoS at least once."
        ]);
    }

    [Fact]
    public async Task FlowAssertionFactory_SupportsStateReducerResults()
    {
        TestValueSourceNode<StateReducerResult>? source = null;
        TestSinkNode<FlowAssertionResult>? resultSink = null;
        TestSinkNode<StateReducerResult>? passedSink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.state-source"), (address, _) =>
            {
                source = new TestValueSourceNode<StateReducerResult>();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.assertion-sink"), (address, _) =>
            {
                resultSink = new TestSinkNode<FlowAssertionResult>();
                return SinkNode(address, resultSink);
            })
            .Register(new NodeType("test.state-sink"), (address, _) =>
            {
                passedSink = new TestSinkNode<StateReducerResult>();
                return SinkNode(address, passedSink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.state-source"),
                        ["assertion"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.FlowAssertion,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["assertionName"] = JsonDocument.Parse("\"State updated\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"StateReducerResult\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse("\"version >= 1 && key == \\\"temperature\\\"\"").RootElement.Clone(),
                                ["failureMessage"] = JsonDocument.Parse("\"State reducer result was not updated.\"").RootElement.Clone()
                            }
                        },
                        ["resultSink"] = NodeWithPort("test.assertion-sink", "Input", "\"assertion.Result\""),
                        ["passedSink"] = NodeWithPort("test.state-sink", "Input", "\"assertion.Passed\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));

        source!.Post(new StateReducerResult
        {
            Key = "temperature",
            NewState = 21.7,
            Version = 1,
            UpdatedAt = DateTimeOffset.Parse("2026-06-01T10:00:00Z")
        });
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        resultSink!.Values.ShouldHaveSingleItem().Passed.ShouldBeTrue();
        passedSink!.Values.ShouldHaveSingleItem().Key.ShouldBe("temperature");
    }

    [Fact]
    public async Task StorageComponents_CanStoreAndReadLocalRecords()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "fluxmq-storage-tests", Guid.NewGuid().ToString("N"));
        TestValueSourceNode<StoragePutRequest>? source = null;
        TestSinkNode<StorageResult>? putSink = null;
        TestSinkNode<StorageResult>? foundSink = null;

        try
        {
            var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
                .RegisterPipelineComponentFactories(fileSystemStorageRootDirectory: storageRoot)
                .Register(new NodeType("test.storage-put-source"), (address, _) =>
                {
                    source = new TestValueSourceNode<StoragePutRequest>();
                    return SourceNode(address, source);
                })
                .Register(new NodeType("test.storage-get-request"), (address, _) =>
                {
                    var mapper = new StorageGetRequestNode();
                    return RuntimeNode.Create(
                        address,
                        mapper,
                        inputs:
                        [
                            new InputPort<StorageResult>(address.Port(new("Input")), mapper.Input)
                        ],
                        outputs:
                        [
                            new OutputPort<StorageGetRequest>(address.Port(new("Output")), mapper.Output)
                        ]);
                })
                .Register(new NodeType("test.storage-result-sink"), (address, _) =>
                {
                    var sink = new TestSinkNode<StorageResult>();
                    if (address.Node.Value == "putSink")
                    {
                        putSink = sink;
                    }
                    else
                    {
                        foundSink = sink;
                    }

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
                            ["source"] = Node("test.storage-put-source"),
                            ["put"] = new NodeDefinition
                            {
                                Type = StorageComponentTypes.Put,
                                Ports =
                                {
                                    ["Input"] = JsonDocument.Parse("\"source.Output\"").RootElement.Clone()
                                },
                                Configuration =
                                {
                                    ["store"] = JsonDocument.Parse("\"local\"").RootElement.Clone(),
                                    ["collection"] = JsonDocument.Parse("\"samples\"").RootElement.Clone(),
                                    ["emitStoredRecord"] = JsonSerializer.SerializeToElement(true)
                                }
                            },
                            ["getRequest"] = NodeWithPort("test.storage-get-request", "Input", "\"put.Result\""),
                            ["get"] = new NodeDefinition
                            {
                                Type = StorageComponentTypes.Get,
                                Ports =
                                {
                                    ["Input"] = JsonDocument.Parse("\"getRequest.Output\"").RootElement.Clone()
                                },
                                Configuration =
                                {
                                    ["store"] = JsonDocument.Parse("\"local\"").RootElement.Clone(),
                                    ["collection"] = JsonDocument.Parse("\"samples\"").RootElement.Clone()
                                }
                            },
                            ["putSink"] = NodeWithPort("test.storage-result-sink", "Input", "\"put.Result\""),
                            ["foundSink"] = NodeWithPort("test.storage-result-sink", "Input", "\"get.Found\"")
                        }
                    }
                }
            });

            result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
            await result.Runtime!.StartAsync();

            source!.Post(new StoragePutRequest
            {
                Collection = "samples",
                Key = "message-1",
                Value = new Dictionary<string, object?>
                {
                    ["topic"] = "factory/one",
                    ["value"] = 12
                },
                ContentType = "application/json"
            });
            result.Runtime.Complete();

            await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

            putSink!.Values.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            var found = foundSink!.Values.ShouldHaveSingleItem();
            found.Found.ShouldBeTrue();
            found.Record.ShouldNotBeNull();
            found.Record!.Key.ShouldBe("message-1");
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
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
                            Type = FluxMqNodeTypes.JsonSchemaValidator,
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
                        ["sink"] = NodeWithPort("test.validation-sink", "Input", "\"validator.Result\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await result.Runtime!.StartAsync();

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = """{"status":"ok"}"""u8.ToArray() });
        source.Post(new MqttEnvelope { Topic = "factory/two", Payload = """{"status":"fault"}"""u8.ToArray() });
        result.Runtime.Complete();

        await result.Runtime.Completion;

        sink!.Values.Count.ShouldBe(2);
        sink.Values[0].IsValid.ShouldBeTrue();
        sink.Values[1].IsValid.ShouldBeFalse();
        sink.Values[1].SchemaId.ShouldBe("status-schema");
    }

    [Fact]
    public async Task JsonSchemaValidatorFactory_RoutesValidAndInvalidEnvelopes()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttEnvelope>? validSink = null;
        TestSinkNode<MqttEnvelope>? invalidSink = null;
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
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                if (address.Node.Value == "validSink")
                {
                    validSink = new TestSinkNode<MqttEnvelope>();
                    return SinkNode(address, validSink);
                }

                invalidSink = new TestSinkNode<MqttEnvelope>();
                return SinkNode(address, invalidSink);
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
                            Type = FluxMqNodeTypes.JsonSchemaValidator,
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
                        ["validSink"] = NodeWithPort("test.envelope-sink", "Input", "\"validator.Valid\""),
                        ["invalidSink"] = NodeWithPort("test.envelope-sink", "Input", "\"validator.Invalid\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await result.Runtime!.StartAsync();

        source!.Post(new MqttEnvelope { Topic = "factory/valid", Payload = """{"status":"ok"}"""u8.ToArray() });
        source.Post(new MqttEnvelope { Topic = "factory/invalid", Payload = """{"status":"fault"}"""u8.ToArray() });
        result.Runtime.Complete();

        await result.Runtime.Completion;

        validSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/valid"]);
        invalidSink!.Values.Select(envelope => envelope.Topic).ShouldBe(["factory/invalid"]);
    }

    [Fact]
    public async Task ConnectionAndTriggerFactories_StartClientAndFeedWorkflowNodes()
    {
        FakeMqttBrokerClient? mqttClient = null;
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(_ =>
            {
                mqttClient = new FakeMqttBrokerClient();
                return mqttClient;
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
                    Type = FluxMqNodeTypes.Connection,
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
                            Type = FluxMqNodeTypes.Trigger,
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"broker\"").RootElement.Clone(),
                                ["subscriptions"] = JsonDocument.Parse("""["factory/#"]""").RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(FluxMqNodeTypes.MqttMetrics, "Input", "\"trigger.Output\""),
                        ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        using var runtime = result.Runtime!;

        await runtime.StartAsync();

        mqttClient.ShouldNotBeNull();
        mqttClient!.ConnectCalls.ShouldBe(1);
        mqttClient.Subscriptions.ShouldContain(subscription =>
            subscription.TopicFilter == "factory/#" &&
            subscription.QualityOfService == MqttQualityOfServiceLevel.AtLeastOnce);

        await mqttClient.WriteAsync(new MqttEnvelope
        {
            Topic = "factory/one",
            Payload = [1, 2, 3]
        });

        mqttClient.CompleteMessages();
        await runtime.Completion;

        sink!.Values.ShouldNotBeEmpty();
        sink.Values[^1].MessageCount.ShouldBe(1);
        sink.Values[^1].TotalPayloadBytes.ShouldBe(3);
    }

    [Fact]
    public async Task TriggerFactory_UsesExplicitSubscriberOptions()
    {
        FakeMqttBrokerClient? mqttClient = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(_ =>
            {
                mqttClient = new FakeMqttBrokerClient();
                return mqttClient;
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["broker"] = new NodeDefinition
                {
                    Type = FluxMqNodeTypes.Connection,
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
                            Type = FluxMqNodeTypes.Trigger,
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"broker\"").RootElement.Clone(),
                                ["subscriptions"] = JsonDocument.Parse("""[{ "topicFilter": "factory/#", "qos": 2, "receiveRetained": false, "retainAsPublished": false }]""").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        runtime.Complete();
        await runtime.Completion;

        var subscription = mqttClient!.SubscriptionOptions.ShouldHaveSingleItem();
        subscription.TopicFilter.ShouldBe("factory/#");
        subscription.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.ExactlyOnce);
        subscription.ReceiveRetainedMessages.ShouldBeFalse();
        subscription.RetainAsPublished.ShouldBeFalse();
    }

    [Fact]
    public void ConnectionStateTriggerFactory_UsesConfiguredConnectionResource()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(_ => new FakeMqttBrokerClient()));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["broker"] = new NodeDefinition
                {
                    Type = FluxMqNodeTypes.Connection,
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
                        ["state"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.ConnectionStateTrigger,
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"broker\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        using var runtime = result.Runtime!;
        var node = runtime.Workflows.ShouldHaveSingleItem().Nodes.ShouldHaveSingleItem();
        node.Outputs.ShouldContain(output =>
            output.Address.Port.Value == "Output" &&
            output.ValueType == typeof(MqttClientStateChangedEventArgs));
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
                            Type = FluxMqNodeTypes.GeneratedSource,
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
                        ["metrics"] = NodeWithPort(FluxMqNodeTypes.MqttMetrics, "Input", "\"generated.Output\""),
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
    public async Task GeneratedSourceFactory_RepeatsMessagesWhenLoopHasMaxItems()
    {
        TestSinkNode<MqttEnvelope>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttEnvelope>();
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
                            Type = FluxMqNodeTypes.GeneratedSource,
                            Configuration =
                            {
                                ["messages"] = JsonDocument.Parse("""
                                [
                                  {"topic":"factory/one","payload":"one"},
                                  {"topic":"factory/two","payload":"two"}
                                ]
                                """).RootElement.Clone(),
                                ["loop"] = JsonSerializer.SerializeToElement(true),
                                ["maxItems"] = JsonSerializer.SerializeToElement(3)
                            }
                        },
                        ["sink"] = NodeWithPort("test.envelope-sink", "Input", "\"generated.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sink!.Values.Select(envelope => envelope.Topic).ShouldBe([
            "factory/one",
            "factory/two",
            "factory/one"
        ]);
    }

    [Fact]
    public async Task TimerIntervalFactory_CreatesLinkableRuntimeNode()
    {
        TestSinkNode<TimerTick>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.timer-sink"), (address, _) =>
            {
                sink = new TestSinkNode<TimerTick>();
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
                        ["timer"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.TimerInterval,
                            Configuration =
                            {
                                ["name"] = JsonDocument.Parse("\"poll\"").RootElement.Clone(),
                                ["intervalMilliseconds"] = JsonDocument.Parse("1").RootElement.Clone(),
                                ["emitImmediately"] = JsonDocument.Parse("true").RootElement.Clone(),
                                ["maxTicks"] = JsonDocument.Parse("2").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.timer-sink", "Input", "\"timer.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sink!.Values.Select(tick => tick.Sequence).ShouldBe([1, 2]);
        sink.Values.ShouldAllBe(tick => tick.Name == "poll");
    }

    [Fact]
    public async Task TimerDelayFactory_DelaysConfiguredInputType()
    {
        TestSinkNode<MqttEnvelope>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.envelope-sink"), (address, _) =>
            {
                sink = new TestSinkNode<MqttEnvelope>();
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
                            Type = FluxMqNodeTypes.GeneratedSource,
                            Configuration =
                            {
                                ["messages"] = JsonDocument.Parse("""[{"topic":"factory/one","payload":"one"}]""").RootElement.Clone()
                            }
                        },
                        ["delay"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.TimerDelay,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"generated.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["delayMilliseconds"] = JsonDocument.Parse("0").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.envelope-sink", "Input", "\"delay.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sink!.Values.ShouldHaveSingleItem().Topic.ShouldBe("factory/one");
    }

    [Fact]
    public async Task TimerTickMapper_CanPublishMappedRequestsToConnection()
    {
        FakeMqttBrokerClient? mqttClient = null;
        const string mapperExpression = """
        {
          "topic": "timer/" & name,
          "payload": {
            "sequence": sequence
          },
          "qos": 0,
          "retain": false
        }
        """;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(_ =>
            {
                mqttClient = new FakeMqttBrokerClient();
                return mqttClient;
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["broker"] = new NodeDefinition
                {
                    Type = FluxMqNodeTypes.Connection,
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
                        ["timer"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.TimerInterval,
                            Configuration =
                            {
                                ["name"] = JsonDocument.Parse("\"heartbeat\"").RootElement.Clone(),
                                ["intervalMilliseconds"] = JsonDocument.Parse("1").RootElement.Clone(),
                                ["emitImmediately"] = JsonDocument.Parse("true").RootElement.Clone(),
                                ["maxTicks"] = JsonDocument.Parse("1").RootElement.Clone()
                            }
                        },
                        ["map"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.DynamicMapper,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"timer.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"jsonata\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"TimerTick\"").RootElement.Clone(),
                                ["outputType"] = JsonDocument.Parse("\"MqttPublishRequest\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse(JsonSerializer.Serialize(mapperExpression)).RootElement.Clone()
                            }
                        },
                        ["publisher"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.MqttPublisher,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"map.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"broker\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        mqttClient.ShouldNotBeNull();
        await WaitUntilAsync(() => mqttClient!.Published.Count == 1);
        mqttClient!.CompleteMessages();
        await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        var publish = mqttClient.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("timer/heartbeat");
        Encoding.UTF8.GetString(publish.Payload).ShouldContain("\"sequence\":1");
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtMostOnce);
        publish.Retain.ShouldBeFalse();
    }

    [Fact]
    public async Task DynamicFilterAndJsonataMapper_CanPublishMappedRequestsToConnection()
    {
        FakeMqttBrokerClient? mqttClient = null;
        var runtimeEvents = new List<FlowEvent>();
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
                mqttClient = new FakeMqttBrokerClient();
                return mqttClient;
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["broker2"] = new NodeDefinition
                {
                    Type = FluxMqNodeTypes.Connection,
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
                            Type = FluxMqNodeTypes.GeneratedSource,
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
                            Type = FluxMqNodeTypes.MessageFilter,
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
                            Type = FluxMqNodeTypes.DynamicMapper,
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
                            Type = FluxMqNodeTypes.MqttPublisher,
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
        var eventSink = new ActionBlock<FlowEvent>(runtimeEvents.Add);
        runtime.Events.LinkTo(eventSink, new DataflowLinkOptions { PropagateCompletion = true });
        runtime.Nodes.Single(node => node.Address.Node.Value == "publisher")
            .FindOutput(new PortName("Events"))
            .ShouldBeNull();

        await runtime.StartAsync();
        mqttClient.ShouldNotBeNull();
        mqttClient!.CompleteMessages();
        await Task.WhenAll(runtime.Completion, eventSink.Completion);

        var publish = mqttClient.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("mirror/factory/b");
        publish.Payload.ShouldBe("mapped:keep"u8.ToArray());
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        publish.Retain.ShouldBeFalse();
        var flowEvent = runtimeEvents
            .Where(flowEvent => flowEvent.Type == FluxMqEventTypes.MqttMessagePublished)
            .ShouldHaveSingleItem();
        flowEvent.Type.ShouldBe(FluxMqEventTypes.MqttMessagePublished);
        flowEvent.Channel.ShouldBe("mirror/factory/b");
        flowEvent.PayloadPreview.ShouldBe("mapped:keep");
    }

    [Fact]
    public async Task LiveTriggerAndJsonataMapper_CanPublishMappedRequestsToConnection()
    {
        FakeMqttBrokerClient? mqttClient = null;
        var runtimeEvents = new List<FlowEvent>();
        const string mapperExpression = """
        {
          "topic": 'test',
          "payload": payloadText,
          "qos": qos,
          "retain": retain
        }
        """;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories(profile =>
            {
                mqttClient = new FakeMqttBrokerClient();
                return mqttClient;
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["local-broker"] = new NodeDefinition
                {
                    Type = FluxMqNodeTypes.Connection,
                    Configuration =
                    {
                        ["profile"] = JsonDocument.Parse("""{"name":"local-broker","host":"localhost","port":1883,"clientId":"test-client"}""").RootElement.Clone()
                    }
                }
            },
            Workflows =
            {
                ["pip1"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["trigger"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.Trigger,
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"local-broker\"").RootElement.Clone(),
                                ["subscriptions"] = JsonDocument.Parse("""["#"]""").RootElement.Clone()
                            }
                        },
                        ["mapper"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.DynamicMapper,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"trigger.Output\"").RootElement.Clone()
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
                            Type = FluxMqNodeTypes.MqttPublisher,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"mapper.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["connection"] = JsonDocument.Parse("\"local-broker\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        await using var runtime = result.Runtime!;
        var eventSink = new ActionBlock<FlowEvent>(runtimeEvents.Add);
        runtime.Events.LinkTo(eventSink, new DataflowLinkOptions { PropagateCompletion = true });
        runtime.Nodes.Single(node => node.Address.Node.Value == "trigger")
            .FindOutput(new PortName("Events"))
            .ShouldBeNull();

        await runtime.StartAsync();
        mqttClient.ShouldNotBeNull();
        await mqttClient!.WriteAsync(new MqttEnvelope
        {
            Topic = "factory/source",
            Payload = """{"hello":"fluxmq"}"""u8.ToArray(),
            QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = true
        });
        await WaitUntilAsync(() => mqttClient.Published.Count == 1);
        mqttClient.CompleteMessages();
        await Task.WhenAll(runtime.Completion, eventSink.Completion);

        var publish = mqttClient.Published.ShouldHaveSingleItem();
        publish.Topic.ShouldBe("test");
        publish.Payload.ShouldBe("""{"hello":"fluxmq"}"""u8.ToArray());
        publish.QualityOfService.ShouldBe(MqttQualityOfServiceLevel.AtLeastOnce);
        publish.Retain.ShouldBeTrue();
        var receivedEvent = runtimeEvents
            .Where(flowEvent => flowEvent.Type == FluxMqEventTypes.MqttMessageReceived)
            .ShouldHaveSingleItem();
        receivedEvent.Type.ShouldBe(FluxMqEventTypes.MqttMessageReceived);
        receivedEvent.Channel.ShouldBe("factory/source");
        receivedEvent.PayloadPreview.ShouldBe("""{"hello":"fluxmq"}""");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(25);
        }

        predicate().ShouldBeTrue();
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
                            Type = FluxMqNodeTypes.GeneratedSource,
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
                            Type = FluxMqNodeTypes.DynamicMapper,
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
                        ["writer"] = NodeWithPort(FluxMqNodeTypes.FileWriter, "Input", "\"map.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion;

        File.ReadAllText(Path.Combine(directory, "factory_a.txt")).ShouldBe("topic=factory/a;payload=alpha");
        File.ReadAllText(Path.Combine(directory, "factory_b.txt")).ShouldBe("topic=factory/b;payload=beta");
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task StateReducerFactory_CanReduceMappedInputs()
    {
        TestSinkNode<StateReducerResult>? sink = null;
        const string mapperExpression = """
        {
          "key": topic,
          "input": payloadText,
          "variables": {
            "topic": topic
          }
        }
        """;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new NodeType("test.state-sink"), (address, _) =>
            {
                sink = new TestSinkNode<StateReducerResult>();
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
                        ["traffic"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.GeneratedSource,
                            Configuration =
                            {
                                ["messages"] = JsonDocument.Parse("""
                                [
                                  {"topic":"factory/a","payload":"first"},
                                  {"topic":"factory/a","payload":"second"}
                                ]
                                """).RootElement.Clone()
                            }
                        },
                        ["map"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.DynamicMapper,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"traffic.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"jsonata\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["outputType"] = JsonDocument.Parse("\"StateReducerInput\"").RootElement.Clone(),
                                ["expression"] = JsonDocument.Parse(JsonSerializer.Serialize(mapperExpression)).RootElement.Clone()
                            }
                        },
                        ["state"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.StateReducer,
                            Ports =
                            {
                                ["Input"] = JsonDocument.Parse("\"map.Output\"").RootElement.Clone()
                            },
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"jsonata\"").RootElement.Clone(),
                                ["reducer"] = JsonDocument.Parse("\"value\"").RootElement.Clone()
                            }
                        },
                        ["sink"] = NodeWithPort("test.state-sink", "Input", "\"state.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        await using var runtime = result.Runtime!;

        await runtime.StartAsync();
        await runtime.Completion;

        sink!.Values.Select(value => value.Key).ShouldBe(["factory/a", "factory/a"]);
        sink.Values.Select(value => value.NewState).ShouldBe(["first", "second"]);
        sink.Values.Select(value => value.Version).ShouldBe([1, 2]);
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
                            Type = FluxMqNodeTypes.StoredSessionSource,
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse($"\"{sessionId}\"").RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(FluxMqNodeTypes.MqttMetrics, "Input", "\"stored.Output\""),
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

    [Theory]
    [InlineData("session.source")]
    [InlineData("replay.source")]
    public void StoredSourceFactories_BuildEmptySourceWhenSessionIdIsBlank(string componentType)
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
                        ["stored"] = new NodeDefinition
                        {
                            Type = new NodeType(componentType),
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse("\"\"").RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
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
                            Type = FluxMqNodeTypes.ReplaySource,
                            Configuration =
                            {
                                ["sessionId"] = JsonDocument.Parse($"\"{sessionId}\"").RootElement.Clone(),
                                ["speed"] = JsonDocument.Parse("1000").RootElement.Clone()
                            }
                        },
                        ["metrics"] = NodeWithPort(FluxMqNodeTypes.MqttMetrics, "Input", "\"replay.Output\""),
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
                            Type = FluxMqNodeTypes.ReplaySource,
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
                            Type = FluxMqNodeTypes.StoredSessionSource,
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
                            Type = FluxMqNodeTypes.Trigger,
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
                            Type = FluxMqNodeTypes.PayloadInspector,
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

    [Theory]
    [InlineData("MqttPublishRequest")]
    [InlineData("MqttRecordingRequest")]
    [InlineData("FileWriteRequest")]
    [InlineData("StateReducerInput")]
    public void DynamicMapperFactory_ReturnsBuildErrorWhenExpressionIsMissing(
        string outputType)
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
                        ["mapper"] = new NodeDefinition
                        {
                            Type = FluxMqNodeTypes.DynamicMapper,
                            Configuration =
                            {
                                ["engine"] = JsonDocument.Parse("\"jsonata\"").RootElement.Clone(),
                                ["inputType"] = JsonDocument.Parse("\"MqttEnvelope\"").RootElement.Clone(),
                                ["outputType"] = JsonDocument.Parse(JsonSerializer.Serialize(outputType)).RootElement.Clone()
                            }
                        }
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error =>
            error.Code == ApplicationRuntimeBuildErrorCode.FactoryFailed &&
            error.Message.Contains("flow.mapper") &&
            error.Message.Contains("expression"));
    }

    private static RuntimeNode SourceNode(NodeAddress address, TestSourceNode node)
        => RuntimeNode.Create(
            address,
            node,
            outputs:
            [
                new OutputPort<MqttEnvelope>(address.Port(new PortName("Output")), node.Output)
            ]);

    private static RuntimeNode SourceNode<T>(NodeAddress address, TestValueSourceNode<T> node)
        => RuntimeNode.Create(
            address,
            node,
            outputs:
            [
                new OutputPort<T>(address.Port(new PortName("Output")), node.Output)
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

    private sealed class TestValueSourceNode<T> : IFlowNode
    {
        private readonly BufferBlock<T> _output = new();
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<T> Output => _output;
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _output.Completion;

        public void Post(T value) => _output.Post(value);

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

    private sealed class StorageGetRequestNode : IFlowNode
    {
        private readonly TransformBlock<StorageResult, StorageGetRequest> _input;
        private readonly BufferBlock<FlowError> _errors = new();

        public StorageGetRequestNode()
        {
            _input = new TransformBlock<StorageResult, StorageGetRequest>(
                result => new StorageGetRequest
                {
                    Collection = result.Collection,
                    Key = result.Key,
                    CorrelationId = result.CorrelationId
                });
            _input.Completion.ContinueWith(
                _ => _errors.Complete(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ITargetBlock<StorageResult> Input => _input;
        public ISourceBlock<StorageGetRequest> Output => _input;
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _input.Completion;

        public void Complete()
            => _input.Complete();

        public void Fault(Exception exception)
        {
            ((IDataflowBlock)_input).Fault(exception);
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

    private sealed class FakeMqttBrokerClient : IMqttBrokerClient
    {
        private readonly Channel<MqttEnvelope> _messages = Channel.CreateUnbounded<MqttEnvelope>();
        private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> _subscriptions = [];
        private readonly List<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService, bool ReceiveRetainedMessages, bool RetainAsPublished)> _subscriptionOptions = [];

        public MqttConnectionProfile Profile { get; } = new() { Name = "factory-test" };
        public MqttClientState State { get; private set; } = MqttClientState.Disconnected;
        public ChannelReader<MqttEnvelope> Messages => _messages.Reader;
        public int ConnectCalls { get; private set; }
        public List<PublishedMessage> Published { get; } = [];
        public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService)> Subscriptions => _subscriptions;
        public IReadOnlyList<(string TopicFilter, MqttQualityOfServiceLevel QualityOfService, bool ReceiveRetainedMessages, bool RetainAsPublished)> SubscriptionOptions => _subscriptionOptions;

        public event EventHandler<MqttClientState>? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            State = MqttClientState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            State = MqttClientState.Disconnected;
            _messages.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken ct = default)
            => SubscribeAsync(topicFilter, qos, receiveRetainedMessages: true, retainAsPublished: true, ct);

        public Task SubscribeAsync(
            string topicFilter,
            MqttQualityOfServiceLevel qos,
            bool receiveRetainedMessages,
            bool retainAsPublished = true,
            CancellationToken ct = default)
        {
            _subscriptions.Add((topicFilter, qos));
            _subscriptionOptions.Add((topicFilter, qos, receiveRetainedMessages, retainAsPublished));
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
