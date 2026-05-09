using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Runtime;

public sealed class PipelineComponentFactoryTests
{
    [Fact]
    public void RegisterPipelineComponentFactories_RegistersStableComponentTypes()
    {
        var registry = new FlowRuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories();

        registry.Factories.Keys.Should().BeEquivalentTo([
            PipelineFlowNodeTypes.PayloadInspector,
            PipelineFlowNodeTypes.MetricsSink
        ]);
    }

    [Fact]
    public async Task PayloadInspectorFactory_CreatesLinkableRuntimeNode()
    {
        TestSourceNode? source = null;
        TestSinkNode<InspectedMqttMessage>? sink = null;

        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new FlowNodeType("test.source"), (name, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(name, source);
            })
            .Register(new FlowNodeType("test.inspected-sink"), (name, _) =>
            {
                sink = new TestSinkNode<InspectedMqttMessage>();
                return SinkNode(name, sink);
            }));

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.source"),
                    ["inspect"] = NodeWithPort(PipelineFlowNodeTypes.PayloadInspector, "Input", "\"source.Output\""),
                    ["sink"] = NodeWithPort("test.inspected-sink", "Input", "\"inspect.Output\"")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/json", Payload = """{"value":1}"""u8.ToArray() });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Should().ContainSingle();
        sink.Values[0].Envelope.Topic.Should().Be("factory/json");
        sink.Values[0].Payload.Format.Should().Be(PayloadFormat.Json);
    }

    [Fact]
    public async Task MetricsSinkFactory_CreatesLinkableRuntimeNode()
    {
        TestSourceNode? source = null;
        TestSinkNode<MqttMetricsSnapshot>? sink = null;

        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories()
            .Register(new FlowNodeType("test.source"), (name, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(name, source);
            })
            .Register(new FlowNodeType("test.snapshot-sink"), (name, _) =>
            {
                sink = new TestSinkNode<MqttMetricsSnapshot>();
                return SinkNode(name, sink);
            }));

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.source"),
                    ["metrics"] = NodeWithPort(PipelineFlowNodeTypes.MetricsSink, "Input", "\"source.Output\""),
                    ["sink"] = NodeWithPort("test.snapshot-sink", "Input", "\"metrics.Snapshots\"")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();

        source!.Post(new MqttEnvelope { Topic = "factory/one", Payload = [1, 2] });
        source.Post(new MqttEnvelope { Topic = "factory/two", Payload = [1, 2, 3] });
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Should().HaveCount(2);
        sink.Values[^1].MessageCount.Should().Be(2);
        sink.Values[^1].TotalPayloadBytes.Should().Be(5);
    }

    [Fact]
    public void RegisteredFactory_ReturnsBuildErrorForInvalidBoundedCapacity()
    {
        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .RegisterPipelineComponentFactories());

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["inspect"] = new FlowNodeDefinition
                    {
                        Type = PipelineFlowNodeTypes.PayloadInspector,
                        Configuration =
                        {
                            ["boundedCapacity"] = JsonDocument.Parse("0").RootElement.Clone()
                        }
                    }
                }
            }
        });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.Code == FlowApplicationRuntimeBuildErrorCode.FactoryFailed &&
            error.Message.Contains("boundedCapacity"));
    }

    private static FlowRuntimeNode SourceNode(FlowNodeName name, TestSourceNode node)
        => FlowRuntimeNode.Create(
            name,
            node,
            outputs:
            [
                new FlowOutputPort<MqttEnvelope>(new FlowPortName("Output"), node.Output)
            ]);

    private static FlowRuntimeNode SinkNode<T>(FlowNodeName name, TestSinkNode<T> node)
        => FlowRuntimeNode.Create(
            name,
            node,
            inputs:
            [
                new FlowInputPort<T>(new FlowPortName("Input"), node.Input)
            ]);

    private static FlowNodeDefinition Node(string type) => new()
    {
        Type = new FlowNodeType(type)
    };

    private static FlowNodeDefinition NodeWithPort(FlowNodeType type, string portName, string linkJson) => new()
    {
        Type = type,
        Ports =
        {
            [portName] = JsonDocument.Parse(linkJson).RootElement.Clone()
        }
    };

    private static FlowNodeDefinition NodeWithPort(string type, string portName, string linkJson)
        => NodeWithPort(new FlowNodeType(type), portName, linkJson);

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
}
