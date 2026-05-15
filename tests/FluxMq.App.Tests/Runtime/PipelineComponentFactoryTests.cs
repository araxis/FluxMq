using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.Session;
using FluxMq.App;
using FluxMq.Components.MqttMetrics;
using FluxMq.Components.MqttPayloadInspector;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using MQTTnet.Protocol;
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
            PipelineFlowNodeTypes.PayloadInspector,
            PipelineFlowNodeTypes.MetricsSink
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
    public async Task MetricsSinkFactory_CreatesLinkableRuntimeNode()
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
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MetricsSink, "Input", "\"source.Output\""),
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
                        ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MetricsSink, "Input", "\"trigger.Output\""),
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
            => Task.CompletedTask;

        public Task WriteAsync(MqttEnvelope message) => _messages.Writer.WriteAsync(message).AsTask();

        public void CompleteMessages() => _messages.Writer.TryComplete();

        public ValueTask DisposeAsync()
        {
            _messages.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
