using Shouldly;
using FluxMq.Core.Ids;
using FluxFlow.Engine.Components;
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
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.sink"), (address, _) =>
            {
                sink = new TestSinkNode<int>();
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
                        ["sink"] = NodeWithPort("test.sink", "Input", "\"source.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();

        source!.Post(1);
        source.Post(2);
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public async Task Build_FansOutOneOutputToMultipleTargets()
    {
        TestSourceNode? source = null;
        TestSinkNode<int>? sinkA = null;
        TestSinkNode<int>? sinkB = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.sink-a"), (address, _) =>
            {
                sinkA = new TestSinkNode<int>();
                return SinkNode(address, sinkA);
            })
            .Register(new NodeType("test.sink-b"), (address, _) =>
            {
                sinkB = new TestSinkNode<int>();
                return SinkNode(address, sinkB);
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
                        ["sinkA"] = NodeWithPort("test.sink-a", "Input", "\"source.Output\""),
                        ["sinkB"] = NodeWithPort("test.sink-b", "Input", "\"source.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();

        source!.Post(1);
        source.Post(2);
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sinkA!.Values.ShouldBe(new[] { 1, 2 });
        sinkB!.Values.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public async Task Build_MultipleLinksToSameInput_WaitsForAllSourceCompletions()
    {
        TestSourceNode? sourceA = null;
        TestSourceNode? sourceB = null;
        TestSinkNode<int>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                var node = new TestSourceNode();
                if (address.Node.Value == "sourceA")
                {
                    sourceA = node;
                }
                else
                {
                    sourceB = node;
                }

                return SourceNode(address, node);
            })
            .Register(new NodeType("test.sink"), (address, _) =>
            {
                sink = new TestSinkNode<int>();
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
                        ["sourceA"] = Node("test.source"),
                        ["sourceB"] = Node("test.source"),
                        ["sink"] = NodeWithPort("test.sink", "Input", "[\"sourceA.Output\", \"sourceB.Output\"]")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        sourceA!.Post(1);
        sourceA.Complete();
        await Task.Delay(50);

        sourceB!.Post(2);
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        sink!.Values.ShouldBe(new[] { 1, 2 });
    }

    [Fact]
    public async Task Build_DrainsUnlinkedWorkflowOutputs()
    {
        TestSourceNode? source = null;
        TestTransformNode? transform = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (address, _) =>
            {
                source = new TestSourceNode();
                return SourceNode(address, source);
            })
            .Register(new NodeType("test.transform"), (address, _) =>
            {
                transform = new TestTransformNode();
                return TransformNode(address, transform);
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
                        ["transform"] = NodeWithPort("test.transform", "Input", "\"source.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        source!.Post(42);
        result.Runtime!.Complete();

        await result.Runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        transform!.Inputs.ShouldBe([42]);
    }

    [Fact]
    public async Task Build_LinksWorkflowNodeFromSharedResource()
    {
        TestSourceNode? resource = null;
        TestSinkNode<int>? sink = null;

        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.resource"), (address, _) =>
            {
                resource = new TestSourceNode();
                return SourceNode(address, resource);
            })
            .Register(new NodeType("test.sink"), (address, _) =>
            {
                sink = new TestSinkNode<int>();
                return SinkNode(address, sink);
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.resource")
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["sink"] = NodeWithPort("test.sink", "Input", "\"$resources.shared.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        resource!.Post(42);
        result.Runtime!.Complete();

        await result.Runtime.Completion;

        sink!.Values.ShouldBe(new[] { 42 });
    }

    [Fact]
    public void Build_ReturnsValidationErrors()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry());

        var result = builder.Build(new ApplicationDefinition());

        result.IsSuccess.ShouldBeFalse();
        result.Runtime.ShouldBeNull();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(ApplicationRuntimeBuildErrorCode.ValidationFailed);
    }

    [Fact]
    public void Build_PreservesValidationErrorScope()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["metrics"] = NodeWithPort("test.sink", "Input", "\"missing.Output\"")
                    }
                }
            }
        });

        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(ApplicationRuntimeBuildErrorCode.ValidationFailed);
        error.WorkflowName.ShouldBe("flow");
        error.NodeName?.Value.ShouldBe("metrics");
        error.PortName?.Value.ShouldBe("Input");
    }

    [Fact]
    public void Build_ReturnsUnknownNodeType()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry());

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().Code.ShouldBe(ApplicationRuntimeBuildErrorCode.UnknownNodeType);
    }

    [Fact]
    public void Build_ReturnsMissingInputPort()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (address, _) => SourceNode(address, new TestSourceNode()))
            .Register(new NodeType("test.sink"), (address, _) => SinkNode(address, new TestSinkNode<int>())));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source"),
                        ["sink"] = NodeWithPort("test.sink", "Unknown", "\"source.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Code == ApplicationRuntimeBuildErrorCode.MissingInputPort);
    }

    [Fact]
    public void Build_ReturnsPortTypeMismatch()
    {
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.source"), (address, _) => SourceNode(address, new TestSourceNode()))
            .Register(new NodeType("test.string-sink"), (address, _) => StringSinkNode(address, new TestSinkNode<string>())));

        var result = builder.Build(new ApplicationDefinition
        {
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.source"),
                        ["sink"] = NodeWithPort("test.string-sink", "Input", "\"source.Output\"")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.Code == ApplicationRuntimeBuildErrorCode.PortTypeMismatch);
    }

    [Fact]
    public void Build_PassesResourceAndWorkflowContextToFactories()
    {
        var contexts = new List<RuntimeNodeFactoryContext>();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.node"), context =>
            {
                contexts.Add(context);
                return SourceNode(context.Address, new TestSourceNode());
            }));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.node")
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["source"] = Node("test.node")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();
        contexts.ShouldContain(context => context.Name.Value == "shared" && context.IsResource && context.WorkflowName == null);
        contexts.ShouldContain(context => context.Name.Value == "source" && !context.IsResource && context.WorkflowName == "flow");
    }

    [Fact]
    public void Dispose_DisposesWorkflowNodesBeforeSharedResources()
    {
        var disposalOrder = new List<string>();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.resource"), (address, _) => DisposableNode(address, "resource", disposalOrder))
            .Register(new NodeType("test.workflow"), (address, _) => DisposableNode(address, "workflow", disposalOrder)));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.resource")
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["worker"] = Node("test.workflow")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        result.Runtime!.Dispose();

        disposalOrder.ShouldBe(new[] { "workflow", "resource" });
    }

    [Fact]
    public async Task StartAsync_StartsResourcesBeforeWorkflowNodes()
    {
        var startOrder = new List<string>();
        var builder = new ApplicationRuntimeBuilder(new RuntimeNodeFactoryRegistry()
            .Register(new NodeType("test.resource"), (address, _) => StartableNode(address, "resource", startOrder))
            .Register(new NodeType("test.workflow"), (address, _) => StartableNode(address, "workflow", startOrder)));

        var result = builder.Build(new ApplicationDefinition
        {
            Resources =
            {
                ["shared"] = Node("test.resource")
            },
            Workflows =
            {
                ["flow"] = new WorkflowDefinition
                {
                    Nodes =
                    {
                        ["worker"] = Node("test.workflow")
                    }
                }
            }
        });

        result.IsSuccess.ShouldBeTrue();

        await result.Runtime!.StartAsync();

        startOrder.ShouldBe(new[] { "resource", "workflow" });
    }

    private static RuntimeNode SourceNode(NodeAddress address, TestSourceNode node)
        => RuntimeNode.Create(
            address,
            node,
            outputs:
            [
                new OutputPort<int>(address.Port(new PortName("Output")), node.Output)
            ]);

    private static RuntimeNode SinkNode(NodeAddress address, TestSinkNode<int> node)
        => RuntimeNode.Create(
            address,
            node,
            inputs:
            [
                new InputPort<int>(address.Port(new PortName("Input")), node.Input)
            ]);

    private static RuntimeNode TransformNode(NodeAddress address, TestTransformNode node)
        => RuntimeNode.Create(
            address,
            node,
            inputs:
            [
                new InputPort<int>(address.Port(new PortName("Input")), node.Input)
            ],
            outputs:
            [
                new OutputPort<int>(address.Port(new PortName("Output")), node.Output)
            ]);

    private static RuntimeNode StringSinkNode(NodeAddress address, TestSinkNode<string> node)
        => RuntimeNode.Create(
            address,
            node,
            inputs:
            [
                new InputPort<string>(address.Port(new PortName("Input")), node.Input)
            ]);

    private static RuntimeNode DisposableNode(NodeAddress address, string label, List<string> disposalOrder)
        => RuntimeNode.Create(address, new TestDisposableNode(label, disposalOrder));

    private static RuntimeNode StartableNode(NodeAddress address, string label, List<string> startOrder)
        => RuntimeNode.Create(address, new TestStartableNode(label, startOrder));

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

    private sealed class TestTransformNode : IFlowNode
    {
        private readonly TransformBlock<int, int> _block;
        private readonly BufferBlock<FlowError> _errors = new();
        private readonly List<int> _inputs = [];

        public TestTransformNode()
        {
            _block = new TransformBlock<int, int>(value =>
            {
                _inputs.Add(value);
                return value;
            });
        }

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ITargetBlock<int> Input => _block;
        public ISourceBlock<int> Output => _block;
        public ISourceBlock<FlowError> Errors => _errors;
        public IReadOnlyList<int> Inputs => _inputs;
        public Task Completion => _block.Completion;

        public void Complete()
        {
            _block.Complete();
            _errors.Complete();
        }

        public void Fault(Exception exception)
        {
            ((IDataflowBlock)_block).Fault(exception);
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

    private sealed class TestStartableNode : IFlowNode
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
