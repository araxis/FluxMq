# Sample App

The sample app is a complete FluxMQ workspace for the dashboard and test-studio V2 foundation. It shows the current app shape without relying on a personal project file.

<div class="sample-actions">
  <a class="sample-action primary" href="../samples/operations-dashboard-test-studio.json">Download operations-dashboard-test-studio.json</a>
</div>

![Operations monitor workspace](/screenshots/sample-workspace.png)

## What It Contains

- Pipeline `operationsMonitor`: generated MQTT-style traffic, payload inspection, and metrics.
- Dashboard `operations`: V2 layout, reusable metrics, widget bindings, and MQTT operations widgets.
- Test `operationsRegression`: phase-based scenario lanes for setup, stimulus, observe, assert, and cleanup.
- Runner metadata: local run profile, run-history references, and report snapshot placeholders.

## Open It

Use the desktop app's Open file action and select:

```text
docs-site/public/samples/operations-dashboard-test-studio.json
```

When launching from a built desktop executable, the same file can be opened directly:

```sh
FluxMq.UI.exe --open docs-site/public/samples/operations-dashboard-test-studio.json
```

## Try The Main Flow

1. Open the sample app.
2. Run the app.
3. Open the `operations` dashboard in Design mode and inspect the grid, metrics, bindings, and widget settings.
4. Switch the dashboard to Live mode and watch the generated traffic drive KPI, status, trend, topic, payload-size, and QoS/retain widgets.
5. Open the test studio and run `operationsRegression` from the Runner Console.

![Operations monitor flow canvas](/screenshots/sample-flow-canvas.png)

## What To Look At

- The pipeline canvas shows generated source, payload inspection, and metrics activity.
- The dashboard keeps structured grid layout as the source of truth.
- Metric definitions can be reused across multiple widgets.
- The runner console keeps execution timeline, live events, logs, payload detail, and report output separate from authoring.
- The Topics page gives the topic tree room for inspection instead of hiding it in a narrow side panel.

## Component Search

The component catalog includes search so the growing component list stays usable as more sources, filters, mappers, storage, routing, and observability components are added.

![Pipeline component search](/screenshots/sample-component-panel.png)
