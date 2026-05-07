using FluxMq.Core.Models;
using System.Collections.Concurrent;

namespace FluxMq.Core.TopicIndex;

public sealed class TopicNode
{
    private long _messageCount;
    private volatile MqttEnvelope? _lastMessage;

    public string Name { get; }
    public string FullPath { get; }
    public int Depth { get; }

    public ConcurrentDictionary<string, TopicNode> Children { get; } = new(StringComparer.Ordinal);
    public bool HasChildren => !Children.IsEmpty;

    public long MessageCount => Interlocked.Read(ref _messageCount);
    public MqttEnvelope? LastMessage => _lastMessage;

    // Slightly stale reads are acceptable — this is a display value only
    public DateTimeOffset LastActivity { get; private set; }

    internal TopicNode(string name, string fullPath, int depth)
    {
        Name = name;
        FullPath = fullPath;
        Depth = depth;
    }

    internal void RecordMessage(MqttEnvelope envelope)
    {
        Interlocked.Increment(ref _messageCount);
        _lastMessage = envelope;
        LastActivity = envelope.ReceivedAt;
    }
}
