using FluxMq.Core.Ids;
using FluxMq.Core.Mqtt;
using FluxMq.Components.Logging;
using FluxMq.Components.Mqtt;
using FluxFlow.Components.Mqtt;
using FluxFlow.Components.Mqtt.Contracts;
using FluxFlow.Components.Mqtt.Nodes;
using FluxFlow.Engine.Components;
using FluxFlow.Engine.Definitions;
using FluxFlow.Engine.Runtime;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;

namespace FluxMq.Components.MqttPublisher;

using ComponentMqttPublishRequest = FluxFlow.Components.Mqtt.Contracts.MqttPublishRequest;
using ComponentMqttPublishResult = FluxFlow.Components.Mqtt.Contracts.MqttPublishResult;

public sealed class MqttPublisherComponent : IFlowNode, IFlowEventSource, IAsyncDisposable
{
    private readonly MqttPublishNode _publisher;
    private readonly RuntimeNode _publisherRuntime;
    private readonly ActionBlock<MqttPublishRequest> _input;
    private readonly ActionBlock<ComponentMqttPublishResult> _resultSink;
    private readonly IDisposable _resultLink;
    private readonly BroadcastBlock<FlowError> _errors;
    private readonly BufferBlock<FlowLogEntry> _entries;
    private readonly BufferBlock<FlowEvent> _events;
    private readonly ConcurrentDictionary<string, MqttPublishRequest> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly Task _completion;
    private readonly Task _errorPump;
    private int _publishedCount;
    private int _started;
    private string? _lastPublishedTopic;

    public MqttPublisherComponent(
        IMqttBrokerClient client,
        FlowNodeId? id = null,
        int boundedCapacity = 1000,
        int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Degree of parallelism must be positive.");
        }

        Id = id ?? FlowNodeId.New();
        _publisherRuntime = CreatePublisherNode(client, boundedCapacity);
        _publisher = (MqttPublishNode)_publisherRuntime.Node;
        _errors = new BroadcastBlock<FlowError>(static error => error);
        _entries = new BufferBlock<FlowLogEntry>();
        _events = new BufferBlock<FlowEvent>();
        _resultSink = new ActionBlock<ComponentMqttPublishResult>(
            result => PublishSuccess(result, TryTakePending(result.CorrelationId)),
            new ExecutionDataflowBlockOptions { BoundedCapacity = boundedCapacity });
        _resultLink = LinkResultSink(_publisherRuntime, _resultSink);
        _input = new ActionBlock<MqttPublishRequest>(
            PublishAsync,
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = boundedCapacity,
                EnsureOrdered = true,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            });

        _errorPump = PumpErrorsAsync();
        _completion = CompleteWhenInnerNodeStopsAsync();
    }

    public FlowNodeId Id { get; }
    public ISourceBlock<FlowError> Errors => _errors;
    public ISourceBlock<FlowLogEntry> Entries => _entries;
    public ISourceBlock<FlowEvent> Events => _events;
    public Task Completion => _completion;
    public ITargetBlock<MqttPublishRequest> Input => _input;
    public int PublishedCount => Volatile.Read(ref _publishedCount);
    public string? LastPublishedTopic => _lastPublishedTopic;

    public Task StartAsync(CancellationToken cancellationToken = default)
        => EnsureStartedAsync(cancellationToken);

    public void Complete()
        => _input.Complete();

    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        PublishError(FlowErrorCodes.NodeFaulted, "MQTT publisher faulted.", exception);
        ((IDataflowBlock)_input).Fault(exception);
        _publisher.Fault(exception);
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _resultLink.Dispose();
        foreach (var output in _publisherRuntime.Outputs)
        {
            await output.DisposeAsync().ConfigureAwait(false);
        }

        await _publisher.DisposeAsync().ConfigureAwait(false);
        _startLock.Dispose();
    }

    private static RuntimeNode CreatePublisherNode(
        IMqttBrokerClient client,
        int boundedCapacity)
    {
        var definition = new NodeDefinition
        {
            Type = MqttComponentTypes.Publish,
            Configuration = new Dictionary<string, JsonElement>
            {
                ["boundedCapacity"] = JsonSerializer.SerializeToElement(boundedCapacity)
            }
        };

        var context = new RuntimeNodeFactoryContext(
            new NodeName("publisher"),
            definition,
            "mqtt",
            new Dictionary<NodeName, RuntimeNode>());
        return MqttPublishNode.Create(
            context,
            new MqttClientAdapterFactory(() => new MqttClientAdapter(client)));
    }

    private static IDisposable LinkResultSink(
        RuntimeNode runtimeNode,
        ActionBlock<ComponentMqttPublishResult> resultSink)
    {
        var resultOutput = runtimeNode.FindOutput(new PortName(MqttComponentPorts.Result))
            ?? throw new InvalidOperationException("MQTT publish package node did not expose a Result output.");
        var resultInput = new InputPort<ComponentMqttPublishResult>(
            new PortAddress("component", new NodeName("publisher"), new PortName("ResultSink")),
            resultSink);

        var link = resultOutput.TryLinkTo(resultInput, propagateCompletion: true, out var error);
        return link ?? throw new InvalidOperationException(error?.Message ?? "MQTT publish package output could not be linked.");
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _started) == 1)
        {
            return;
        }

        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started == 0)
            {
                await _publisher.StartAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _started, 1);
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task PublishAsync(MqttPublishRequest request)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        _pending[correlationId] = request;

        try
        {
            await EnsureStartedAsync().ConfigureAwait(false);

            var accepted = await _publisher.Input.SendAsync(
                    ToComponentRequest(request, correlationId))
                .ConfigureAwait(false);

            if (!accepted)
            {
                _pending.TryRemove(correlationId, out _);
                PublishError(
                    FlowErrorCodes.ProcessingFailed,
                    "MQTT publish failed.",
                    new InvalidOperationException("MQTT publisher input is closed."),
                    request.Topic);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _pending.TryRemove(correlationId, out _);
            PublishError(FlowErrorCodes.ProcessingFailed, "MQTT publish failed.", exception, request.Topic);
        }
    }

    private async Task PumpErrorsAsync()
    {
        var errorSource = (IReceivableSourceBlock<FlowError>)_publisher.Errors;
        while (await _publisher.Errors.OutputAvailableAsync().ConfigureAwait(false))
        {
            while (errorSource.TryReceive(out var error))
            {
                PublishMappedError(error);
            }
        }
    }

    private async Task CompleteWhenInnerNodeStopsAsync()
    {
        try
        {
            await _input.Completion.ConfigureAwait(false);
            _publisher.Complete();
            await _publisher.Completion.ConfigureAwait(false);
            await Task.WhenAll(_resultSink.Completion, _errorPump).ConfigureAwait(false);

            _entries.Complete();
            _events.Complete();
            _errors.Complete();
        }
        catch (Exception)
        {
            _entries.Complete();
            _events.Complete();
            _errors.Complete();
            throw;
        }
    }

    private static ComponentMqttPublishRequest ToComponentRequest(
        MqttPublishRequest request,
        string correlationId)
        => new()
        {
            Topic = request.Topic,
            Payload = request.Payload,
            QualityOfService = MqttClientAdapter.ToComponentQualityOfService(request.QualityOfService),
            Retain = request.Retain,
            CorrelationId = correlationId
        };

    private MqttPublishRequest? TryTakePending(string? correlationId)
        => correlationId is null
            ? null
            : _pending.TryRemove(correlationId, out var request)
                ? request
                : null;

    private MqttPublishRequest? TryTakePending(MqttClientAdapterException exception)
    {
        if (exception.CorrelationId is not null && _pending.TryRemove(exception.CorrelationId, out var request))
        {
            return request;
        }

        var match = _pending.FirstOrDefault(pair =>
            string.Equals(pair.Value.Topic, exception.Topic, StringComparison.Ordinal));
        return match.Key is null ? null : _pending.TryRemove(match.Key, out request) ? request : null;
    }

    private void PublishSuccess(ComponentMqttPublishResult result, MqttPublishRequest? request)
    {
        Interlocked.Increment(ref _publishedCount);
        _lastPublishedTopic = result.Topic;
        _entries.Post(new FlowLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Severity = FlowLogSeverity.Info,
            Source = "MqttPublisher",
            Message = $"Published MQTT message to '{result.Topic}'.",
            RelatedNodeId = Id,
            Topic = result.Topic,
            PayloadBytes = result.PayloadBytes,
            Context = $"qos={(int)ToBrokerQualityOfService(result.QualityOfService)}; retain={result.Retain}"
        });
        _events.Post(new FlowEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            Type = FluxMqEventTypes.MqttMessagePublished,
            Source = "MqttPublisher",
            SourceNodeId = Id,
            Subject = result.Topic,
            Status = "published",
            Channel = result.Topic,
            PayloadBytes = result.PayloadBytes,
            PayloadPreview = request is null ? null : FlowEventPayloadPreview.FromBytes(request.Payload),
            Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["qos"] = ((int)ToBrokerQualityOfService(result.QualityOfService)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["retain"] = result.Retain.ToString()
            }
        });
    }

    private void PublishMappedError(FlowError error)
    {
        var request = error.Exception is MqttClientAdapterException adapterException
            ? TryTakePending(adapterException)
            : null;
        var context = request?.Topic ??
            (error.Exception as MqttClientAdapterException)?.Topic ??
            error.Context;

        PublishError(
            FlowErrorCodes.ProcessingFailed,
            "MQTT publish failed.",
            error.Exception ?? new InvalidOperationException(error.Message),
            context);
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

    private static MqttQualityOfServiceLevel ToBrokerQualityOfService(MqttQualityOfService qualityOfService)
        => MqttClientAdapter.ToBrokerQualityOfService(qualityOfService);
}
