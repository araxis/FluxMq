using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Replay;

public sealed class MqttRecordingSinkComponent : IFlowNode
{
    private readonly IMessageRepository _messages;
    private readonly SessionId _sessionId;
    private readonly ActionBlock<MqttEnvelope> _block;
    private readonly BroadcastBlock<FlowError> _errors;

    public MqttRecordingSinkComponent(
        IMessageRepository messages,
        SessionId sessionId,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _sessionId = sessionId;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _block = new ActionBlock<MqttEnvelope>(
            Record,
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
    public ITargetBlock<MqttEnvelope> Input => _block;

    public void Complete() => _block.Complete();

    public void Fault(Exception exception)
    {
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT recording sink faulted.", exception);
        ((IDataflowBlock)_block).Fault(exception);
    }

    private void Record(MqttEnvelope envelope)
    {
        try
        {
            _messages.Add(_sessionId, envelope);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT recording failed.", exception, envelope.Topic);
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
