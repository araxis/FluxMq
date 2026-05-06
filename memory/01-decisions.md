# FluxMQ Decisions

This file records project decisions so they do not get lost across sessions.

## Accepted Decisions

### 2026-05-06 - Use LiteDB for local storage

Decision: Use LiteDB as the first local database.

Reasoning:
- FluxMQ is starting as a local-first desktop debugging and observability tool.
- LiteDB fits embedded desktop storage without requiring a separate database service.
- It is a good match for connection profiles, recorded sessions, replay metadata, payload indexes, app settings, and lightweight metrics.
- SQLite can remain an option later if relational querying, multi-process access, or heavier analytics become necessary.

Status: Accepted.

### 2026-05-06 - Build core first, formal plugin runtime later

Decision: Do not make external plugins the foundation of the MVP. Build stable internal modules first, then expose proven contracts through a plugin runtime.

Reasoning:
- The proposal's plugin direction is strong, but plugin APIs are expensive to change once externalized.
- Payload inspection, observability, and replay should first exist as internal modules.
- Once those module boundaries feel right, they can become plugin contracts.

Status: Accepted.

### 2026-05-06 - Message/session pipeline is the architectural spine

Decision: Center the architecture around MQTT sessions, message ingestion, processing, storage, and UI projection.

Reasoning:
- FluxMQ's real value comes from high-throughput debugging and replay, not just UI panels.
- The app needs a clean flow from MQTTnet into channels, processing, storage, metrics, and UI state.

Status: Accepted.

### 2026-05-06 - Keep project memory in Markdown

Decision: Use a dedicated `memory` folder with Markdown files for decisions, steps, progress, and architecture notes.

Reasoning:
- Keeps planning visible and versionable.
- Makes it easy to resume work without reconstructing context.

Status: Accepted.

### 2026-05-06 - Start with Windows-only MAUI target

Decision: Target `net10.0-windows10.0.19041.0` for the first MAUI Blazor Hybrid scaffold.

Reasoning:
- The first development environment is Windows desktop.
- The MAUI template generated mobile and Mac targets, but the available workload set did not cleanly restore all generated targets.
- Keeping the first target Windows-only makes the scaffold buildable immediately.
- Cross-platform targets can be reintroduced after the Windows desktop core is useful.

Status: Accepted.

### 2026-05-06 - Use classic `.sln` instead of `.slnx`

Decision: Use a classic Visual Studio `.sln` file.

Reasoning:
- The .NET 11 preview CLI generated an empty `.slnx` and reported successful project additions that did not persist.
- A classic `.sln` restored, built, and tested reliably.

Status: Accepted.

### 2026-05-06 - Support both dark and light themes

Decision: FluxMQ will support both dark and light UI themes. Neither is the canonical mode.

Reasoning:
- Theme support (dark and light) is a baseline expectation for a desktop app, not a differentiating design choice.
- Calling it out in copy or docs implies it is optional or notable, which it is not.
- The IDE-like, operational character of the UI is the relevant design statement, not the color scheme.

Status: Accepted.

### 2026-05-06 - Use a static mockup image as the README top banner

Decision: Use `design/ui-mockups/01-main-workspace.png` as the static banner at the top of the README. The animated GIF remains in the Visual Direction / Intro Animation section below.

Reasoning:
- A static image loads instantly and is always visible on any Markdown renderer.
- The GIF adds value lower in the page where motion is appropriate, but a banner should be immediate.
- Keeping both means first-time readers get a quick visual impression, then an animated walkthrough further down.

Status: Accepted.
