using FluxMq.Core.Models;
using System.Collections.Concurrent;

namespace FluxMq.Core.TopicIndex;

public sealed class TopicIndex : ITopicIndex
{
    private readonly ConcurrentDictionary<string, TopicNode> _roots = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TopicNode> Roots => _roots;

    public event EventHandler? Changed;

    public void Process(MqttEnvelope envelope)
    {
        var segments = envelope.Topic.Split('/');
        foreach (var node in GetOrCreatePath(segments))
        {
            node.RecordMessage(envelope);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public TopicNode? Find(string topicPath)
    {
        var segments = topicPath.Split('/');
        if (!_roots.TryGetValue(segments[0], out var node))
            return null;

        for (var i = 1; i < segments.Length; i++)
            if (!node.Children.TryGetValue(segments[i], out node))
                return null;

        return node;
    }

    public IEnumerable<TopicNode> Search(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
            ? Flatten(_roots.Values)
            : Flatten(_roots.Values)
                .Where(n => n.FullPath.Contains(filter, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<TopicNode> GetOrCreatePath(string[] segments)
    {
        var node = _roots.GetOrAdd(segments[0], k => new TopicNode(k, k, 0));
        yield return node;

        var path = segments[0];

        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            path = $"{path}/{segment}";
            var nodePath = path;
            var depth = i;
            node = node.Children.GetOrAdd(segment, k => new TopicNode(k, nodePath, depth));
            yield return node;
        }
    }

    private static IEnumerable<TopicNode> Flatten(IEnumerable<TopicNode> nodes)
    {
        var queue = new Queue<TopicNode>(nodes);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            yield return node;
            foreach (var child in node.Children.Values)
                queue.Enqueue(child);
        }
    }
}
