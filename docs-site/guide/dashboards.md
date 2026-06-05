# Dashboards

Dashboards show runtime events from the running app. They are separate app artifacts, not pipeline nodes.

The V2 dashboard framework uses a structured grid, reusable metric queries, widget bindings, and separate authoring/runtime modes. The operations dashboard sample includes an `operations` dashboard with MQTT operations widgets for message count, status, trends, topic activity, payload sizes, and QoS/retain breakdown.

## Modes

- Design: change the grid, cells, metrics, widget instances, bindings, and widget settings.
- Live: view the dashboard while the app is running.
- Presentation: reserved for a later full-screen display mode.

The right live tools panel is available in dashboard mode so you can publish test traffic and watch widgets update without leaving the dashboard. Generated-source samples can also drive widgets without a broker.

## Widgets

The first widget pack is MQTT operations focused:

- KPI tile and rate tile.
- Status strip.
- Line, area, bar, donut, and gauge chart widgets.
- Event table and latest event.
- Topic tree and topic activity widgets.
- Payload size distribution.
- QoS and retain breakdown.

Widget settings can bind to reusable metrics and can filter by event type, topic prefix, excluded topic prefix, QoS, retain flag, status, and payload text where those fields apply.

Filters are combined with AND semantics. For example, a topic prefix of `fluxmq/test` and an excluded prefix of `$SYS` means the event must start with `fluxmq/test` and must not start with `$SYS`.

## Metrics

Metrics are named once, then reused by widgets. The first metric sources are runtime events, topic/message projection, MQTT metric snapshots, and payload inspection summaries.

Metric settings include source, event type, topic filters, QoS/retain filters, aggregation, window, grouping, and formatting where those options apply.

## Runtime Boundary

Dashboards observe app runtime events. Isolated test-runner events stay out of app dashboard counts unless the test is observing the running app.
