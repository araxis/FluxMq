namespace FluxMq.Core.Session;

public enum MqttSessionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Reconnecting,
    Faulted
}
