# FluxMQ UI Mockups

These are deterministic raster mockups generated from `render_fluxmq_mockups.py`.

They are intended as early visual direction for the Blazor/MudBlazor app, not final product art.

## Images

- `01-main-workspace.png` - primary MQTT operations console.
- `02-payload-debugger.png` - payload inspection and message diffing.
- `03-observability-replay.png` - recording, observability, and replay timeline.

## Direction

The mockups assume:

- Dark, IDE-like desktop workspace.
- Dense but scannable operational UI.
- Topic explorer, message stream, payload inspector, metrics, and replay as first-class surfaces.
- Restrained palette with semantic color: green for healthy, cyan for live data, yellow for warnings, red for alerting, purple for schema/drift events.

## Regeneration

```powershell
python design/ui-mockups/render_fluxmq_mockups.py
```
