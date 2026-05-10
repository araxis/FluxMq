using FluentAssertions;
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

        index.Roots.Should().ContainKey("sensors");
        index.Roots["sensors"].MessageCount.Should().Be(1);
    }

    [Fact]
    public void Process_MultiSegment_BuildsTree()
    {
        var index = new TopicIndex();
        index.Process(Envelope("factory/line-01/temperature"));

        index.Roots.Should().ContainKey("factory");
        var factory = index.Roots["factory"];
        factory.Children.Should().ContainKey("line-01");
        factory.Children["line-01"].Children.Should().ContainKey("temperature");
    }

    [Fact]
    public void Process_SameTopic_Twice_IncrementsCount()
    {
        var index = new TopicIndex();
        index.Process(Envelope("sensors/temp"));
        index.Process(Envelope("sensors/temp"));

        index.Find("sensors/temp")!.MessageCount.Should().Be(2);
    }

    [Fact]
    public void Process_DifferentTopicsSameRoot_SharesRootNode()
    {
        var index = new TopicIndex();
        index.Process(Envelope("sensors/temp"));
        index.Process(Envelope("sensors/humidity"));

        index.Roots.Should().ContainKey("sensors");
        index.Roots["sensors"].Children.Should().HaveCount(2);
    }

    [Fact]
    public void Process_MultiSegment_IncrementsAncestorCounts()
    {
        var index = new TopicIndex();
        index.Process(Envelope("factory/line-01/temperature"));
        index.Process(Envelope("factory/line-01/humidity"));

        index.Find("factory")!.MessageCount.Should().Be(2);
        index.Find("factory/line-01")!.MessageCount.Should().Be(2);
        index.Find("factory/line-01/temperature")!.MessageCount.Should().Be(1);
    }

    [Fact]
    public void Process_UpdatesLeafLastMessage()
    {
        var index = new TopicIndex();
        var envelope = Envelope("sensors/temp");
        index.Process(envelope);

        index.Find("sensors/temp")!.LastMessage.Should().BeSameAs(envelope);
    }

    [Fact]
    public void Process_RaisesChangedEvent()
    {
        var index = new TopicIndex();
        var raised = 0;
        index.Changed += (_, _) => raised++;

        index.Process(Envelope("a/b"));
        index.Process(Envelope("a/b"));

        raised.Should().Be(2);
    }

    [Fact]
    public void Find_ReturnsNull_ForUnknownTopic()
    {
        var index = new TopicIndex();
        index.Find("unknown/topic").Should().BeNull();
    }

    [Fact]
    public void Find_ReturnsCorrectNode_ForDeepPath()
    {
        var index = new TopicIndex();
        index.Process(Envelope("a/b/c/d"));

        var node = index.Find("a/b/c/d");
        node.Should().NotBeNull();
        node!.Name.Should().Be("d");
        node.FullPath.Should().Be("a/b/c/d");
        node.Depth.Should().Be(3);
    }

    [Fact]
    public void Search_NoFilter_ReturnsAllNodes()
    {
        var index = new TopicIndex();
        index.Process(Envelope("a/b"));
        index.Process(Envelope("a/c"));
        index.Process(Envelope("x/y/z"));

        // a, a/b, a/c, x, x/y, x/y/z
        index.Search(null).Should().HaveCount(6);
    }

    [Fact]
    public void Search_WithFilter_ReturnsMatchingNodes()
    {
        var index = new TopicIndex();
        index.Process(Envelope("factory/line-01/temp"));
        index.Process(Envelope("factory/line-01/pressure"));
        index.Process(Envelope("sensors/outdoor/temp"));

        var results = index.Search("temp").Select(n => n.FullPath);

        results.Should().Contain("factory/line-01/temp");
        results.Should().Contain("sensors/outdoor/temp");
        results.Should().NotContain("factory/line-01/pressure");
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        var index = new TopicIndex();
        index.Process(Envelope("Sensors/TEMP"));

        index.Search("sensors").Should().NotBeEmpty();
        index.Search("temp").Should().NotBeEmpty();
    }

    [Fact]
    public void Node_DepthIsCorrect_ForEachLevel()
    {
        var index = new TopicIndex();
        index.Process(Envelope("a/b/c"));

        index.Find("a")!.Depth.Should().Be(0);
        index.Find("a/b")!.Depth.Should().Be(1);
        index.Find("a/b/c")!.Depth.Should().Be(2);
    }
}
