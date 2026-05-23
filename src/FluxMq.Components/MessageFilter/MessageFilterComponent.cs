using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MessageFilter;

public sealed class MessageFilterComponent : IFlowNode
{
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly TransformManyBlock<MqttEnvelope, MqttEnvelope> _block;
    private long _passedCount;
    private readonly Func<MqttEnvelope, bool> _predicate;

    public MessageFilterComponent(Func<MqttEnvelope, bool> predicate, FlowNodeId? id = null, int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _predicate = predicate;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new TransformManyBlock<MqttEnvelope, MqttEnvelope>(
            Filter,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

        _block.Completion.ContinueWith(
            _ => _errors.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _block.Completion;
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttEnvelope> Output => _block;
    public long PassedCount => Interlocked.Read(ref _passedCount);

    public static MessageFilterComponent TopicPrefix(string topicPrefix, StringComparison comparison = StringComparison.Ordinal)
        => new(envelope => envelope.Topic.StartsWith(topicPrefix, comparison));

    public void Complete()
    {
        _block.Complete();
    }

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "Topic filter faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private IEnumerable<MqttEnvelope> Filter(MqttEnvelope envelope)
    {
        bool matched;

        try
        {
            matched = _predicate(envelope);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "Topic filter predicate failed.", exception, envelope.Topic);
            yield break;
        }

        if (matched)
        {
            Interlocked.Increment(ref _passedCount);
            yield return envelope;
        }
    }

    private void PublishError(int code, string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = context
        });
    }
}
