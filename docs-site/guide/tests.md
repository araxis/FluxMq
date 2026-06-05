# Tests

Tests are scenario artifacts inside the app file. They are separate from production pipelines, but they can reuse normal components where the behavior is the same.

The V2 test framework separates authoring from execution. Scenario Designer is for arranging phases and editing steps. Runner Console is for preflight, execution, live events, logs, matched details, failure diagnosis, and report/export actions.

The operations dashboard sample includes `operationsSmoke`, a phase-based scenario with setup, stimulus, observe, assert, and cleanup lanes.

## Scenario Phases

Default scenarios use these phases:

- Setup
- Stimulus
- Observe
- Assert
- Cleanup

Older flat scenarios are migrated into an imported phase so their execution order is preserved.

## Step Pack

- `mqtt.publisher`: publishes an MQTT message through an app-level broker resource.
- `mqtt.trigger`: creates a runner-owned MQTT trigger using an app-level broker resource.
- `wait.event`: waits for a matching event.
- `when.event`: gates later steps based on observed runtime events.
- `expect.event`: waits for a matching runtime event.
- `assert.payload`: checks payload content.
- `assert.json-schema`: validates payload JSON against a schema.
- `assert.metric-threshold`: checks a named metric threshold.
- `wait.delay`: waits for a duration.
- `cleanup.action`: performs cleanup work.

## Runner Boundary

The test runner owns its MQTT clients and event observation lifetime. It does not mutate the app pipeline graph just to run a test.

Use app-level connections as shared resources. Do not define broker settings inside individual test steps unless a future step explicitly supports that.

## Reports

Scenario results persist local run history with run metadata, step results, matched events, log excerpts, and report snapshots. Reports can be previewed, copied, or saved as structured JSON or readable text.
