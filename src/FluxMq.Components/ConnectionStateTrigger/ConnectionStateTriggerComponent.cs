using FluxMq.Core.Ids;
using FluxMq.Core.Mqtt;
using FluxFlow.Engine.Components;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.ConnectionStateTrigger;

public sealed class ConnectionStateTriggerComponent : IFlowNode, IDisposable
{
    private readonly IMqttConnectionManager? _connectionManager;
    private readonly IMqttBrokerClient? _client;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<MqttClientStateChangedEventArgs> _output;

    public ConnectionStateTriggerComponent(IMqttConnectionManager connectionManager, FlowNodeId? id = null)
    {
        Id = id ?? FlowNodeId.New();
        _connectionManager = connectionManager;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _output = new BufferBlock<MqttClientStateChangedEventArgs>();
        _connectionManager.StateChanged += OnStateChanged;
    }

    public ConnectionStateTriggerComponent(IMqttBrokerClient client, FlowNodeId? id = null)
    {
        Id = id ?? FlowNodeId.New();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _output = new BufferBlock<MqttClientStateChangedEventArgs>();
        _client.StateChanged += OnClientStateChanged;
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public Task Completion => _output.Completion;
    public ISourceBlock<MqttClientStateChangedEventArgs> Output => _output;

    private void OnStateChanged(object? sender, MqttClientStateChangedEventArgs e)
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

    private void OnClientStateChanged(object? sender, MqttClientState state)
    {
        if (_client is null)
        {
            return;
        }

        OnStateChanged(
            sender,
            new MqttClientStateChangedEventArgs(_client.Profile.Id, _client.Profile, state));
    }

    public void Complete()
    {
        if (_connectionManager is not null)
        {
            _connectionManager.StateChanged -= OnStateChanged;
        }

        if (_client is not null)
        {
            _client.StateChanged -= OnClientStateChanged;
        }

        _output.Complete();
        _errors.Complete();
    }

    public void Fault(Exception exception)
    {
        if (_connectionManager is not null)
        {
            _connectionManager.StateChanged -= OnStateChanged;
        }

        if (_client is not null)
        {
            _client.StateChanged -= OnClientStateChanged;
        }

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

file static class MqttClientStateChangedEventArgsExtensions
{
    public static string ProfileName(this MqttClientStateChangedEventArgs args) => args.Profile.Name;
}
