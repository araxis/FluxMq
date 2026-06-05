# Component Boundary Refactor

Date: 2026-06-05

## Completed Slice

- Added a FluxMQ catalog adapter layer:
  - `IFluxMqComponentCatalog`
  - `FluxMqComponentAliasRegistry`
  - `FluxMqComponentCatalogAdapter`
- `FlowComponentCatalog` now reads descriptors from the adapter.
- `FlowDefinitionComposer` now resolves component metadata and default
  configuration through the adapter.
- `RegisterPipelineComponentFactories` now clearly registers FluxFlow package
  components before FluxMQ runtime adapters and compatibility aliases.
- FluxMQ consumes `FluxFlow.Components.Designer` through a project reference for
  the metadata contracts.
- FluxMQ now composes package metadata providers for reusable HTTP, mapping,
  payload, routing, serialization, state, storage, and timer components.
- Removed duplicate FluxMQ serialization transform descriptors from the local
  metadata registry; FluxMQ keeps only package-backed behavior overrides for
  preferred node names, no-auto-link behavior, and the historical default
  capacity.

## Boundary Decision

FluxMQ keeps app-specific aliases, saved JSON compatibility, local persistence,
dashboard/test artifacts, and UI workflow behavior. FluxFlow owns reusable
component metadata contracts and package-owned runtime component metadata.

The static FluxMQ metadata registry is no longer the source for the migrated
package-backed serialization transforms. It remains as a compatibility alias
source for product-specific node ids and app-owned behavior.

## Verification

- `dotnet test D:\Projects\FluxFlow\FluxFlow.sln --no-restore`
- `dotnet test D:\Projects\FluxMq\FluxMq.sln --no-restore`
- `dotnet test D:\Projects\FluxMq\tests\FluxMq.UI.Tests\FluxMq.UI.Tests.csproj`
