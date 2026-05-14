using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Definitions;
using FluxMq.Pipeline.Runtime;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Runtime;

public sealed class ApplicationRuntimeStateTests
{
    [Fact]
    public void State_IsIdleOnCreation()
    {
        var (runtime, _) = MakeRuntime();

        runtime.State.ShouldBe(ApplicationState.Idle);
    }

    [Fact]
    public async Task StartAsync_TransitionsToRunning()
    {
        var (runtime, _) = MakeRuntime();

        await runtime.StartAsync();

        runtime.State.ShouldBe(ApplicationState.Running);
    }

    [Fact]
    public async Task StartAsync_PublishesIdleToStartingThenStartingToRunning()
    {
        var (runtime, _) = MakeRuntime();
        var buffer = new BufferBlock<ApplicationStateChanged>();
        runtime.StateChanges.LinkTo(buffer);

        await runtime.StartAsync();

        var first = await buffer.ReceiveAsync(TimeSpan.FromSeconds(1));
        var second = await buffer.ReceiveAsync(TimeSpan.FromSeconds(1));

        first.Previous.ShouldBe(ApplicationState.Idle);
        first.Current.ShouldBe(ApplicationState.Starting);
        second.Previous.ShouldBe(ApplicationState.Starting);
        second.Current.ShouldBe(ApplicationState.Running);
    }

    [Fact]
    public async Task Complete_TransitionsToStopping()
    {
        var (runtime, _) = MakeRuntime();
        var buffer = new BufferBlock<ApplicationStateChanged>();
        runtime.StateChanges.LinkTo(buffer);
        await runtime.StartAsync();

        runtime.Complete();

        var stopping = await DrainUntilAsync(buffer, ApplicationState.Stopping);
        stopping.Previous.ShouldBe(ApplicationState.Running);
    }

    [Fact]
    public async Task Complete_ThenCompletion_TransitionsToStopped()
    {
        var (runtime, _) = MakeRuntime();
        var buffer = new BufferBlock<ApplicationStateChanged>();
        runtime.StateChanges.LinkTo(buffer);

        await runtime.StartAsync();
        runtime.Complete();

        var stopped = await DrainUntilAsync(buffer, ApplicationState.Stopped);
        stopped.Previous.ShouldBe(ApplicationState.Stopping);
        runtime.State.ShouldBe(ApplicationState.Stopped);
    }

    [Fact]
    public async Task Fault_TransitionsToFaulted()
    {
        var (runtime, _) = MakeRuntime();
        await runtime.StartAsync();

        runtime.Fault(new InvalidOperationException("test"));

        runtime.State.ShouldBe(ApplicationState.Faulted);
    }

    [Fact]
    public async Task Fault_PublishesException()
    {
        var (runtime, _) = MakeRuntime();
        var buffer = new BufferBlock<ApplicationStateChanged>();
        runtime.StateChanges.LinkTo(buffer);
        await runtime.StartAsync();

        var ex = new InvalidOperationException("broken");
        runtime.Fault(ex);

        var faulted = await DrainUntilAsync(buffer, ApplicationState.Faulted);
        faulted.Exception.ShouldBeSameAs(ex);
    }

    [Fact]
    public async Task StartAsync_WhenNodeStartThrows_TransitionsToFaulted()
    {
        var node = new TestNode(onStart: () => throw new InvalidOperationException("startup failed"));
        var address = new NodeAddress(WellKnownScopes.Resources, new NodeName("resource"));
        var runtimeNode = RuntimeNode.Create(address, node);
        var runtime = new ApplicationRuntime([runtimeNode], [], [runtimeNode]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync());

        runtime.State.ShouldBe(ApplicationState.Faulted);
    }

    [Fact]
    public async Task WorkflowStateChanges_AreAccessiblePerWorkflow()
    {
        var node = new TestNode();
        var wfAddress = new NodeAddress("flow", new NodeName("node"));
        var runtimeNode = RuntimeNode.Create(wfAddress, node);
        var workflow = new Workflow(new WorkflowName("flow"), [runtimeNode], [], [runtimeNode]);
        var runtime = new ApplicationRuntime([], [workflow], []);

        var wfBuffer = new BufferBlock<WorkflowStateChanged>();
        workflow.StateChanges.LinkTo(wfBuffer);

        await runtime.StartAsync();

        var change = await wfBuffer.ReceiveAsync(TimeSpan.FromSeconds(1));
        change.Current.ShouldBe(WorkflowState.Starting);
    }

    private static (ApplicationRuntime Runtime, TestNode Node) MakeRuntime()
    {
        var node = new TestNode();
        var address = new NodeAddress(WellKnownScopes.Resources, new NodeName("resource"));
        var runtimeNode = RuntimeNode.Create(address, node);
        return (new ApplicationRuntime([runtimeNode], [], [runtimeNode]), node);
    }

    private static async Task<ApplicationStateChanged> DrainUntilAsync(
        BufferBlock<ApplicationStateChanged> buffer, ApplicationState target)
    {
        ApplicationStateChanged change;
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
