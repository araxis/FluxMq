# FluxMQ Progress Log

Chronological progress record.

## 2026-05-06

- Read the initial FluxMQ proposal.
- Chose to treat the proposal as a product north star, not a fixed architecture.
- Agreed that LiteDB is a good first storage database.
- Decided to prioritize the message/session pipeline before formal external plugins.
- Created the `memory` folder for project continuity.
- Renamed the original proposal to `FluxMQ-Platform-Proposal.md`.
- Added project memory files:
  - `00-index.md`
  - `01-decisions.md`
  - `02-architecture-plan.md`
  - `03-roadmap.md`
  - `04-progress-log.md`
- Created the initial .NET solution scaffold:
  - `FluxMq.App`
  - `FluxMq.Core`
  - `FluxMq.Pipeline`
  - `FluxMq.Storage`
  - `FluxMq.UI`
  - `FluxMq.Core.Tests`
  - `FluxMq.Pipeline.Tests`
  - `FluxMq.Storage.Tests`
- Added initial package references:
  - MQTTnet in `FluxMq.Core`
  - LiteDB in `FluxMq.Storage`
  - MudBlazor in `FluxMq.App`
  - FluentAssertions in test projects
- Wired MudBlazor into the MAUI Blazor app.
- Normalized projects to .NET 10 target frameworks because the MAUI Blazor template generated `net10.0` targets.
- Limited the first MAUI target to Windows desktop.
- Replaced the empty generated `.slnx` with a classic `.sln`.
- Added a root `.gitignore`.
- Verified `dotnet restore`, `dotnet build`, and `dotnet test` all pass.
- Initialized a local Git repository on branch `main`.
- Created the initial commit: `11f00d1 Initial FluxMQ scaffold`.
- Checked GitHub profile through the connected GitHub app: `araxis`.
- Confirmed no existing `FluxMq` repository was found under that profile through repository search.
- Used GitHub CLI from `C:\Program Files\GitHub CLI\gh.exe`.
- Created private GitHub repository: `https://github.com/araxis/FluxMq`.
- Added `origin` remote and pushed `main`.
- Verified remote visibility is private and default branch is `main`.
- Added initial `README.md` with project vision, status, architecture direction, build commands, and links to memory docs.
- Created initial UI mockup assets under `design/ui-mockups/`:
  - `01-main-workspace.png`
  - `02-payload-debugger.png`
  - `03-observability-replay.png`
- Added `design/ui-mockups/render_fluxmq_mockups.py` to regenerate the mockups deterministically.
- Added `design/ui-mockups/README.md` describing the UI direction.
- Installed Pillow locally for Python-based mockup rendering.
- Installed Node.js LTS for Remotion work.
- Created a Remotion intro animation under `design/intro-animation/`.
- Rendered intro outputs:
  - `design/intro-animation/out/fluxmq-intro.mp4`
  - `design/intro-animation/out/fluxmq-intro-poster.png`
- Updated the root `README.md` with the UI mockups, intro poster, and intro animation link.
- Changed the intro animation README section to use an HTML `<video controls>` block with a fallback link.

- Converted the intro animation from MP4 to GIF using Remotion's built-in GIF codec:
  - Output: `design/intro-animation/out/fluxmq-intro.gif` (960×540, 15 fps, 5.8 MB).
  - Render parameters: `--codec=gif --scale=0.5 --every-nth-frame=2`.
  - Added `render:gif` npm script to `design/intro-animation/package.json`.
- Replaced the unplayable `<video>` embed in the README with a `![img]` GIF embed (GitHub does not render `<video>`).
- Added `design/ui-mockups/01-main-workspace.png` as a full-width static banner at the very top of the README.
- Removed "dark" from the Visual Direction description: FluxMQ supports both dark and light themes; it is not a defining characteristic worth calling out.
- Trimmed README noise: removed the Remotion/MP4 attribution line, the `### UI Mockups` section header, and the `Primary MQTT operations workspace:` caption.
- Merged PR #1 (`claude/frosty-bose-2c2756`): GIF banner + video embed fix.
- Opened PR #2 (`readme-banner-cleanup`): static mockup banner + README cleanup.

## Current Next Step

Define the first core domain models and MQTT session abstractions.
