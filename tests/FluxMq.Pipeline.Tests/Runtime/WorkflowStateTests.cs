using Shouldly;
using FluxMq.Core.Ids;
using FluxFlow.Engine.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Runtime;

public sealed class WorkflowStateTests
{
    [Fact]
    public void State_IsIdleOnCreation()
    {
        var workflow = MakeWorkflow();

        workflow.State.ShouldBe(WorkflowState.Idle);
    }

    [Fact]
    public async Task StartAsync_TransitionsToRunning()
    {
        var workflow = MakeWorkflow();

        await workflow.StartAsync();

        workflow.State.ShouldBe(WorkflowState.Running);
    }

    [Fact]
    public async Task StartAsync_PublishesIdleToStartingThenStartingToRunning()
    {
        var workflow = MakeWorkflow();
        var buffer = new BufferBlock<WorkflowStateChanged>();
        workflow.StateChanges.LinkTo(buffer);

        await workflow.StartAsync();

        var first = await buffer.ReceiveAsync(TimeSpan.FromSeconds(1));
        var second = await buffer.ReceiveAsync(TimeSpan.FromSeconds(1));

        first.Previous.ShouldBe(WorkflowState.Idle);
        first.Current.ShouldBe(WorkflowState.Starting);
        second.Previous.ShouldBe(WorkflowState.Starting);
        second.Current.ShouldBe(WorkflowState.Running);
    }

    [Fact]
    public async Task Complete_TransitionsToStopping()
    {
        var workflow = MakeWorkflow();
        var buffer = new BufferBlock<WorkflowStateChanged>();
        workflow.StateChanges.LinkTo(buffer);
        await workflow.StartAsync();

        workflow.Complete();

        var stopping = await DrainUntilAsync(buffer, WorkflowState.Stopping);
        stopping.Previous.ShouldBe(WorkflowState.Running);
    }

    [Fact]
    public async Task Complete_ThenCompletion_TransitionsToStopped()
    {
        var workflow = MakeWorkflow();
        var buffer = new BufferBlock<WorkflowStateChanged>();
        workflow.StateChanges.LinkTo(buffer);

        await workflow.StartAsync();
        workflow.Complete();

        var stopped = await DrainUntilAsync(buffer, WorkflowState.Stopped);
        stopped.Previous.ShouldBe(WorkflowState.Stopping);
        workflow.State.ShouldBe(WorkflowState.Stopped);
    }

    [Fact]
    public async Task Fault_TransitionsToFaulted()
    {
        var workflow = MakeWorkflow();
        await workflow.StartAsync();

        workflow.Fault(new InvalidOperationException("test"));

        workflow.State.ShouldBe(WorkflowState.Faulted);
    }

    [Fact]
    public async Task Fault_PublishesExceptionAndWorkflowName()
    {
        var workflow = MakeWorkflow(workflowName: "my-flow");
        var buffer = new BufferBlock<WorkflowStateChanged>();
        workflow.StateChanges.LinkTo(buffer);
        await workflow.StartAsync();

        var ex = new InvalidOperationException("broken");
        workflow.Fault(ex);

        var faulted = await DrainUntilAsync(buffer, WorkflowState.Faulted);
        faulted.Exception.ShouldBeSameAs(ex);
        faulted.WorkflowName.ShouldBe(new WorkflowName("my-flow"));
    }

    [Fact]
    public async Task StartAsync_WhenNodeStartThrows_TransitionsToFaulted()
    {
        var node = new TestNode(onStart: () => throw new InvalidOperationException("startup failed"));
        var workflow = MakeWorkflow(node);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.StartAsync());

        workflow.State.ShouldBe(WorkflowState.Faulted);
    }

    private static Workflow MakeWorkflow(TestNode? node = null, string workflowName = "test")
    {
        node ??= new TestNode();
        var address = new NodeAddress(workflowName, new NodeName("node"));
        var runtimeNode = RuntimeNode.Create(address, node);
        return new Workflow(new WorkflowName(workflowName), [runtimeNode], [], [runtimeNode]);
    }

    private static async Task<WorkflowStateChanged> DrainUntilAsync(
        BufferBlock<WorkflowStateChanged> buffer, WorkflowState target)
    {
        WorkflowStateChanged change;
        do { change = await buffer.ReceiveAsync(TimeSpan.FromSeconds(5)); }
        while (change.Current != target);
        return change;
    }

    private sealed class TestNode(Action? onStart = null) : IFlowNode
    {
        private readonly TaskCompletionSource _completion = new();
        private readonly BufferBlock<FlowError> _errors = new();

        public FlowNodeId Id { get; } = FlowNodeId.New();
        public ISourceBlock<FlowError> Errors => _errors;
        public Task Completion => _completion.Task;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            onStart?.Invoke();
            return Task.CompletedTask;
        }

        public void Complete()
        {
            _completion.TrySetResult();
            _errors.Complete();
        }

        public void Fault(Exception exception)
        {
            _completion.TrySetException(exception);
            ((IDataflowBlock)_errors).Fault(exception);
        }
    }
}
