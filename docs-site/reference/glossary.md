# Glossary

## Broker

The MQTT server that receives published messages and distributes them to subscribers.

## Connection Profile

Saved broker connection settings.

## Condition Router

A workflow component that sends each incoming value to one of two branches.

## Fork Flow

A configurable FluxMQ pipeline made from sources, triggers, filters, mappers, routers, observers, and actors.

## Flow Application Definition

A configuration model that describes one runnable FluxMQ application: resources, workflows, dashboards, tests, node properties, and receiving-port links before runtime graphs are built.

## Flow Application Runtime

The host-independent runtime layer that loads executable resources and workflows, owns shared resources, starts workflows, and supervises lifecycle and errors.

## Flow Components

The concrete MQTT, replay, storage, filtering, mapping, routing, publishing, recording, metrics, HTTP, file, state, and validation nodes registered into the runtime by a host.

## Runtime Builder

The cold-start builder that validates a flow application definition, creates registered runtime nodes, links compatible typed ports, and returns build errors when the graph cannot be constructed.

## Runtime Node Factory

A registered constructor that turns a node definition into a runtime node with typed input and output ports.

## MQTT Envelope

The runtime shape for an MQTT message and its metadata.

## MQTT Trigger

A workflow node that subscribes to topics through an app-level broker connection and emits `MqttEnvelope` values.

## MQTT Metrics

A workflow component that tracks counters from MQTT messages and publishes metric snapshots.

## Recording Session

A stored sequence of MQTT messages captured during a debugging session.

## MQTT Recorder

A workflow component that stores incoming MQTT messages for a recording session.

## Replay Source

A flow component that emits recorded MQTT messages in timestamp order.

## Actor

A workflow endpoint that writes, publishes, stores, or calls an external system from incoming data.
