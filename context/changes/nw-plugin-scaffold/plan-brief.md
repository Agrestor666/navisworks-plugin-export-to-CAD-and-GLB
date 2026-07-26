# Navisworks Plugin Host Scaffold — Plan Brief

> Full plan: `context/changes/nw-plugin-scaffold/plan.md`

## What & Why

Reshape the temporary ASP.NET Core webapi scaffold into a real, loadable Navisworks Manage 2023 plugin host: two independent `.NET Framework 4.8` projects (GLB export, AutoCAD export) with stub command entry points. This is roadmap `F-01` — a foundation slice with no user-visible export capability yet, but it removes the current blocker (wrong host shape, `top_blocker: skills`) so `S-01` (GLB export) and `S-02` (AutoCAD export) become plannable end-to-end.

## Starting Point

The repo holds only the disposable `dotnet new webapi` scaffold bootstrapped for lack of a Navisworks-specific starter template — `Program.cs`, `.bootstrap-scaffold.csproj` (targeting `net9.0`), `appsettings*.json`. No git history, no test project, no code that talks to the Navisworks API. That scaffold is fundamentally incompatible with Navisworks: add-ins must target `.NET Framework 4.8` (x64), and Navisworks has no manifest file — it loads plugins purely by folder/assembly-name convention.

## Desired End State

Two `net48` plugin projects build cleanly and, once deployed to the local Navisworks install, show up as "Export to GLB" and "Export to AutoCAD" commands under the Add-Ins tab. Clicking either reads the current selection (read-only) and shows its count in a message box — proving the load path, the command wiring, and the core PRD guardrail (never mutate the source model) all work, before any real export logic is written.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| API reference strategy | Reference local Navisworks install via `HintPath` | Matches `AGENTS.md`'s existing note that API assemblies are host-local; guarantees exact version match with the installed host. |
| Project structure | Two separate plugin projects, no shared core yet | Matches the PRD's explicit framing of "two osobne pluginy"; avoids premature abstraction before real duplication appears in S-01/S-02. |
| Install path portability | MSBuild property with standard default + env var override | Works out of the box on a standard install, still overridable on other machines, in one shared `Directory.Build.props`. |
| Command stub scope | Read-only selection-count proof (not a bare no-op) | De-risks the exact API surface (`CurrentSelection`) both S-01 and S-02 depend on, and proves the read-only guardrail this early for near-zero extra cost. |
| Repo setup | `git init` + `.gitignore` as Phase 1 | This reshape (delete + recreate) is the natural moment to start real version history. |
| Test project | Deferred | No business logic exists yet worth testing; avoids an empty ceremonial project. |
| Disk layout | Flat repo root, one folder per project | Matches the current flat layout the bootstrap scaffold already established. |

## Scope

**In scope:** git init, deleting the webapi scaffold, two `net48`/x64 plugin projects referencing `Autodesk.Navisworks.Api.dll`, a shared `Directory.Build.props` for the install path, a root `.sln`, one stub `AddInPlugin` command per project (selection-count proof), a post-build deploy-to-Plugins-folder step, and an `AGENTS.md` refresh.

**Out of scope:** any real GLB/DWG/DXF export logic, the empty-selection error dialog (FR-007) and save-file dialog (FR-008), a shared core library between the two plugins, Navisworks 2025 support, a test project, CI/CD, `.bundle` packaging, ribbon icon/UI polish.

## Architecture / Approach

Two independent SDK-style `net48` class libraries at the repo root, each referencing only `Autodesk.Navisworks.Api.dll` (via a shared `NavisworksInstallDir` MSBuild property) and `System.Windows.Forms`. Each has one `AddInPlugin`-derived command class. A post-build MSBuild target (best-effort, non-blocking) deploys the built DLL into the local Navisworks Plugins folder so `build → open Navisworks → see it load` is a one-step local loop.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Repository foundation | `git init`, `.gitignore`, obsolete scaffold files deleted | None significant — pure cleanup |
| 2. Plugin project scaffolding | Two `net48`/x64 projects + `Directory.Build.props` + `.sln`, all building | Wrong target framework/platform silently breaks host loading later, not at build time |
| 3. Command entry points | `AddInPlugin` classes with selection-count proof | Elevated build required for deploy — easy to forget and think deploy "worked" |
| 4. Host verification & docs | Confirmed live-loading in Navisworks 2023, `AGENTS.md` refreshed | Manual verification is the only signal here — must actually be run, not assumed |

**Prerequisites:** Navisworks Manage 2023 installed on the dev machine at the default path (or `NavisworksInstallDir` env var set); ability to run an elevated build/IDE for the deploy step.
**Estimated effort:** ~1 session across 4 phases.

## Open Risks & Assumptions

- Assumes the default install path `C:\Program Files\Autodesk\Navisworks Manage 2023\` — if the real install lives elsewhere, the `NavisworksInstallDir` environment variable override must be set before building.
- The post-build deploy step needs an elevated terminal/IDE to actually copy files into `Program Files`; a non-elevated build still succeeds but silently skips the deploy (by design, so CI/non-elevated dev loops aren't blocked).
- No `.NET Framework 4.8` Developer Pack check has been done on this machine — if `dotnet build` fails with a missing-targeting-pack error, install it via the Visual Studio Installer before proceeding.

## Success Criteria (Summary)

- `dotnet build NavisworksExportPlugins.sln` succeeds and produces two `net48` assemblies.
- Both "Export to GLB" and "Export to AutoCAD" commands appear under Navisworks Manage 2023's Add-Ins tab and correctly report the selected-item count, including the empty-selection case, without ever mutating the source document.
