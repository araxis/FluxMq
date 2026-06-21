using FluxMq.Core.Models;
using FluxMq.Core.Payloads;
using FluxMq.Core.TopicIndex;
using FluxMq.UI.Services;
using Shouldly;

namespace FluxMq.UI.Tests;

public sealed class WorkspaceMessageProjectionTests
{
    [Fact]
    public async Task ApplyAsync_UpdatesRecentMessagesPayloadInspectionAndTopicIndex()
    {
        var topicIndex = new TopicIndex();
        var projection = new WorkspaceMessageProjection(topicIndex);
        var message = new MqttEnvelope
        {
            Topic = "factory/temperature",
            Payload = """{"value":21}"""u8.ToArray()
        };

        await projection.ApplyAsync(message);

        projection.RecentMessages.ShouldHaveSingleItem().Topic.ShouldBe("factory/temperature");
        projection.LatestMessage.ShouldBe(message);
        projection.LatestInspection.Format.ShouldBe(PayloadFormat.Json);
        projection.SelectedMessage.ShouldBe(message);
        topicIndex.Search("temperature").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Reset_ClearsProjectionState()
    {
        var projection = new WorkspaceMessageProjection(new TopicIndex());
        await projection.ApplyAsync(new MqttEnvelope { Topic = "factory/one", Payload = [1] });

        projection.Reset();

        projection.RecentMessages.ShouldBeEmpty();
        projection.LatestMessage.ShouldBeNull();
        projection.SelectedMessage.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_PreservesTopicHistoryWhenOtherTopicsExceedRecentLimit()
    {
        var projection = new WorkspaceMessageProjection(new TopicIndex(), recentLimit: 4);

        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "10"));
        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "20"));
        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "30"));

        for (var i = 0; i < 12; i++)
        {
            await projection.ApplyAsync(Message("local-broker", "$SYS/broker/load/messages/received/1min", i.ToString()));
        }

        projection.RecentMessages.Count(message => message.Topic == "$SYS/broker/uptime")
            .ShouldBe(3);

        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "40"));

        projection.RecentMessages
            .Where(message => message.Topic == "$SYS/broker/uptime")
            .Select(static message => System.Text.Encoding.UTF8.GetString(message.Payload))
            .ShouldBe(["40", "30", "20", "10"]);
    }

    [Fact]
    public async Task ApplyAsync_TrimsHistoryPerBrokerTopic()
    {
        var projection = new WorkspaceMessageProjection(new TopicIndex(), recentLimit: 2);

        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "10"));
        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "20"));
        await projection.ApplyAsync(Message("local-broker", "$SYS/broker/uptime", "30"));
        await projection.ApplyAsync(Message("local-broker2", "$SYS/broker/uptime", "100"));
        await projection.ApplyAsync(Message("local-broker2", "$SYS/broker/uptime", "110"));

        projection.RecentMessages
            .Where(message => message.BrokerName == "local-broker" && message.Topic == "$SYS/broker/uptime")
            .Select(static message => System.Text.Encoding.UTF8.GetString(message.Payload))
            .ShouldBe(["30", "20"]);

        projection.RecentMessages
            .Where(message => message.BrokerName == "local-broker2" && message.Topic == "$SYS/broker/uptime")
            .Select(static message => System.Text.Encoding.UTF8.GetString(message.Payload))
            .ShouldBe(["110", "100"]);
    }

    private static MqttEnvelope Message(string brokerName, string topic, string payload)
        => new()
        {
            BrokerName = brokerName,
            Topic = topic,
            Payload = System.Text.Encoding.UTF8.GetBytes(payload)
        };
}
