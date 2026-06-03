# Tests

Tests are scenario artifacts inside the app file. They are separate from production pipelines, but they can reuse normal components where the behavior is the same.

## Current Steps

- `mqtt.publisher`: publishes an MQTT message through an app-level broker resource.
- `mqtt.trigger`: creates a runner-owned MQTT trigger using an app-level broker resource.
- `when.event`: gates later steps based on observed runtime events.
- `expect.event`: waits for a matching runtime event.

## Runner Boundary

The test runner owns its MQTT clients and event observation lifetime. It does not mutate the app pipeline graph just to run a test.

Use app-level connections as shared resources. Do not define broker settings inside individual test steps unless a future step explicitly supports that.

## Reports

Scenario results keep recent run history in the desktop session. Reports can be previewed, copied, or saved as structured JSON or readable text.
