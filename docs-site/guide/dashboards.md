# Dashboards

Dashboards show runtime events from the running app. They are separate app artifacts, not pipeline nodes.

## Modes

- Edit: change rows, columns, cells, and widgets.
- Live: view the dashboard while the app is running.

The right live tools panel is available in dashboard mode so you can publish test traffic and watch widgets update without leaving the dashboard.

## Widgets

Current dashboard widgets are event-based:

- Event counter: counts matching runtime events.
- Latest event: shows the latest matching event.
- Event rate: shows recent event throughput.

Widget settings can filter by event type, topic prefix, excluded topic prefix, QoS, retain flag, status, and payload text where those fields apply.

Filters are combined with AND semantics. For example, a topic prefix of `fluxmq/test` and an excluded prefix of `$SYS` means the event must start with `fluxmq/test` and must not start with `$SYS`.

## Runtime Boundary

Dashboards observe app runtime events. Isolated test-runner events stay out of app dashboard counts unless the test is observing the running app.
