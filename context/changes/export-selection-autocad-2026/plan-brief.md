# Export Selection to AutoCAD on Manage 2026 — Plan Brief

> Full plan: `context/changes/export-selection-autocad-2026/plan.md`
> Research: `context/changes/export-selection-autocad-2026/research.md`

## What & Why

Ship selection → **DWG (AC1032)** from Navisworks Manage **2026** so the file opens in AutoCAD as filled, shaded, colored mesh geometry — the product’s AutoCAD half of the MVP workflow. True `3DSOLID` is out; industry practice and ACadSharp limits point to `PolyfaceMesh` + viewport shading instead.

## Starting Point

`NavisworksExport.AutoCad.2026` is an F-02 selection-count stub. `NavisworksExport.Geometry` / `Geometry.2026` already extract world-space colored triangles (used by GLB), but only as a **flat** list — fragment boundaries needed for per-fragment meshes are not exposed. No ACadSharp reference exists in the repo yet.

## Desired End State

In Manage 2026, export a real selection to `.dwg`; AutoCAD opens it shaded with visible per-face colors and no import errors. Empty / zero-geometry selections error like GLB (no empty file). Manage 2023 AutoCAD stays a stub until a later reverse-port.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Host sequencing | 2026-first greenfield (S-02 deferred) | Active host wave is 2026; format decisions are host-agnostic | Plan |
| Output format | DWG AC1032 first (no DXF this change) | User preference; AC1032 covers AutoCAD 2018–2027 | Plan |
| Entity type | `PolyfaceMesh` (not `Mesh` / `3DSOLID`) | Per-face color works in ACadSharp; solids need watertight ACIS | Research |
| Face color | Average RGB of C0/C1/C2 → true color | Best fidelity from existing `ExtractedTriangle` data | Plan |
| Chunking | Per-fragment, split at ~32k verts | Matches extractor boundaries + PolyfaceMesh limit | Plan + Research |
| Layers / attributes | Single default layer; no property→attributes | Keeps PRD Non-Goals; user withdrew full BIM dump | Plan |
| Shaded open | Set active `Viewport.RenderMode` Gouraud | Default templates often open 2D wireframe | Research |
| De-risk | `tools/DwgWriterHarness` before command wiring | Empirically close research open Qs (color + VPORT) | Plan + Research |
| Library | ACadSharp `3.6.35` exact pin | netstandard2.0 → net48; matches repo pin style | Research + Plan |

## Scope

**In scope:** `ExtractGrouped` / `ExtractedFragment`; `DwgWriter` + ACadSharp on AutoCad.2026; harness; command UX (FR-007/008); Manage 2026 + AutoCAD verification; roadmap sequencing update.

**Out of scope:** S-02 Manage 2023 AutoCAD; DXF; `3DSOLID`; property attributes / per-item layers; GLB changes beyond additive Geometry API; formal unit-test project; cloud/APS.

## Architecture / Approach

```text
Manage 2026 selection
  → Geometry.2026 ExtractGrouped (fragment groups)
  → DwgWriter (ACadSharp PolyfaceMesh + VPORT)
  → SaveFileDialog *.dwg
  → AutoCAD opens shaded + colored
```

Writer lives in `NavisworksExport.AutoCad.2026` (2026-first; reverse-link later like Glb twins). Deploy copies private DLLs (exclude `Autodesk.*`).

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Grouped geometry API | `ExtractedFragment` + `ExtractGrouped` | Accidentally breaking flat `Extract` for GLB |
| 2. DWG writer + harness | ACadSharp writer + visual proof in AutoCAD/TrueView | Per-face color or VPORT doesn’t behave as docs claim |
| 3. Command wiring | Real `AutoCadExportCommand` + multi-DLL deploy | Deploy misses dependency DLLs → runtime load failure |
| 4. Host verify + roadmap | Live Manage 2026 matrix; S-02/S-04 sequencing fixed | Non-elevated deploy looks green but plugin never lands |

**Prerequisites:** F-02 done; Manage 2026 installed; AutoCAD or DWG TrueView for harness + acceptance; elevated terminal for plugin deploy.
**Estimated effort:** ~2–3 sessions across 4 phases (writer/harness is the riskiest).

## Open Risks & Assumptions

- Per-face `PolyfaceMesh` color and VPORT shaded-on-open need harness confirmation before Phase 3.
- ACadSharp is young around mesh writers — harness is the safety net.
- Roadmap previously blocked S-04 on S-02; this plan intentionally resequences and must update `roadmap.md`.

## Success Criteria (Summary)

- Selection from Manage 2026 exports to DWG and opens in AutoCAD shaded with visible colors, no import errors.
- Empty / zero-geometry paths error clearly and never write an empty file.
- Source Navisworks document unchanged; 2023 AutoCAD stub unaffected.
