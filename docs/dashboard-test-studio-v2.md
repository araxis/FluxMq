# Dashboard And Test Studio V2

FluxMQ dashboard and test artifacts now use a framework-first V2 shape. Older flat dashboard and test JSON is migrated on load, and saves keep the V2 metadata required by the studio surfaces.

## Dashboards

Dashboard V2 artifacts contain:

- `version`: current schema version.
- `layout`: structured grid source of truth.
- `responsive`: breakpoint metadata for desktop, tablet, and mobile layouts.
- `metrics`: reusable named metric/query definitions.
- `widgets`: widget instances with dedicated widget types.
- `bindings`: widget-to-metric bindings.
- `view`: authoring/runtime view metadata.

The first widget pack is MQTT operations focused: KPI tile, status strip, rate tile, line/area/bar/donut chart types, event table, latest event, topic tree, topic heatmap, payload size distribution, and QoS/retain breakdown. Rich charts are routed through FluxMQ-owned chart adapter interfaces so the UI remains neutral and replaceable.

## Tests

Test V2 artifacts contain:

- `version`: current schema version.
- `phases`: ordered `Setup`, `Stimulus`, `Observe`, `Assert`, and `Cleanup` lanes.
- `steps`: a compatibility mirror for older readers.
- `runProfile`: local runner defaults.
- `runHistory` and `reportSnapshots`: persisted history references and report metadata.

Migrated V1 flat tests are placed in an `Imported` phase so existing execution order is preserved. New steps are added to their registry-defined default phase.

The first step pack includes MQTT publisher, MQTT trigger/listener, wait for event, conditional event, payload assertion, JSON schema assertion, metric threshold assertion, delay, and cleanup action.

## Sample

Use `samples/flow-applications/operations-dashboard-test-studio.json` as the repo-contained V2 reference sample.

## Stabilization Notes

The dashboard studio keeps the structured grid as the source of truth. Design mode owns cell, track, split, merge, and widget settings edits; Live mode renders the current widget snapshots and runtime event projections without writing design metadata.

The stabilization pass tightened dashboard widget containment across desktop, tablet, and narrow windows so KPI values, event tables, gauge panels, topic activity, payload distribution, and QoS/retain breakdowns shrink without overlapping neighboring cells.

## Metric Query Builder Direction

The dashboard metric query builder is the standard, visual path for defining metrics. It should read like a sentence and keep common setup easy: choose the measure, source, time window, match rules, and number format, then preview the result against live or generated sample data.

Architecturally, this visual builder is a facade over a richer metric-function model. A metric should be able to evaluate against the active app context, runtime events, topic projections, payload summaries, and saved resources, then return a typed scalar, series, or breakdown for widgets. Future advanced editing can allow expression-backed metric definitions for users who want to write logic directly, but the persisted dashboard model should stay neutral and structured so visual and advanced editors can round-trip safely.

KPI remains the first consumer of this builder. The metric engine must not become KPI-specific; the same builder model should later serve counter, rate, chart, table, and topic-focused widgets.

The test studio keeps authoring and execution separate. Scenario Designer remains the phase-lane authoring surface, while Runner Console now carries run preflight, active timeline, event/log streams, diagnosis, run-history selection, and report preview/copy/save actions.

Component catalog fallback is covered so package-backed serialization transforms and FluxMQ aliases remain visible when source references are unavailable.
