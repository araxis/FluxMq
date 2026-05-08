using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class TopicFilterComponent : IFlowNode
{
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly TransformManyBlock<MqttEnvelope, MqttEnvelope> _block;
    private readonly Func<MqttEnvelope, bool> _predicate;

    public TopicFilterComponent(Func<MqttEnvelope, bool> predicate, FlowNodeId? id = null, int boundedCapacity = 1000)
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

    public static TopicFilterComponent Prefix(string topicPrefix, StringComparison comparison = StringComparison.Ordinal)
        => new(envelope => envelope.Topic.StartsWith(topicPrefix, comparison));

    public void Complete()
    {
        _block.Complete();
    }

    public void Fault(Exception exception)
    {
        PublishError("Topic filter faulted.", exception);
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
            PublishError("Topic filter predicate failed.", exception, envelope.Topic);
            yield break;
        }

        if (matched)
        {
            yield return envelope;
        }
    }

    private void PublishError(string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Message = message,
            Exception = exception,
            Context = context
        });
    }
}
