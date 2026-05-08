using FluxMq.Core.Models;

namespace FluxMq.Core.TopicIndex;

public interface ITopicIndex
{
    IReadOnlyDictionary<string, TopicNode> Roots { get; }

    /// <summary>
    /// Raised after each message is indexed. Fires frequently at high throughput —
    /// UI consumers should throttle their StateHasChanged calls.
    /// </summary>
    event EventHandler? Changed;

    void Process(MqttEnvelope envelope);

    TopicNode? Find(string topicPath);

    /// <summary>
    /// Returns all nodes whose full path contains <paramref name="filter"/>.
    /// Returns all nodes when filter is null or whitespace.
    /// </summary>
    IEnumerable<TopicNode> Search(string? filter);
}
