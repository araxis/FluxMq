# Flow Components

Flow components are the building blocks users will eventually place on a Fork Flow canvas.

This page documents the current Dataflow-backed components and shows how each behaves.

## Shared Shape

Current flow nodes expose:

- typed node identity through `FlowNodeId`
- Dataflow lifecycle through `Complete`, `Fault`, and `Completion`
- an `Errors` output port for `FlowError`
- component-specific input and output ports

```mermaid
flowchart LR
    Input["Input Port"] --> Node["Flow Node"]
    Node --> Output["Output Port"]
    Node --> Errors["Errors Port"]
```

Not every component has an input port. Source and trigger components produce events.

## Connection State Trigger

`ConnectionStateTriggerComponent` converts connection manager state changes into flow events.

### Behavior

```mermaid
flowchart LR
    Manager["IMqttConnectionManager.StateChanged"] --> Trigger["ConnectionStateTriggerComponent"]
    Trigger --> State["Output: SessionStateChangedEventArgs"]
    Trigger --> Errors["Errors: FlowError"]
```

### Usage

```csharp
var trigger = new ConnectionStateTriggerComponent(connectionManager);

trigger.Output.LinkTo(stateSink, new DataflowLinkOptions
{
    PropagateCompletion = true
});
```

### Notes

- Use this when a flow should react to connection lifecycle events.
- Example downstream nodes: state router, notification sink, UI state projection.

## MQTT Message Source

`MqttMessageSourceComponent` converts an active MQTT session message channel into a flow source.

### Behavior

```mermaid
flowchart LR
    Session["IMqttSession.Messages"] --> Source["MqttMessageSourceComponent"]
    Source --> Out["Output: MqttEnvelope"]
    Source -->|reader failure| Errors["Errors: FlowError code 2000"]
```

### Usage

```csharp
var source = new MqttMessageSourceComponent(session);

source.Output.LinkTo(filter.Input, new DataflowLinkOptions
{
    PropagateCompletion = true
});

source.Errors.LinkTo(errorSink);

await source.StartAsync();
```

### Failure Behavior

If the session reader fails, the component publishes a `FlowError` and completes its output. The application host decides whether to reconnect, rebuild the flow, or surface the failure to the user.

## Topic Filter

`TopicFilterComponent` forwards only matching MQTT messages.

### Behavior

```mermaid
flowchart LR
    In["Input: MqttEnvelope"] --> Filter["TopicFilterComponent"]
    Filter -->|match| Out["Output: MqttEnvelope"]
    Filter -->|predicate failure| Errors["Errors: FlowError code 2000"]
```

### Usage

```csharp
var filter = TopicFilterComponent.Prefix("factory/");

source.LinkTo(filter.Input, new DataflowLinkOptions
{
    PropagateCompletion = true
});

filter.Output.LinkTo(next.Input, new DataflowLinkOptions
{
    PropagateCompletion = true
});
```

### Failure Behavior

If the predicate throws, the component publishes a `FlowError` and drops that message. Later messages continue processing.

## Payload Inspector Mapper

`PayloadInspectorMapperComponent` maps raw MQTT messages into inspected payload messages.

### Behavior

```mermaid
flowchart LR
    In["Input: MqttEnvelope"] --> Mapper["PayloadInspectorMapperComponent"]
    Mapper --> Out["Output: InspectedMqttMessage"]
    Mapper --> Errors["Errors: FlowError"]
```

### Usage

```csharp
var mapper = new PayloadInspectorMapperComponent();

filter.Output.LinkTo(mapper.Input, new DataflowLinkOptions
{
    PropagateCompletion = true
});

mapper.Output.LinkTo(uiSink, new DataflowLinkOptions
{
    PropagateCompletion = true
});
```

### Output

`InspectedMqttMessage` contains:

- original `MqttEnvelope`
- payload inspection result

## Replay Source

`ReplaySourceComponent` emits recorded MQTT messages in timestamp order.

### Behavior

```mermaid
flowchart LR
    Messages["IEnumerable<MqttEnvelope>"] --> Replay["ReplaySourceComponent"]
    Replay --> Out["Output: MqttEnvelope"]
    Replay --> Errors["Errors: FlowError"]
```

### Timing

```mermaid
sequenceDiagram
    participant Replay as ReplaySource
    participant Out as Output Port
    Replay->>Out: first message
    Note over Replay: wait scaled relative delay
    Replay->>Out: second message
    Note over Replay: wait scaled relative delay
    Replay->>Out: third message
```

### Usage

```csharp
var replay = new ReplaySourceComponent(messages, speed: 2);

replay.Output.LinkTo(next.Input, new DataflowLinkOptions
{
    PropagateCompletion = true
});

await replay.StartAsync();
```

### Notes

- `speed = 1` preserves original relative timing.
- `speed = 2` replays twice as fast.
- delay behavior is injectable for deterministic tests.

## MQTT Publish Sink

`MqttPublishSinkComponent` publishes incoming MQTT messages through an active session.

### Behavior

```mermaid
flowchart LR
    In["Input: MqttEnvelope"] --> Sink["MqttPublishSinkComponent"]
    Sink --> Session["IMqttSession.PublishAsync"]
    Sink -->|publish failure| Errors["Errors: FlowError code 2000"]
```

### Usage

```csharp
var publishSink = new MqttPublishSinkComponent(session);

source.LinkTo(publishSink.Input, new DataflowLinkOptions
{
    PropagateCompletion = true
});

publishSink.Errors.LinkTo(errorSink);
```

### Failure Behavior

If publishing fails for one message, the sink publishes a `FlowError` with the topic in `Context` and continues processing later messages.

The component preserves publish order by default. Higher parallelism is available through the constructor, but ordered single-message publishing should remain the default for replay and deterministic flow behavior.

## Recorded Session Replay Factory

`RecordedSessionReplayFactory` creates replay sources from stored sessions.

This is not a flow node. It is an orchestration service.

### Behavior

```mermaid
flowchart LR
    SessionId["SessionId"] --> Factory["RecordedSessionReplayFactory"]
    Repository["IMessageRepository"] --> Factory
    Factory --> Convert["StoredMessage.ToEnvelope"]
    Convert --> Replay["ReplaySourceComponent"]
```

### Usage

```csharp
var factory = new RecordedSessionReplayFactory(messageRepository);
var replay = factory.Create(sessionId, new RecordedSessionReplayOptions
{
    Speed = 2
});
```

## Sample Flow

This flow reads a live MQTT session, filters it, inspects payloads, and projects results to UI.

```mermaid
flowchart LR
    Source["MqttMessageSourceComponent"] --> Filter["TopicFilterComponent"]
    Filter --> Mapper["PayloadInspectorMapperComponent"]
    Mapper --> Ui["Payload UI Sink"]
    Source --> ErrorLog["Error Log"]
    Filter --> ErrorLog
    Mapper --> ErrorLog
```

Equivalent code shape:

```csharp
var source = new MqttMessageSourceComponent(session);
var filter = TopicFilterComponent.Prefix("factory/");
var mapper = new PayloadInspectorMapperComponent();

source.Output.LinkTo(filter.Input, new DataflowLinkOptions { PropagateCompletion = true });
filter.Output.LinkTo(mapper.Input, new DataflowLinkOptions { PropagateCompletion = true });
mapper.Output.LinkTo(payloadUiSink, new DataflowLinkOptions { PropagateCompletion = true });

source.Errors.LinkTo(errorSink);
filter.Errors.LinkTo(errorSink);
mapper.Errors.LinkTo(errorSink);

await source.StartAsync();
```

This flow replays a recorded session back through an MQTT session.

```mermaid
flowchart LR
    Factory["RecordedSessionReplayFactory"] --> Replay["ReplaySourceComponent"]
    Replay --> Filter["TopicFilterComponent"]
    Filter --> Publish["MqttPublishSinkComponent"]
    Replay --> ErrorLog["Error Log"]
    Filter --> ErrorLog
    Publish --> ErrorLog
```

Equivalent code shape:

```csharp
var replay = replayFactory.Create(sessionId);
var filter = TopicFilterComponent.Prefix("factory/");
var publishSink = new MqttPublishSinkComponent(session);

replay.Output.LinkTo(filter.Input, new DataflowLinkOptions { PropagateCompletion = true });
filter.Output.LinkTo(publishSink.Input, new DataflowLinkOptions { PropagateCompletion = true });

replay.Errors.LinkTo(errorSink);
filter.Errors.LinkTo(errorSink);
publishSink.Errors.LinkTo(errorSink);

await replay.StartAsync();
await publishSink.Completion;
```

## Future Component Types

Likely near-term additions:

- dynamic expression mapper
- JSONata mapper
- condition/router component
- storage sink
- metrics sink

Dynamic expression components should use `FlowError.Code` for routing and diagnostics instead of relying on exception message text.
