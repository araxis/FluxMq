using FluxMq.Core.Ids;
using FluxMq.Core.Models;
using FluxMq.Core.Mqtt;
using FluxMq.Components.Mqtt;
using FluxFlow.Components.Mqtt;
using FluxFlow.Components.Mqtt.Contracts;
using FluxFlow.Components.Mqtt.Nodes;
using FluxFlow.Components.Mqtt.Timing;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using MQTTnet.Protocol;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MessageSource;

using ComponentMqttReceivedMessage = FluxFlow.Components.Mqtt.Contracts.MqttReceivedMessage;

public sealed class MqttTriggerComponent : IFlowNode, IFlowEventSource, IAsyncDisposable
{
    private readonly IMqttBrokerClient _client;
    private readonly ISourceBlock<MqttEnvelope> _connectionStream;
    private readonly IReadOnlyList<MqttSubscription> _subscriptions;
    private readonly SubscriberRuntime[] _subscribers;
    private readonly BufferBlock<MqttEnvelope> _output;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowEvent> _events;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _completion;
    private int _started;
    private int _stopped;

    public MqttTriggerComponent(
        IMqttBrokerClient client,
        ISourceBlock<MqttEnvelope> connectionStream,
        IEnumerable<MqttSubscription> subscriptions,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
    {
        Id = id ?? FlowNodeId.New();
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _connectionStream = connectionStream ?? throw new ArgumentNullException(nameof(connectionStream));
        _subscriptions = subscriptions?.ToArray() ?? throw new ArgumentNullException(nameof(subscriptions));
        if (_subscriptions.Count == 0)
        {
            throw new ArgumentException("A trigger must declare at least one subscription.", nameof(subscriptions));
        }

        _errors = new BroadcastBlock<FlowError>(static error => error);
        _events = new BufferBlock<FlowEvent>();
        _output = new BufferBlock<MqttEnvelope>(new DataflowBlockOptions
        {
            BoundedCapacity = boundedCapacity
        });
        _subscribers = _subscriptions
            .Select(subscription => CreateSubscriberNode(
                _client,
                _connectionStream,
                subscription,
                boundedCapacity))
            .ToArray();
        _completion = CompleteWhenSubscribersStopAsync();
    }

    public MqttTriggerComponent(
        MqttConnectionComponent connection,
        IEnumerable<MqttSubscription> subscriptions,
        FlowNodeId? id = null,
        int boundedCapacity = 1000)
        : this(
            (connection ?? throw new ArgumentNullException(nameof(connection))).Client,
            connection.Messages,
            subscriptions,
            id,
            boundedCapacity)
    {
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public ISourceBlock<FlowEvent> Events => _events;
    public Task Completion => _output.Completion;
    public ISourceBlock<MqttEnvelope> Output => _output;
    public IReadOnlyList<MqttSubscription> Subscriptions => _subscriptions;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        try
        {
            foreach (var subscriber in _subscribers)
            {
                await subscriber.Node.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            PublishError(FlowErrorCodes.NodeFaulted, "MQTT trigger failed to start.", exception, _client.Profile.Name);
            StopSubscribers();
            throw;
        }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
        {
            return;
        }

        StopSubscribers();
    }

    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Interlocked.Exchange(ref _stopped, 1);
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT trigger faulted.", exception, _client.Profile.Name);
        _cts.Cancel();
        foreach (var subscriber in _subscribers)
        {
            subscriber.Node.Fault(exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        await _completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        foreach (var subscriber in _subscribers)
        {
            subscriber.OutputLink.Dispose();
            foreach (var output in subscriber.RuntimeNode.Outputs)
            {
                await output.DisposeAsync().ConfigureAwait(false);
            }

            await subscriber.Node.DisposeAsync().ConfigureAwait(false);
        }

        _cts.Dispose();
    }

    private SubscriberRuntime CreateSubscriberNode(
        IMqttBrokerClient client,
        ISourceBlock<MqttEnvelope> connectionStream,
        MqttSubscription subscription,
        int boundedCapacity)
    {
        var definition = new NodeDefinition
        {
            Type = MqttComponentTypes.Subscribe,
            Configuration = new Dictionary<string, JsonElement>
            {
                ["topicFilter"] = JsonSerializer.SerializeToElement(subscription.TopicFilter),
                ["qualityOfService"] = JsonSerializer.SerializeToElement(MqttClientAdapter.ToComponentQualityOfService(subscription.QualityOfService).ToString()),
                ["receiveRetainedMessages"] = JsonSerializer.SerializeToElement(subscription.ReceiveRetainedMessages),
                ["retainAsPublished"] = JsonSerializer.SerializeToElement(subscription.RetainAsPublished),
                ["boundedCapacity"] = JsonSerializer.SerializeToElement(boundedCapacity)
            }
        };

        var context = new RuntimeNodeFactoryContext(
            new NodeName("trigger"),
            definition,
            "mqtt",
            new Dictionary<NodeName, RuntimeNode>());
        var runtimeNode = MqttSubscribeNode.Create(
            context,
            new MqttClientAdapterFactory(() => new MqttClientAdapter(client, connectionStream)),
            SystemMqttClock.Instance);
        var outputSink = new ActionBlock<ComponentMqttReceivedMessage>(
            HandleReceivedMessageAsync,
            new ExecutionDataflowBlockOptions { BoundedCapacity = boundedCapacity });
        var outputLink = LinkOutputSink(runtimeNode, outputSink);
        var subscriber = (MqttSubscribeNode)runtimeNode.Node;

        return new SubscriberRuntime(
            subscriber,
            runtimeNode,
            outputSink,
            outputLink,
            PumpErrorsAsync(subscriber));
    }

    private static IDisposable LinkOutputSink(
        RuntimeNode runtimeNode,
        ActionBlock<ComponentMqttReceivedMessage> outputSink)
    {
        var output = runtimeNode.FindOutput(new PortName(MqttComponentPorts.Output))
            ?? throw new InvalidOperationException("MQTT subscribe package node did not expose an Output port.");
        var input = new InputPort<ComponentMqttReceivedMessage>(
            new PortAddress("component", new NodeName("trigger"), new PortName("OutputSink")),
            outputSink);

        var link = output.TryLinkTo(input, propagateCompletion: true, out var error);
        return link ?? throw new InvalidOperationException(error?.Message ?? "MQTT subscribe package output could not be linked.");
    }

    private async Task HandleReceivedMessageAsync(ComponentMqttReceivedMessage message)
    {
        var envelope = MqttClientAdapter.ToEnvelope(message);
        await _events.SendAsync(CreateReceivedEvent(envelope), _cts.Token).ConfigureAwait(false);
        await _output.SendAsync(envelope, _cts.Token).ConfigureAwait(false);
    }

    private async Task PumpErrorsAsync(MqttSubscribeNode subscriber)
    {
        var source = (IReceivableSourceBlock<FlowError>)subscriber.Errors;

        while (await subscriber.Errors.OutputAvailableAsync().ConfigureAwait(false))
        {
            while (source.TryReceive(out var error))
            {
                PublishMappedError(error);
            }
        }
    }

    private async Task CompleteWhenSubscribersStopAsync()
    {
        try
        {
            await Task.WhenAll(_subscribers.Select(subscriber => subscriber.OutputSink.Completion)).ConfigureAwait(false);
            _output.Complete();
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            _output.Complete();
        }
        catch (Exception exception)
        {
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT trigger failed.", exception, _client.Profile.Name);
            ((IDataflowBlock)_output).Fault(exception);
        }
        finally
        {
            await _output.Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            foreach (var subscriber in _subscribers)
            {
                subscriber.Node.Complete();
            }

            await Task.WhenAll(_subscribers.Select(subscriber => subscriber.ErrorPump)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _errors.Complete();
            _events.Complete();
        }
    }

    private void StopSubscribers()
    {
        _cts.Cancel();
        foreach (var subscriber in _subscribers)
        {
            subscriber.Node.Complete();
        }
    }

    private void PublishMappedError(FlowError error)
    {
        var context = error.Exception is MqttClientAdapterException adapterException
            ? adapterException.Topic
            : error.Context;
        PublishError(
            FlowErrorCodes.ProcessingFailed,
            "MQTT trigger failed.",
            error.Exception ?? new InvalidOperationException(error.Message),
            context ?? _client.Profile.Name);
    }

    private void PublishError(int code, string message, Exception exception, string? context = null)
    {
        _errors.Post(new FlowError
        {
            NodeId = Id,
            Code = code,
            Message = message,
            Exception = exception,
            Context = context
        });
    }

    private FlowEvent CreateReceivedEvent(MqttEnvelope envelope)
        => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FluxMqEventTypes.MqttMessageReceived,
            Source = "MqttTrigger",
            SourceNodeId = Id,
            Subject = envelope.Topic,
            Status = "received",
            Channel = envelope.Topic,
            PayloadBytes = envelope.Payload.Length,
            PayloadPreview = FlowEventPayloadPreview.FromBytes(envelope.Payload),
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["qos"] = ((int)envelope.QualityOfService).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["retain"] = envelope.Retain.ToString()
            }
        };

    private sealed record SubscriberRuntime(
        MqttSubscribeNode Node,
        RuntimeNode RuntimeNode,
        ActionBlock<ComponentMqttReceivedMessage> OutputSink,
        IDisposable OutputLink,
        Task ErrorPump);
}
