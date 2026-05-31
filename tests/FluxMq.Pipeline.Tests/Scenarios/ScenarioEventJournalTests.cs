using FluxMq.Pipeline.Components;
using FluxMq.Pipeline.Scenarios;
using Shouldly;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Pipeline.Tests.Scenarios;

public sealed class ScenarioEventJournalTests
{
    [Fact]
    public async Task Append_NotifiesObserverForRunnerOwnedEventsOnly()
    {
        var sourceEvents = new BufferBlock<FlowEvent>();
        var observer = new TestScenarioEventObserver();
        using var journal = new ScenarioEventJournal(
            sourceEvents,
            runnerOwnedEventObserver: observer);

        sourceEvents.Post(Event("source"));
        var sourceMatch = await journal.WaitForMatchAsync(
                0,
                flowEvent => flowEvent.Topic == "source",
                TimeSpan.FromSeconds(1))
            .WaitAsync(TimeSpan.FromSeconds(2));

        sourceMatch.ShouldNotBeNull();
        observer.Events.ShouldBeEmpty();

        journal.Append(Event("runner"));

        var runnerMatch = await journal.WaitForMatchAsync(
                0,
                flowEvent => flowEvent.Topic == "runner",
                TimeSpan.FromSeconds(1))
            .WaitAsync(TimeSpan.FromSeconds(2));

        runnerMatch.ShouldNotBeNull();
        observer.Events.ShouldHaveSingleItem().Topic.ShouldBe("runner");
    }

    private static FlowEvent Event(string topic)
        => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FlowEventTypes.MqttMessagePublished,
            Source = "test",
            Topic = topic,
            Status = "published"
        };

    private sealed class TestScenarioEventObserver : IScenarioEventObserver
    {
        public List<FlowEvent> Events { get; } = [];

        public void Observe(FlowEvent flowEvent)
            => Events.Add(flowEvent);
    }
}
