using FluxMq.Core.Ids;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttPublisher;

public sealed class MqttPublisherComponent : IFlowNode
{
    private readonly IMqttSession _session;
    private readonly ActionBlock<MqttPublishRequest> _block;
    private readonly BroadcastBlock<FlowError> _errors;

    public MqttPublisherComponent(
        IMqttSession session,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new ActionBlock<MqttPublishRequest>(
            PublishAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
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
    public ITargetBlock<MqttPublishRequest> Input => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT publisher faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private async Task PublishAsync(MqttPublishRequest request)
    {
        try
        {
            await _session.PublishAsync(
                request.Topic,
                request.Payload,
                request.QualityOfService,
                request.Retain).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT publish failed.", exception, request.Topic);
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
