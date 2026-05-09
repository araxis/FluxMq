using FluentAssertions;
using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Pipeline.Components;
using FluxMq.Replay;
using FluxMq.Storage.Models;
using FluxMq.Storage.Repositories;
using MQTTnet.Protocol;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Replay.Tests;

public sealed class MqttRecordingSinkComponentTests
{
    [Fact]
    public async Task Input_RecordsMessagesForSession()
    {
        var sessionId = SessionId.New();
        var repository = new FakeMessageRepository();
        var component = new MqttRecordingSinkComponent(repository, sessionId);

        component.Input.Post(Message("factory/1", [1]));
        component.Input.Post(Message("factory/2", [2]));
        component.Complete();

        await component.Completion;

        repository.Recorded.Select(record => record.SessionId)
            .Should().Equal(sessionId, sessionId);
        repository.Recorded.Select(record => record.Envelope.Topic)
            .Should().Equal("factory/1", "factory/2");
    }

    [Fact]
    public async Task Input_PreservesRecordOrder()
    {
        var repository = new FakeMessageRepository();
        var component = new MqttRecordingSinkComponent(repository, SessionId.New());

        component.Input.Post(Message("factory/1"));
        component.Input.Post(Message("factory/2"));
        component.Input.Post(Message("factory/3"));
        component.Complete();

        await component.Completion;

        repository.Recorded.Select(record => record.Envelope.Topic)
            .Should().Equal("factory/1", "factory/2", "factory/3");
    }

    [Fact]
    public async Task RecordFailure_PublishesErrorAndKeepsProcessing()
    {
        var repository = new FakeMessageRepository(topicToFail: "factory/fail");
        var component = new MqttRecordingSinkComponent(repository, SessionId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });

        component.Input.Post(Message("factory/ok-1"));
        component.Input.Post(Message("factory/fail"));
        component.Input.Post(Message("factory/ok-2"));
        component.Complete();

        await Task.WhenAll(component.Completion, errorSink.Completion);

        repository.Recorded.Select(record => record.Envelope.Topic)
            .Should().Equal("factory/ok-1", "factory/ok-2");

        var error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(FlowErrorCodes.ProcessingFailed);
        error.Context.Should().Be("factory/fail");
        error.NodeId.Should().Be(component.Id);
    }

    [Fact]
    public async Task Fault_PublishesErrorAndFaultsCompletion()
    {
        var component = new MqttRecordingSinkComponent(new FakeMessageRepository(), SessionId.New(), FlowNodeId.New());
        var errors = new List<FlowError>();
        var errorSink = new ActionBlock<FlowError>(errors.Add);
        var failure = new InvalidOperationException("recording sink failed");

        component.Errors.LinkTo(errorSink, new DataflowLinkOptions { PropagateCompletion = true });
        component.Fault(failure);

        var act = async () => await component.Completion;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("recording sink failed");
        await errorSink.Completion;

        errors.Should().ContainSingle().Which.Code.Should().Be(FlowErrorCodes.NodeFaulted);
    }

    private static MqttEnvelope Message(string topic, byte[]? payload = null) => new()
    {
        Topic = topic,
        Payload = payload ?? [],
        QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce,
        Retain = false
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
        public long CountBySession(SessionId sessionId) => Recorded.Count(record => record.SessionId == sessionId);
    }

    private sealed record RecordedMessage(SessionId SessionId, MqttEnvelope Envelope);
}
