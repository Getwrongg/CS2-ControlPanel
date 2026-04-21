# CS2 Dedicated Server Admin Control Panel

A Windows desktop control panel for managing Counter-Strike 2 dedicated servers over RCON.

Linux Version: https://github.com/SkinnyRibs/CS2-ControlPanelLinux

CS2 Control Panel is designed for server owners, match admins, and community operators who want a structured workflow for running server configs, switching maps, monitoring server health, and performing common admin actions from a single UI.

<img width="1379" height="887" alt="image" src="https://github.com/user-attachments/assets/f35763c6-cee0-4ea0-a0cc-7232c6ff05b2" />

**Latest release:** https://github.com/Getwrongg/CS2-ControlPanel/releases/tag/v1.0.0

**Direct download (v1.0.0):** https://github.com/Getwrongg/CS2-ControlPanel/releases/download/v1.0.0/CS2AdminTool.zip

> Because this app is not code-signed yet, Windows may show a warning.
> Only run it if you downloaded it from the official GitHub Releases page for this project.
> We plan to provide a properly Windows code-signed release process in a future update.

## FULL RCON COMMAND LIST
- [Click Here for Full Command List](commands.md)


## Application Features

- **RCON connection management**
  - Connect/disconnect to a server with host, port, and password.
  - Send manual commands and execute saved server profiles.

- **Config Library**
  - Organize server configs by category.
  - Create, edit, duplicate, and delete configs.
  - Store multi-command profiles and run them with configurable execution behavior.

- **Map Library**
  - Maintain standard and workshop map profiles.
  - Tag and annotate map entries.
  - Assign map profiles directly to server configs.

- **Live Monitor**
  - Refresh status/stats on demand or at intervals.
  - View map, mode, player count, and raw server telemetry output.
  - Surface health indicators such as choke/loss/tick variance.

- **Import / Export**
  - Import/export all data or scoped data sets (configs/maps).
  - Share individual server config profiles.

- **RCON Ops+ utilities**
  - Preset command packs with rollback support.
  - Player actions (kick/ban/mute/swap team) with admin reason entry.
  - Scheduled automation hooks for recurring admin tasks.
  - Safety guardrails (dry-run mode, denylist/allowlist, destructive command confirmation).
  - Audit log export for administrative traceability.

## Tech Stack

- **.NET 8**
- **WPF (Windows desktop UI)**
- **CoreRCON** for RCON communication

## Project Structure

- `CS2AdminTool/` — Main WPF application
- `CS2AdminTool/Data/Seeds/` — Seed JSON files copied to app data on first run
- `build.ps1` — Build and packaging script (publishes and zips output)
- `dist/` — Generated build artifacts

## Getting Started

### Prerequisites

- Windows 10/11
- .NET SDK 8.0+
- Access to a CS2 server with RCON enabled

### Run from source

```powershell
# from repo root
dotnet build .\CS2AdminTool.sln

dotnet run --project .\CS2AdminTool\CS2AdminTool.csproj
```

### Build distributable package

```powershell
# from repo root
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

This produces a zip artifact at:

- `dist/CS2AdminTool.zip`

## Data Storage

Application data is stored in the current user's local app data directory:

- `%LOCALAPPDATA%\CS2AdminTool\Data`

On first run, seed data is copied from `CS2AdminTool/Data/Seeds` if missing.

## Open Source Usage

This repository is intended for public/open-source usage.

If you use this project:

1. Keep your own server credentials secure.
2. Review and test command packs/configs before applying them to production servers.
3. Validate your local server rules and regional policies before using player-management actions.

## Security Notes

- RCON is powerful and potentially destructive if misused.
- Prefer running with dry-run and confirmation options while validating new workflows.
- Avoid committing private server endpoints/passwords to source control.

## Contributing

Contributions are welcome.

- Open an issue to discuss bugs or feature ideas.
- Submit a PR with a clear description, rationale, and testing notes.
- Keep changes focused and avoid unrelated formatting churn.

## Disclaimer

This project is not affiliated with Valve. Counter-Strike and related trademarks are property of their respective owners.
