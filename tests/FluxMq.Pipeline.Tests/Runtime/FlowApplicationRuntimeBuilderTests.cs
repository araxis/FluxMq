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

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (name, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(name, source);
            })
            .Register(new NodeType("test.sink"), (name, _) =>
            {
                sink = new TestSinkNode<int>();
                return SinkNode(name, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
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

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.resource"), (name, _) =>
            {
                resource = new TestSourceNode();
                return SourceNode(name, resource);
            })
            .Register(new NodeType("test.sink"), (name, _) =>
            {
                sink = new TestSinkNode<int>();
                return SinkNode(name, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
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
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry());

        var result = builder.Build(new ApplicationDefinition());

        result.IsSuccess.Should().BeFalse();
        result.Runtime.Should().BeNull();
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ApplicationRuntimeBuildErrorCode.ValidationFailed);
    }

    [Fact]
    public void Build_ReturnsUnknownNodeType()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry());

        var result = builder.Build(new ApplicationDefinition
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
            .Which.Code.Should().Be(ApplicationRuntimeBuildErrorCode.UnknownNodeType);
    }

    [Fact]
    public void Build_ReturnsMissingInputPort()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (name, _) => SourceNode(name, new TestSourceNode()))
            .Register(new NodeType("test.sink"), (name, _) => SinkNode(name, new TestSinkNode<int>())));

        var result = builder.Build(new ApplicationDefinition
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
        result.Errors.Should().ContainSingle(error => error.Code == ApplicationRuntimeBuildErrorCode.MissingInputPort);
    }

    [Fact]
    public void Build_ReturnsPortTypeMismatch()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (name, _) => SourceNode(name, new TestSourceNode()))
            .Register(new NodeType("test.string-sink"), (name, _) => StringSinkNode(name, new TestSinkNode<string>())));

        var result = builder.Build(new ApplicationDefinition
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
        result.Errors.Should().ContainSingle(error => error.Code == ApplicationRuntimeBuildErrorCode.PortTypeMismatch);
    }

    [Fact]
    public void Build_PassesResourceAndWorkflowContextToFactories()
    {
        var contexts = new List<RuntimeNodeFactoryContext>();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.node"), context =>
            {
                contexts.Add(context);
                return SourceNode(context.Name, new TestSourceNode());
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.node")
            },
            Workflows =
            {
                ["flow"] = new()
                {
                    ["source"] = Node("test.node")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();
        contexts.Should().ContainSingle(context => context.Name.Value == "shared" && context.IsResource && context.WorkflowName == null);
        contexts.Should().ContainSingle(context => context.Name.Value == "source" && !context.IsResource && context.WorkflowName == "flow");
    }

    [Fact]
    public void Dispose_DisposesWorkflowNodesBeforeSharedResources()
    {
        var disposalOrder = new List<string>();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.resource"), (name, _) => DisposableNode(name, "resource", disposalOrder))
            .Register(new NodeType("test.workflow"), (name, _) => DisposableNode(name, "workflow", disposalOrder)));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.resource")
            },
            Workflows =
            {
                ["flow"] = new()
                {
                    ["worker"] = Node("test.workflow")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();

        result.Runtime!.Dispose();

        disposalOrder.Should().Equal("workflow", "resource");
    }

    [Fact]
    public async Task StartAsync_StartsResourcesBeforeWorkflowNodes()
    {
        var startOrder = new List<string>();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.resource"), (name, _) => StartableNode(name, "resource", startOrder))
            .Register(new NodeType("test.workflow"), (name, _) => StartableNode(name, "workflow", startOrder)));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.resource")
            },
            Workflows =
            {
                ["flow"] = new()
                {
                    ["worker"] = Node("test.workflow")
                }
            }
        });

        result.IsSuccess.Should().BeTrue();

        await result.Runtime!.StartAsync();

        startOrder.Should().Equal("resource", "workflow");
    }

    private static RuntimeNode SourceNode(NodeName name, TestSourceNode node)
        => RuntimeNode.Create(
            name,
            node,
            outputs:
            [
                new OutputPort<int>(new PortName("Output"), node.Output)
            ]);

    private static RuntimeNode SinkNode(NodeName name, TestSinkNode<int> node)
        => RuntimeNode.Create(
            name,
            node,
            inputs:
            [
                new InputPort<int>(new PortName("Input"), node.Input)
            ]);

    private static RuntimeNode StringSinkNode(NodeName name, TestSinkNode<string> node)
        => RuntimeNode.Create(
            name,
            node,
            inputs:
            [
                new InputPort<string>(new PortName("Input"), node.Input)
            ]);

    private static RuntimeNode DisposableNode(NodeName name, string label, List<string> disposalOrder)
        => RuntimeNode.Create(name, new TestDisposableNode(label, disposalOrder));

    private static RuntimeNode StartableNode(NodeName name, string label, List<string> startOrder)
        => RuntimeNode.Create(name, new TestStartableNode(label, startOrder));

    private static NodeDefinition Node(string type) => new()
    {
        Type = new NodeType(type)
    };

    private static NodeDefinition NodeWithPort(string type, string portName, string linkJson) => new()
    {
        Type = new NodeType(type),
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

    private sealed class TestDisposableNode : IFlowNode, IDisposable
    {
        private readonly string _label;
        private readonly List<string> _disposalOrder;
        private readonly BufferBlock<FlowError> _errors = new();

        public TestDisposableNode(string label, List<string> disposalOrder)
        {
            _label = label;
            _disposalOrder = disposalOrder;
        }

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => Task.CompletedTask;

        public void Complete() => _errors.Complete();

        public void Fault(Exception exception) => _errors.Complete();

        public void Dispose() => _disposalOrder.Add(_label);
    }

    private sealed class TestStartableNode : IFlowNode, IFlowStartable
    {
        private readonly string _label;
        private readonly List<string> _startOrder;
        private readonly BufferBlock<FlowError> _errors = new();

        public TestStartableNode(string label, List<string> startOrder)
        {
            _label = label;
            _startOrder = startOrder;
        }

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _startOrder.Add(_label);
            return Task.CompletedTask;
        }

        public void Complete() => _errors.Complete();

        public void Fault(Exception exception) => _errors.Complete();
    }
}
