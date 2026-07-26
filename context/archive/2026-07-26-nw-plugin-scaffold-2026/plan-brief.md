# Navisworks Manage 2026 Plugin Host Scaffold — Plan Brief

> Full plan: `context/changes/nw-plugin-scaffold-2026/plan.md`
> Research: (none — grounded on archived F-01 + roadmap F-02)

## What & Why

Ship the smallest loadable **Manage 2026** twin of the proven F-01 plugin host — two stub Add-In commands that load in Navisworks Manage 2026 — so **S-03** can port GLB export onto a known-good shell instead of rediscovering host loader/API wiring.

## Starting Point

Manage 2023 host is done (F-01 archived; S-01 GLB live on `NavisworksExport.Glb`). No 2026 projects exist; `Directory.Build.props` points only at Manage 2023. Foundation docs still say “2025 @ v1.1” while the roadmap already parks 2025 and sequences F-02 → S-03 on **2026**.

## Desired End State

`NavisworksExport.Glb.2026` and `NavisworksExport.AutoCad.2026` build, deploy into Manage 2026 `Plugins\`, appear under Add-Ins, report selection count (including empty = 0) without mutating the model. Docs (AGENTS / PRD / tech-stack) match the 2026 host wave. 2023 projects remain unchanged and green.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Project shape | Twin projects (`*.2026`) | Keep 2023 green; side-by-side install | Plan |
| Scaffold breadth | Both plugin stubs only | Unlock S-03 entry points without Geometry/export in F-02 | Plan |
| Assembly naming | `.2026` suffix (folder = assembly) | Avoid Plugins-folder collisions across hosts | Plan |
| Docs catch-up | In F-02 final phase | Close roadmap docs gap before archive | Plan / Roadmap |
| Off-host 2026 API | Install-required HintPath only | No official `NavisworksAPIdlls2026`; avoid unofficial NuGet | Plan |
| Done bar | Build + elevated deploy + load in Manage 2026 | Same proof bar as F-01 before S-03 | Plan |
| Install props | Separate `NavisworksInstallDir2026` | Zero risk of retargeting 2023 builds | Plan |

## Scope

**In scope:** `NavisworksInstallDir2026`; two `net48`/x64 twin projects; stub `AddInPlugin` commands; post-build deploy to Manage 2026; host verification; AGENTS/PRD/tech-stack host-wave sync.

**Out of scope:** Real export on 2026; Geometry retarget; NuGet fallback for 2026; FR-007/FR-008 on stubs; tests/CI/bundle; Manage 2025; multi-targeting 2023 projects.

## Architecture / Approach

Parallel SDK-style class libraries in the existing solution. Twins reference Manage 2026 `Autodesk.Navisworks.Api.dll` via `NavisworksInstallDir2026` and deploy to that host’s `Plugins\<AssemblyName>\`. Stubs copy the F-01 AutoCAD selection-count pattern (`NwApplication` alias), not the S-01 GLB exporter.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Twin project scaffolding | Props + two csproj + sln wiring | Missing Manage 2026 install blocks compile (by design) |
| 2. Command entry points | Selection-count stubs | Accidentally copying S-01 GLB export into the twin |
| 3. Host verification & docs | Live load in Manage 2026 + docs sync | Non-elevated deploy looks “green” but plugins never appear |

**Prerequisites:** Navisworks Manage 2026 installed (or `NavisworksInstallDir2026` set); elevated terminal/IDE for deploy; F-01 already done.
**Estimated effort:** ~1 session across 3 phases.

## Open Risks & Assumptions

- Assumes default path `C:\Program Files\Autodesk\Navisworks Manage 2026\` (override via `NavisworksInstallDir2026`).
- Assumes Manage 2026 still loads `net48`/x64 Add-Ins with the same folder=assembly convention as 2023 — verify in Phase 3; if TFM/loader differs, stop and replan before S-03.
- `shape-notes.md` may still mention 2025; this plan updates PRD/AGENTS/tech-stack only (roadmap’s named docs gap).

## Success Criteria (Summary)

- Both `*.2026` stubs load in Manage 2026 and report selection count without mutating the document.
- 2023 solution projects still build; install-dir defaults for 2023 unchanged.
- Foundation docs describe 2026 as the next host wave (not 2025).
