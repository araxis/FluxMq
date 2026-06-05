# Component Boundary Refactor

FluxMQ now treats component catalog data as an adapter surface over FluxFlow
component design metadata. The immediate slice keeps the existing FluxMQ node
ids and default configurations intact, while moving the reusable metadata
contract to the package side.

## Current Boundary

FluxFlow owns reusable component design metadata:

- component type
- display name, category, summary, and icon key
- preferred node name
- option metadata and defaults
- port metadata and value type hints

FluxMQ owns product aliases and workspace behavior:

- app document resources, workflows, dashboards, and tests
- compatibility ids such as `mqtt.trigger`, `mqtt.publisher`, `session.source`,
  and `replay.source`
- app-specific default configuration and connection/resource resolution
- dashboard and test studio UI
- local LiteDB persistence and run history

## FluxMQ Catalog Adapter

`FluxMqComponentCatalogAdapter` composes FluxMQ alias metadata into a
`ComponentDesignMetadataCatalog`, then maps it back to the UI descriptor shape
used by the palette, composer, and diagram surface.

`FlowComponentCatalog` now reads through the adapter. `FlowDefinitionComposer`
uses the adapter for component metadata and default configuration, so future
package-provided metadata can replace the compatibility data source without
rewiring the UI.

FluxMQ now composes package-owned metadata providers for reusable HTTP, mapping,
payload, routing, serialization, state, storage, and timer components. Product
aliases still win when the same component id is app-specific.

Serialization transform descriptors were removed from the local FluxMQ static
registry. They are now package-backed metadata entries with FluxMQ behavior
overrides for the historical preferred node names and default capacity.

## Runtime Composition

`RegisterPipelineComponentFactories` now registers reusable package components
first and FluxMQ runtime adapters second. This keeps the composition root clear:

- package modules provide reusable runtime nodes
- FluxMQ adapters preserve product node ids, app resources, local repositories,
  and event projection behavior

Existing saved app JSON remains compatible.

## Verification

- `dotnet test tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj`
- `dotnet test FluxMq.sln --no-restore`
