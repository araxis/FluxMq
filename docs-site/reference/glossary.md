# Glossary

## Broker

The MQTT server that receives published messages and distributes them to subscribers.

## Connection Profile

Saved broker connection settings.

## Condition Router

A flow component that sends each incoming message to one of two branches.

## Fork Flow

A configurable FluxMQ pipeline made from sources, triggers, filters, mappers, routers, and sinks.

## Flow Application Definition

A configuration model that describes one runnable FluxMQ application: shared resources, named workflows, node properties, and receiving-port links before runtime graphs are built.

## Flow Application Runtime

The future host-independent runtime layer that loads a flow application definition, owns shared resources, starts workflows, coordinates reloads, and supervises lifecycle and errors.

## Flow Components

The concrete MQTT, replay, storage, filtering, mapping, routing, publishing, recording, and metrics nodes registered into the runtime by a host.

## Runtime Builder

The cold-start builder that validates a flow application definition, creates registered runtime nodes, links compatible typed ports, and returns build errors when the graph cannot be constructed.

## Runtime Node Factory

A registered constructor that turns a node definition into a runtime node with typed input and output ports.

## MQTT Envelope

The FluxMQ runtime shape for an MQTT message and its metadata.

## Traffic Source

A logical flow source that can bind to live MQTT traffic, a stored recording session, or generated messages while exposing the same `MqttEnvelope` output.

## Metrics Sink

A flow component that tracks counters from MQTT messages and publishes metric snapshots.

## OpenTelemetry

A planned optional integration for exporting selected FluxMQ metrics, traces, and diagnostic events to external observability tools.

## Recording Session

A stored sequence of MQTT messages captured during a debugging session.

## Recording Sink

A flow component that stores incoming MQTT messages for a recording session.

## Replay Source

A flow component that emits recorded MQTT messages in timestamp order.

## Sink

A flow endpoint that writes, publishes, stores, or projects incoming data.
