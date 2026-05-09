using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Runtime;

public sealed class FlowApplicationRuntimeBuilderTests
{
    [Fact]
    public async Task Build_LinksWorkflowNodes()
    {
        TestSourceNode? source = null;
        TestSinkNode<int>? sink = null;

        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .Register(new FlowNodeType("test.source"), (name, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(name, source);
            })
            .Register(new FlowNodeType("test.sink"), (name, _) =>
            {
                sink = new TestSinkNode<int>();
                return SinkNode(name, sink);
            }));

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.source"),
                    ["sink"] = NodeWithPort("test.sink", "Input", "\"source.Output\"")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        source!.Post(1);
        source.Post(2);
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Build_LinksWorkflowNodeFromSharedResource()
    {
        TestSourceNode? resource = null;
        TestSinkNode<int>? sink = null;

        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .Register(new FlowNodeType("test.resource"), (name, _) =>
            {
                resource = new TestSourceNode();
                return SourceNode(name, resource);
            })
            .Register(new FlowNodeType("test.sink"), (name, _) =>
            {
                sink = new TestSinkNode<int>();
                return SinkNode(name, sink);
            }));

        var result = builder.Build(new FlowApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.resource")
            },
            Workflows =
            {
                ["flow"] = new()
                {
                    ["sink"] = NodeWithPort("test.sink", "Input", "\"shared.Output\"")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();

        resource!.Post(42);
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.Should().Equal(42);
    }

    [Fact]
    public void Build_ReturnsValidationErrors()
    {
        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry());

        var result = builder.Build(new FlowApplicationDefinition());

        result.IsSuccess.Should().BeFalse();
        result.Runtime.Should().BeNull();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(FlowApplicationRuntimeBuildErrorCode.ValidationFailed);
    }

    [Fact]
    public void Build_ReturnsUnknownNodeType()
    {
        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry());

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.source")
                }
            }
        });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(FlowApplicationRuntimeBuildErrorCode.UnknownNodeType);
    }

    [Fact]
    public void Build_ReturnsMissingInputPort()
    {
        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .Register(new FlowNodeType("test.source"), (name, _) => SourceNode(name, new TestSourceNode()))
            .Register(new FlowNodeType("test.sink"), (name, _) => SinkNode(name, new TestSinkNode<int>())));

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.source"),
                    ["sink"] = NodeWithPort("test.sink", "Unknown", "\"source.Output\"")
                }
            }
        });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Code == FlowApplicationRuntimeBuildErrorCode.MissingInputPort);
    }

    [Fact]
    public void Build_ReturnsPortTypeMismatch()
    {
        var builder = new FlowApplicationRuntimeBuilder(new FlowRuntimeNodeFactoryRegistry()
            .Register(new FlowNodeType("test.source"), (name, _) => SourceNode(name, new TestSourceNode()))
            .Register(new FlowNodeType("test.string-sink"), (name, _) => StringSinkNode(name, new TestSinkNode<string>())));

        var result = builder.Build(new FlowApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.source"),
                    ["sink"] = NodeWithPort("test.string-sink", "Input", "\"source.Output\"")
                }
            }
        });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Code == FlowApplicationRuntimeBuildErrorCode.PortTypeMismatch);
    }

    private static FlowRuntimeNode SourceNode(FlowNodeName name, TestSourceNode node)
        => FlowRuntimeNode.Create(
            name,
            node,
            outputs:
            [
                new FlowOutputPort<int>(new FlowPortName("Output"), node.Output)
            ]);

    private static FlowRuntimeNode SinkNode(FlowNodeName name, TestSinkNode<int> node)
        => FlowRuntimeNode.Create(
            name,
            node,
            inputs:
            [
                new FlowInputPort<int>(new FlowPortName("Input"), node.Input)
            ]);

    private static FlowRuntimeNode StringSinkNode(FlowNodeName name, TestSinkNode<string> node)
        => FlowRuntimeNode.Create(
            name,
            node,
            inputs:
            [
                new FlowInputPort<string>(new FlowPortName("Input"), node.Input)
            ]);

    private static FlowNodeDefinition Node(string type) => new()
    {
        Type = new FlowNodeType(type)
    };

    private static FlowNodeDefinition NodeWithPort(string type, string portName, string linkJson) => new()
    {
        Type = new FlowNodeType(type),
        Ports =
        {
            [portName] = System.Text.Json.JsonDocument.Parse(linkJson).RootElement.Clone()
        }
    };

    private sealed class TestSourceNode : IFlowNode
    {
        private readonly BufferBlock<int> _output = new();
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<int> Output => _output;
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _output.Completion;

        public void Post(int value) => _output.Post(value);

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
