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
}
