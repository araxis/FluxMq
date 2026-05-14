using Shouldly;
using FluxMq.Core.Models;
using FluxMq.Core.TopicIndex;

namespace FluxMq.Core.Tests.TopicIndexTests;

using TopicIndex = FluxMq.Core.TopicIndex.TopicIndex;

public class TopicIndexTests
{
    private static MqttEnvelope Envelope(string topic) =>
        new() { Topic = topic, Payload = [] };

    [Fact]
    public void Process_SingleSegment_CreatesRootNode()
    {
        var index = new TopicIndex();
        index.Process(Envelope("sensors"));

        index.Roots.ContainsKey("sensors").ShouldBeTrue();
        index.Roots["sensors"].MessageCount.ShouldBe(1);
    }

    [Fact]
    public void Process_MultiSegment_BuildsTree()
    {
        var index = new TopicIndex();
        index.Process(Envelope("factory/line-01/temperature"));

        index.Roots.ContainsKey("factory").ShouldBeTrue();
        var factory = index.Roots["factory"];
        factory.Children.ShouldContainKey("line-01");
        factory.Children["line-01"].Children.ShouldContainKey("temperature");
    }

    [Fact]
    public void Process_SameTopic_Twice_IncrementsCount()
    {
        var index = new TopicIndex();
        index.Process(Envelope("sensors/temp"));
        index.Process(Envelope("sensors/temp"));

        index.Find("sensors/temp")!.MessageCount.ShouldBe(2);
    }

    [Fact]
    public void Process_DifferentTopicsSameRoot_SharesRootNode()
    {
        var index = new TopicIndex();
        index.Process(Envelope("sensors/temp"));
        index.Process(Envelope("sensors/humidity"));

        index.Roots.ContainsKey("sensors").ShouldBeTrue();
        index.Roots["sensors"].Children.Count.ShouldBe(2);
    }

    [Fact]
    public void Process_MultiSegment_IncrementsAncestorCounts()
    {
        var index = new TopicIndex();
        index.Process(Envelope("factory/line-01/temperature"));
        index.Process(Envelope("factory/line-01/humidity"));

        index.Find("factory")!.MessageCount.ShouldBe(2);
        index.Find("factory/line-01")!.MessageCount.ShouldBe(2);
        index.Find("factory/line-01/temperature")!.MessageCount.ShouldBe(1);
    }

    [Fact]
    public void Process_UpdatesLeafLastMessage()
    {
        var index = new TopicIndex();
        var envelope = Envelope("sensors/temp");
        index.Process(envelope);

        index.Find("sensors/temp")!.LastMessage.ShouldBeSameAs(envelope);
    }

    [Fact]
    public void Process_RaisesChangedEvent()
    {
        var index = new TopicIndex();
        var raised = 0;
        index.Changed += (_, _) => raised++;

        index.Process(Envelope("a/b"));
        index.Process(Envelope("a/b"));

        raised.ShouldBe(2);
    }

    [Fact]
    public void Find_ReturnsNull_ForUnknownTopic()
    {
        var index = new TopicIndex();
        index.Find("unknown/topic").ShouldBeNull();
    }

    [Fact]
    public void Find_ReturnsCorrectNode_ForDeepPath()
    {
        var index = new TopicIndex();
        index.Process(Envelope("a/b/c/d"));

        var node = index.Find("a/b/c/d");
        node.ShouldNotBeNull();
        node!.Name.ShouldBe("d");
        node.FullPath.ShouldBe("a/b/c/d");
        node.Depth.ShouldBe(3);
    }

    [Fact]
    public void Search_NoFilter_ReturnsAllNodes()
    {
        var index = new TopicIndex();
        index.Process(Envelope("a/b"));
        index.Process(Envelope("a/c"));
        index.Process(Envelope("x/y/z"));

        // a, a/b, a/c, x, x/y, x/y/z
        index.Search(null).Count().ShouldBe(6);
    }

    [Fact]
    public void Search_WithFilter_ReturnsMatchingNodes()
    {
        var index = new TopicIndex();
        index.Process(Envelope("factory/line-01/temp"));
        index.Process(Envelope("factory/line-01/pressure"));
        index.Process(Envelope("sensors/outdoor/temp"));

        var results = index.Search("temp").Select(n => n.FullPath);

        results.ShouldContain("factory/line-01/temp");
        results.ShouldContain("sensors/outdoor/temp");
        results.ShouldNotContain("factory/line-01/pressure");
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        var index = new TopicIndex();
        index.Process(Envelope("Sensors/TEMP"));

        index.Search("sensors").ShouldNotBeEmpty();
        index.Search("temp").ShouldNotBeEmpty();
    }

    [Fact]
    public void Node_DepthIsCorrect_ForEachLevel()
    {
        var index = new TopicIndex();
        index.Process(Envelope("a/b/c"));

        index.Find("a")!.Depth.ShouldBe(0);
        index.Find("a/b")!.Depth.ShouldBe(1);
        index.Find("a/b/c")!.Depth.ShouldBe(2);
    }
}
