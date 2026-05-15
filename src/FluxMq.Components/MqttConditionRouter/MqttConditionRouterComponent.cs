using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttConditionRouter;

public sealed class MqttConditionRouterComponent : IFlowNode
{
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly TransformManyBlock<MqttEnvelope, RoutedMqttMessage> _block;
    private readonly TransformBlock<RoutedMqttMessage, MqttEnvelope> _whenTrue;
    private readonly TransformBlock<RoutedMqttMessage, MqttEnvelope> _whenFalse;
    private readonly Func<MqttEnvelope, bool> _condition;

    public MqttConditionRouterComponent(
        Func<MqttEnvelope, bool> condition,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new TransformManyBlock<MqttEnvelope, RoutedMqttMessage>(
            Route,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true
            });

        _whenTrue = CreateOutputBlock(boundedCapacity);
        _whenFalse = CreateOutputBlock(boundedCapacity);

        var propagate = new DataflowLinkOptions { PropagateCompletion = true };
        _block.LinkTo(_whenTrue, propagate, route => route.Matched);
        _block.LinkTo(_whenFalse, propagate, route => !route.Matched);

        _block.Completion.ContinueWith(
            _ => _errors.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => Task.WhenAll(_block.Completion, _whenTrue.Completion, _whenFalse.Completion);
    public ITargetBlock<MqttEnvelope> Input => _block;
    public ISourceBlock<MqttEnvelope> WhenTrue => _whenTrue;
    public ISourceBlock<MqttEnvelope> WhenFalse => _whenFalse;

    public static MqttConditionRouterComponent TopicPrefix(
        string topicPrefix,
        StringComparison comparison = StringComparison.Ordinal)
        => new(envelope => envelope.Topic.StartsWith(topicPrefix, comparison));

    public void Complete()
    {
        _block.Complete();
    }

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT condition router faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private IEnumerable<RoutedMqttMessage> Route(MqttEnvelope envelope)
    {
        bool matched;

        try
        {
            matched = _condition(envelope);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT condition router predicate failed.", exception, envelope.Topic);
            yield break;
        }

        yield return new RoutedMqttMessage(envelope, matched);
    }

    private static TransformBlock<RoutedMqttMessage, MqttEnvelope> CreateOutputBlock(int boundedCapacity)
        => new(route => route.Message, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity,
            EnsureOrdered = true
        });

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

    private sealed record RoutedMqttMessage(MqttEnvelope Message, bool Matched);
}
