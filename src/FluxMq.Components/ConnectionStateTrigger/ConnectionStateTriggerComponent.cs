using FluxMq.Core.Ids;
using FluxMq.Core.Session;
using FluxMq.Pipeline.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.ConnectionStateTrigger;

public sealed class ConnectionStateTriggerComponent : IFlowNode, IDisposable
{
    private readonly IMqttConnectionManager _connectionManager;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BroadcastBlock<SessionStateChangedEventArgs> _output;

    public ConnectionStateTriggerComponent(IMqttConnectionManager connectionManager, FlowNodeId? id = null)
    {
        Id = id ?? FlowNodeId.New();
        _connectionManager = connectionManager;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _output = new BroadcastBlock<SessionStateChangedEventArgs>(static state => state);
        _connectionManager.StateChanged += OnStateChanged;
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _output.Completion;
    public ISourceBlock<SessionStateChangedEventArgs> Output => _output;

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        try
        {
            _output.Post(e);
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "Connection state could not be published.", exception, e.ProfileName());
        }
    }

    public void Complete()
    {
        _connectionManager.StateChanged -= OnStateChanged;
        _output.Complete();
        _errors.Complete();
    }

    public void Fault(Exception exception)
    {
        _connectionManager.StateChanged -= OnStateChanged;
        PublishError(FlowErrorCodes.NodeFaulted, "Connection state trigger faulted.", exception);
        ((IDataflowBlock)_output).Fault(exception);
        _errors.Complete();
    }

    public void Dispose() => Complete();

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

file static class SessionStateChangedEventArgsExtensions
{
    public static string ProfileName(this SessionStateChangedEventArgs args) => args.Profile.Name;
}
