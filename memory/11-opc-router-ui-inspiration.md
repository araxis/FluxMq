---
name: FluxMQ OPC Router UI inspiration
description: Industrial ETL/integration UI ideas FluxMQ can learn from without copying.
type: project
---
# OPC Router UI Inspiration

OPC Router is a useful reference point for the ETL/integration era because it speaks the language of industrial data movement: plug-ins, triggers, transfer objects, visual connections, monitoring, and schema-aware JSON handling.

This is inspiration, not a product clone. FluxMQ should keep its own runtime vocabulary: sources, filters, routers, dynamic mappers, actors, observers, validators, assertions, and scenarios.

## Observed Reference Ideas

Sources:

- OPC Router introduction: <https://docs.opc-router.com/>
- OPC Router modular functionality summary: <https://softwaretoolbox.com/opc-router/modular-functionality>
- OPC Router JSON/JPath plug-in page: <https://www.opc-router.com/p020-json-plug-in-opc-router_en/>
- Siemens OPC Router overview: <https://www.siemens.com/en-us/products/inray-industriesoftware-opc-router-standard-edition/>

Useful ideas:

- A no-code visual interface with drag-and-drop configuration for integration workflows.
- Plug-ins group system-specific capabilities without exposing all low-level details first.
- Transfer objects abstract external systems into input/output blocks that users connect visually.
- Triggers are first-class workflow starters.
- JSON tools expose readable structure and selectable fields.
- JSON schema/OpenAPI/AsyncAPI-style imports can generate inputs and outputs instead of making users hand-copy payload shapes.
- Documentation/help is close to the object being configured.

## FluxMQ Translation

Do not rename FluxMQ concepts to OPC Router concepts, but learn from the mental model:

- OPC Router plug-in -> FluxMQ component/module pack.
- OPC Router transfer object -> FluxMQ node with typed ports and schema-backed configuration.
- OPC Router trigger -> FluxMQ source/trigger component.
- OPC Router visual connection -> FluxMQ typed diagram link.
- OPC Router JSON read/write -> FluxMQ JSONata mapper and JSON Schema validator.

## JSONata Mapper Workbench Direction

The JSONata mapper UI should become a structured workbench, not just a textarea.

Layout idea:

- Left: input sample tree from the selected upstream message, with common variables:
  - `topic`
  - `payloadText`
  - parsed JSON payload when available
  - QoS/retain/receivedAt metadata
- Middle: expression editor and mapping fields.
- Right: output request shape, based on selected `outputType`:
  - `MqttPublishRequest`
  - `MqttRecordingRequest`
  - `FileWriteRequest`
  - later `HttpRequest`, `EmailSendRequest`, assertion inputs, metric observations
- Bottom or side drawer: live preview/test result and errors.

Expected controls:

- Engine selector: Dynamic Expresso / JSONata.
- Fixed `MqttEnvelope` input sample editor.
- Result contract selector: `Any`, `Typed`, or `JSON Schema file`.
- Typed output target selector when the contract is `Typed`.
- Single object-expression editor.
- Insert-field action from the input tree into the active expression.
- Preview with the selected/recent/sample message.
- Validation messages for missing required output fields.
- Schema/source selector for validator and schema-backed contract flows.

The goal is to make mapping approachable for ops users while still powerful for developers.

## Design Rules For FluxMQ

- The graph must show the mapper explicitly. Do not auto-insert hidden request mappers.
- Actors should still consume typed command/request objects.
- Mapper configuration should be serializable in the flow definition JSON.
- Runtime evaluation and UI preview should use the same mapper engine path where practical.
- Schema import should help generate fields and validation hints, not hide the actual data contract.
