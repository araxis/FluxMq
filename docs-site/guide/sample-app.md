# Sample App

The sample app is a complete FluxMQ workspace for an operations monitor. It shows the current app shape without relying on a personal project file.

<div class="sample-actions">
  <a class="sample-action primary" href="../samples/operations-monitor.json">Download operations-monitor.json</a>
</div>

![Operations monitor workspace](/screenshots/sample-workspace.png)

## What It Contains

- App resources: `local-broker` on `localhost:1883` and `edge-broker` on `localhost:1884`.
- Pipeline `order-intake`: live MQTT trigger, payload inspector, schema validator, conditional router, mapper, publisher, metrics, and logger.
- Pipeline `edge-replay`: generated source traffic, filter, mapper, publisher, and metrics.
- Dashboard `ops-overview`: event counters, latest payload views, and event-rate widgets.
- Test `priority-order-roundtrip`: publish a priority order and expect receive/publish runtime events.

## Open It

Use the desktop app's Open file action and select:

```text
docs-site/public/samples/operations-monitor.json
```

When launching from a built desktop executable, the same file can be opened directly:

```sh
FluxMq.UI.exe --open docs-site/public/samples/operations-monitor.json
```

## Try The Main Flow

1. Open the sample app.
2. Connect `local-broker`.
3. Run the app.
4. Publish this payload to `factory/orders/line-a/priority` with QoS `1`:

```json
{
  "orderId": "A-2048",
  "priority": "high",
  "status": "blocked",
  "line": "line-a"
}
```

The `order-intake` pipeline receives the message, validates the payload, routes the high-priority order, maps it into an alert publish request, and publishes to `ops/alerts/factory/orders/line-a/priority`.

![Operations monitor flow canvas](/screenshots/sample-flow-canvas.png)

## What To Look At

- The pipeline canvas shows message activity on source, mapper, publisher, metrics, and logger nodes.
- The dashboard counts only matching business topics and excludes `$SYS`.
- The Logs page keeps scoped runtime entries separate from the live publish tools.
- The Topics page gives the topic tree room for inspection instead of hiding it in a narrow side panel.

## Component Search

The component catalog includes search so the growing component list stays usable as more sources, filters, mappers, storage, routing, and observability components are added.

![Pipeline component search](/screenshots/sample-component-panel.png)
