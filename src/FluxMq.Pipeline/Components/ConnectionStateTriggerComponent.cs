using FluxMq.Core.Session;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Components;

public sealed class ConnectionStateTriggerComponent : IDisposable
{
    private readonly IMqttConnectionManager _connectionManager;
    private readonly BroadcastBlock<SessionStateChangedEventArgs> _output;

    public ConnectionStateTriggerComponent(IMqttConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        _output = new BroadcastBlock<SessionStateChangedEventArgs>(static state => state);
        _connectionManager.StateChanged += OnStateChanged;
    }

    public ISourceBlock<SessionStateChangedEventArgs> Output => _output;

    private void OnStateChanged(object? sender, SessionStateChangedEventArgs e)
    {
        _output.Post(e);
    }

    public void Dispose()
    {
        _connectionManager.StateChanged -= OnStateChanged;
        _output.Complete();
    }
}
