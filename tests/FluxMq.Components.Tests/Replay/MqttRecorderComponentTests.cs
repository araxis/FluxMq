using Shouldly;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Components.Replay;
using FluxMq.Components.Storage.Models;
using FluxMq.Components.Storage.Repositories;
using FluxMq.Pipeline.Components;
using MQTTnet.Protocol;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.Tests.Replay;

public sealed class MqttRecorderComponentTests
{
    [Fact]
    public async Task Input_RecordsMessagesForSession()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository();
        var component = new MqttRecorderComponent(repository);
        var events = new List<FlowEvent>();
        var eventSink = new ActionBlock<FlowEvent>(events.Add);

        component.Events.LinkTo(eventSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Input.Post(Record(sessionId, "factory/1", [1]));
        component.Input.Post(Record(sessionId, "factory/2", [2]));
        component.Complete();

        await Task.WhenAll(component.Completion, eventSink.Completion);

        repository.Recorded.Select(record => record.SessionId)
            .ShouldBe(new[] { sessionId, sessionId });
        repository.Recorded.Select(record => record.Envelope.Topic)
            .ShouldBe(new[] { "factory/1", "factory/2" });
        events.Select(flowEvent => flowEvent.Type).ShouldBe([FluxMqEventTypes.MqttMessageRecorded, FluxMqEventTypes.MqttMessageRecorded]);
        events.Select(flowEvent => flowEvent.Topic).ShouldBe(["factory/1", "factory/2"]);
        events[0].GetAttribute("sessionId").ShouldBe(sessionId.ToString());
    }

    [Fact]
    public async Task Input_PreservesRecordOrder()
    {
        var repository = new FakeMessageRepository();
        var component = new MqttRecorderComponent(repository);
        var sessionId = SessionId.New();

        component.Input.Post(Record(sessionId, "factory/1"));
        component.Input.Post(Record(sessionId, "factory/2"));
        component.Input.Post(Record(sessionId, "factory/3"));
        component.Complete();

        await component.Completion;

        repository.Recorded.Select(record => record.Envelope.Topic)
            .ShouldBe(new[] { "factory/1", "factory/2", "factory/3" });
    }

    [Fact]
    public async Task RecordFailure_PublishesErrorAndKeepsProcessing()
    {
        var repository = new FakeMessageRepository(topicToFail: "factory/fail");
        var component = new MqttRecorderComponent(repository);
        var sessionId = SessionId.New();
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Record(sessionId, "factory/ok-1"));
        component.Input.Post(Record(sessionId, "factory/fail"));
        component.Input.Post(Record(sessionId, "factory/ok-2"));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        repository.Recorded.Select(record => record.Envelope.Topic)
            .ShouldBe(new[] { "factory/ok-1", "factory/ok-2" });

        var error = errors.ShouldHaveSingleItem();
        error.Code.ShouldBe(FlowErrorCodes.ProcessingFailed);
        error.Context.ShouldBe("factory/fail");
        error.NodeId.ShouldBe(component.Id);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttRecorderComponent(new FakeMessageRepository(), FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("recorder failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("recorder failed");
        await errorSink.Completion;

        errors.ShouldHaveSingleItem().Code.ShouldBe(FlowErrorCodes.NodeFaulted);
    }

    private static MqttEnvelope Message(string topic, byte[]? payload = null) => new()
    {
        Topic = topic,
        Payload = payload ?? [],
        QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
        Retain = false
    };

    private static MqttRecordingRequest Record(SessionId sessionId, string topic, byte[]? payload = null) => new()
    {
        SessionId = sessionId,
        Envelope = Message(topic, payload)
    };

    private sealed class FakeMessageRepository(string? topicToFail = null) : IMessageRepository
    {
        public List<RecordedMessage> Recorded { get; } = [];

        public void Add(SessionId sessionId, MqttEnvelope envelope)
        {
            if (envelope.Topic == topicToFail)
            {
                throw new InvalidOperationException("record failed");
            }

            Recorded.Add(new RecordedMessage(sessionId, envelope));
        }

        public void AddBatch(SessionId sessionId, IEnumerable<MqttEnvelope> envelopes)
        {
            foreach (var envelope in envelopes)
            {
                Add(sessionId, envelope);
            }
        }

        public IReadOnlyList<StoredMessage> GetBySession(SessionId sessionId) => [];
        public IReadOnlyList<StoredMessage> GetByTopic(string topic) => [];
        public async IAsyncEnumerable<StoredMessage> ReadBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<MqttEnvelope> ReadEnvelopesBySessionAsync(
            SessionId sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public long CountBySession(SessionId sessionId) => Recorded.Count(record => record.SessionId == sessionId);
    }

    private sealed record RecordedMessage(SessionId SessionId, MqttEnvelope Envelope);
}
