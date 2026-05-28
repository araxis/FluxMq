namespace FluxMq.Core.Mqtt;

public enum MqttClientState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Reconnecting,
    Faulted
}
