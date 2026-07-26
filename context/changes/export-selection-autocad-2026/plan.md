# Export Selection to AutoCAD on Manage 2026 (DWG) Implementation Plan

## Overview

Deliver a real "Export to AutoCAD" flow on the Manage **2026** twin: read the current selection, extract mesh geometry via the existing `NavisworksExport.Geometry` stack (extended with fragment grouping), write an **AC1032 DWG** of `PolyfaceMesh` entities with per-face true color and a shaded active viewport, and prove the file opens in AutoCAD looking filled/shaded — not wireframe-only and not a failed `3DSOLID` conversion. This change is the **proof path** for the AutoCAD capability (roadmap S-02 stays deferred; a later reverse-port can land on Manage 2023).

## Current State Analysis

`NavisworksExport.AutoCad.2026` is a loadable F-02 stub: `AutoCadExportCommand` only shows the selected-item count. It has no `ProjectReference` to Geometry, no ACadSharp package, and a single-DLL deploy target that would omit private dependencies. `NavisworksExport.Geometry` / `Geometry.2026` already extract world-space colored triangles with fragment dedup, but `Extract` returns a **flat** `IReadOnlyList<ExtractedTriangle>` — fragment boundaries needed for per-fragment `PolyfaceMesh` chunking are discarded inside the extractor loop. The sibling AutoCAD 2023 plugin is the same stub; there is no prior DWG writer to link.

Upstream research (`context/changes/export-selection-autocad-2026/research.md` + S-02 research) settled: no native Navisworks→DWG API; use ACadSharp; prefer `PolyfaceMesh` over `Mesh`/`3DSOLID`; set `Viewport.RenderMode` so the file opens shaded; industry exporters stop at mesh.

## Desired End State

In Navisworks Manage 2026, with a non-empty geometry-bearing selection, "Export to AutoCAD" opens a save dialog, writes a `.dwg` (AC1032) containing that selection as one or more colored `PolyfaceMesh` entities on a single default layer, and opening the file in AutoCAD shows filled/shaded geometry with visible per-face colors and no import errors. Empty selection and zero-triangle selection show the same clear errors as the GLB plugin (no empty file). The source document is never modified. Manage 2023 AutoCAD plugin remains a stub until a future reverse-port.

**Verification**: build the solution; run `tools/DwgWriterHarness` and open the DWG in AutoCAD/TrueView; then load the plugin in Manage 2026, export a real selection, and confirm the same visual criteria in AutoCAD.

### Key Discoveries:

- `SelectionGeometryExtractor.Extract` flattens all triangles (`SelectionGeometryExtractor.cs` emit loop) — per-fragment meshes require an additive grouped API; keep `Extract` for GLB unchanged.
- ACadSharp `Mesh` cannot do per-face color overrides today; `PolyfaceMesh` + `VertexFaceRecord.Color` can (`research.md` §4). Pin package `ACadSharp` `3.6.35` (netstandard2.0 → net48), matching this repo’s exact-pin style (`SharpGLTF.Toolkit` `1.0.6`).
- Hard limit ~32 767 vertices per `PolyfaceMesh`; split within a fragment when exceeded (`research.md` §7).
- Shaded “looks like a solid” depends on `VPORT` render mode, not entity type — set active viewport `RenderMode` to Gouraud shaded (`research.md` §3). Empirically confirm in the harness before trusting host acceptance.
- Unlike GLB, AutoCAD is Z-up like Navisworks — **do not** apply the GLB Y-up axis swap. Write world-space coordinates from the extractor (document units).
- Mirror GLB UX strings/control flow (`GlbExportCommand.cs`); upgrade AutoCad.2026 deploy to copy `$(TargetDir)*.dll` excluding `Autodesk.*.dll` (pattern in `NavisworksExport.Glb.2026.csproj`).
- S-03 GLB 2026 host-debugging lessons are captured in `@context/foundation/lessons.md` and summarized in `AGENTS.md` (Manage 2026 plugin checklist). Re-read both before Phase 3; do not rediscover assembly-resolve crashes, composite-selection drops, or COM matrix bugs.

## What We're NOT Doing

- Manage **2023** AutoCAD export (S-02) — deferred; this plan is 2026-first greenfield. A later reverse-port may link or copy the writer.
- DXF output in this change — **DWG-first (AC1032)** only; DXF can follow in a later change.
- True `3DSOLID` / ACIS / `CONVTOSOLID` / watertight mesh repair (`research.md` §1–2, §5).
- Property → block attribute mapping, per-item layers, or BIM metadata dump (user withdrew full attributes; PRD Non-Goals).
- Changing GLB plugins or the semantics of `Extract` (additive Geometry API only).
- A formal unit-test project (`AGENTS.md`); verification is harness + manual host/AutoCAD.
- Cloud/APS, batch export, whole-model export, Manage 2025 (PRD Non-Goals / parked).

## Implementation Approach

1. **Extend Geometry** with `ExtractedFragment` + `ExtractGrouped` (linked sources → both Geometry twins), leaving `Extract` intact for GLB.
2. **`DwgWriter` in `NavisworksExport.AutoCad.2026`** — ACadSharp `CadDocument` → `PolyfaceMesh`(es) with averaged per-face true color, default layer, active `Viewport.RenderMode` Gouraud; chunk per fragment at the 32k vertex limit. Smoke via `tools/DwgWriterHarness` opened in AutoCAD/TrueView.
3. **Wire `AutoCadExportCommand`** — same empty / zero-triangle / cancel / success / exception pattern as GLB; `SaveFileDialog` for `*.dwg`; `ProjectReference` → `Geometry.2026`; multi-DLL deploy.
4. **Live Manage 2026 + AutoCAD** verification; update `roadmap.md` so S-04 is no longer blocked on S-02 and S-02 remains parked as reverse-port.

## Critical Implementation Details

### Grouped extract must stay additive

GLB depends on `IReadOnlyList<ExtractedTriangle> Extract(...)`. Add a parallel entry point that returns fragment groups; do not change `Extract`’s return shape. Geometry.2026 compiles the same linked `.cs` files — one source change covers both host twins.

### No Y-up conversion for DWG

`GlbWriter` swaps Z-up→Y-up for PowerPoint. AutoCAD expects Z-up; applying that swap would lay the model on its side. Pass extractor world-space coordinates through (optionally document→meters only if a clear `INSUNITS`/header story is trivial — default: write document units as-is).

### Harness gates visual assumptions

Research open questions (per-face color on `PolyfaceMesh`, VPORT shaded-on-open) are **not** closed until a human opens the harness DWG in AutoCAD/TrueView. Do not treat Phase 2 automated build success as proof of FR-006 visual quality.

## Phase 1: Grouped geometry API

### Overview

Expose fragment-bounded triangle groups from the existing COM extractor so the DWG writer can emit one (or more) `PolyfaceMesh` per fragment without re-deriving boundaries from a flat list.

### Changes Required:

#### 1. Fragment DTO

**File**: `NavisworksExport.Geometry/ExtractedFragment.cs` (new; linked into `NavisworksExport.Geometry.2026`)

**Intent**: Format-agnostic group of world-space colored triangles belonging to one extracted fragment occurrence after dedup/transform — enough for “one mesh per fragment”, without ModelItem names or BIM properties.

**Contract**: Public type holding `IReadOnlyList<ExtractedTriangle> Triangles` (optional opaque index/key is fine if useful for debug; not required for MVP). No ACadSharp/glTF types.

#### 2. Grouped extract API

**File**: `NavisworksExport.Geometry/SelectionGeometryExtractor.cs`

**Intent**: Reuse the existing path-filter + Geometry-reference dedup + local-to-world pipeline, but emit triangles grouped per fragment occurrence instead of appending into one flat list. Keep `Extract` as a thin flatten of the grouped result (or equivalent) so GLB behavior stays identical.

**Contract**: New public method e.g. `IReadOnlyList<ExtractedFragment> ExtractGrouped(ModelItemCollection selection)`. Null selection → `ArgumentNullException`. Empty selection → empty list. Read-only on the document. `Extract` remains and continues to return the same flat triangle sequence GLB already consumes. **Must call the same `GeometryLeaves` expansion as `Extract`** before `ComApiBridge.ToInwOpSelection` — composite/group selections export nothing without it (`@context/foundation/lessons.md`, S-03).

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds with the new Geometry types
- `NavisworksExport.Geometry` and `NavisworksExport.Geometry.2026` both build; existing `Extract` call sites in GLB projects still compile without changes
- No ACadSharp types appear inside `NavisworksExport.Geometry*`

#### Manual Verification:

- No standalone host check this phase — grouping is proven when Phase 2/4 consume `ExtractGrouped`

**Implementation Note**: After automated verification passes, pause for human confirmation before Phase 2.

---

## Phase 2: DWG writer + harness

### Overview

Add ACadSharp-backed DWG writing and a host-free harness that proves per-face color + shaded viewport before touching the Navisworks command.

### Changes Required:

#### 1. Package + project wiring

**File**: `NavisworksExport.AutoCad.2026/NavisworksExport.AutoCad.2026.csproj`

**Intent**: Pull in ACadSharp and Geometry.2026; widen deploy so private dependency DLLs land next to the plugin (same pattern as Glb.2026).

**Contract**: `PackageReference` `ACadSharp` Version=`3.6.35` (exact pin). `ProjectReference` → `NavisworksExport.Geometry.2026`. Deploy target copies `$(TargetDir)*.dll` excluding `Autodesk.*.dll` into `$(NavisworksInstallDir2026)Plugins\$(AssemblyName)\`.

#### 2. DWG writer

**File**: `NavisworksExport.AutoCad.2026/DwgWriter.cs`

**Intent**: Convert grouped `ExtractedTriangle`s into an AC1032 DWG: default layer; one `PolyfaceMesh` per fragment (split when unique vertices in that chunk would exceed ~32 767); each face `VertexFaceRecord.Color` = average of the triangle’s three vertex RGBAs as true color; set active viewport `RenderMode` to Gouraud shaded so the file opens filled, not 2D wireframe-only.

**Contract**: Static entry point e.g. `WriteDwg(IReadOnlyList<ExtractedFragment> fragments, string filePath)` (or triangles+grouping equivalent). Reject null/empty path; do not write an empty mesh file when there are zero triangles across all fragments (caller may pre-check — writer should still guard). Uses `CadDocument` + `DwgWriter` (ACadSharp). No Navisworks API types inside this file. No Y-up axis swap.

#### 3. Writer harness

**File**: `tools/DwgWriterHarness/` (new; out of solution, mirror `tools/GlbWriterHarness`)

**Intent**: Host-free smoke: hardcoded multi-color unit cube (and enough triangles/colors to exercise per-face coloring) → `.dwg` on disk for AutoCAD/TrueView inspection of shaded open + per-face colors.

**Contract**: `net48`/x64 exe; `ProjectReference` to `NavisworksExport.AutoCad.2026` (+ Geometry.2026 if needed for DTOs). Optional output path arg; default Desktop/`dwg-writer-harness-cube.dwg`. Not added to `NavisworksExportPlugins.sln`. Gitignore generated `.dwg` under the harness folder (same idea as Glb harness `.glb`).

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExport.AutoCad.2026/NavisworksExport.AutoCad.2026.csproj` succeeds with ACadSharp restore
- `dotnet build tools/DwgWriterHarness/DwgWriterHarness.csproj` succeeds
- Running the harness writes a non-empty `.dwg` and exits 0

#### Manual Verification:

- Open harness DWG in AutoCAD or DWG TrueView: geometry appears **filled/shaded** (not only 2D wireframe) without the user having to hunt for a visual style — or document the one-click workaround if VPORT alone proves insufficient
- Distinct per-face (or per-triangle) colors are visible on the cube
- File opens with **no import/recovery errors**

**Implementation Note**: Do not start Phase 3 until the manual harness visual checks pass (or an explicit fallback for VPORT is agreed). Pause for human confirmation.

---

## Phase 3: Command wiring on AutoCad.2026

### Overview

Replace the selection-count stub with the real export orchestration, matching GLB’s FR-007/FR-008 behavior and product tone.

### Changes Required:

#### 1. Export command

**File**: `NavisworksExport.AutoCad.2026/AutoCadExportCommand.cs`

**Intent**: Empty selection → error, no dialog, no file; save dialog for DWG; `ExtractGrouped` → zero triangles → same error pattern; write DWG; success/failure message boxes. Keep Plugin id `AutoCadExport2026`. Drop the “(2026 scaffold)” caption tone.

**Contract**: Mirror `GlbExportCommand` control flow and body strings (swap product name / filter only):
- Empty: `"Select one or more items to export. The current selection is empty."`
- Zero geometry: `"The selection has no extractable mesh geometry. Nothing was exported."`
- Success: exported triangle count + path
- Exception: `"Export failed:"` + message
- Caption / DisplayName family: `"Export to AutoCAD"`
- Dialog: filter `*.dwg`, `DefaultExt = "dwg"`, default `selection.dwg`, `OverwritePrompt = true`; cancel → `return 0` silent
- Read-only: never mutate the document or selection

#### 2. Lessons from S-03 (GLB 2026) — copy, do not re-debug

**File**: `NavisworksExport.AutoCad.2026/AutoCadExportCommand.cs`, `NavisworksExport.AutoCad.2026/NavisworksExport.AutoCad.2026.csproj`

**Intent**: S-03 hit hard crashes and silent partial exports on Manage 2026 before GLB worked. AutoCAD Phase 3 must inherit the fixes without repeating the investigation.

**Contract** — required checklist (reference implementation: `NavisworksExport.Glb.2026/GlbExportCommand.cs`; full rationale: `@context/foundation/lessons.md`):

- [ ] `PluginAssemblyResolver.Install()` at the start of `Execute` (ACadSharp + Geometry.2026 + transitive NuGet DLLs).
- [ ] `[MethodImpl(NoInlining)] RunExport()` separated from `Execute`; outer try/catch logs to `%TEMP%\NavisworksExport.AutoCad.2026.log`.
- [ ] `.csproj` deploy target copies all private `*.dll` and `*.pdb`, excluding `Autodesk.*.dll` (same pattern as `NavisworksExport.Glb.2026.csproj`).
- [ ] Explicit `Reference` to `Autodesk.Navisworks.ComApi` in the AutoCad.2026 project.
- [ ] Pass an optional log delegate into `ExtractGrouped(..., log)` for path/fragment/triangle counts (same diagnostics as GLB `Extract`).
- [ ] Do **not** apply GLB's Z-up→Y-up axis swap or glTF metallic defaults — DWG uses Navisworks world coordinates as-is (`Critical Implementation Details` above).

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds
- Built output folder for AutoCad.2026 contains the plugin DLL plus ACadSharp (and Geometry.2026) dependency DLLs — not only the plugin assembly

#### Manual Verification:

- (Deferred to Phase 4 host matrix — command is exercised live there)

**Implementation Note**: After automated verification, pause before Phase 4 if deploy/elevation concerns need a dry run.

---

## Phase 4: Host verification & roadmap hygiene

### Overview

Prove the end-to-end flow inside Manage 2026 + AutoCAD, and align roadmap status with the 2026-first sequencing decision.

### Changes Required:

#### 1. Live host + AutoCAD matrix

**File**: (no code — verification against deployed `NavisworksExport.AutoCad.2026`)

**Intent**: Confirm FR-004/005/006/007/008 behavior on Manage 2026 with the visual quality bar from research (shaded + per-face color).

**Contract**: Manual matrix below; elevated build/deploy so DLLs actually land under `{NavisworksInstallDir2026}Plugins\NavisworksExport.AutoCad.2026\`.

#### 2. Roadmap status update

**File**: `context/foundation/roadmap.md`

**Intent**: Reflect that AutoCAD proof ships on 2026 first; S-02 remains parked as reverse-port to Manage 2023; S-04 unblocked / in progress or done when this change completes.

**Contract**: Update At-a-glance / S-02 / S-04 / Backlog Handoff / Parked notes so they no longer claim S-04 is blocked on S-02 as a hard prerequisite for starting work. Do not invent a completed S-02.

### Success Criteria:

#### Automated Verification:

- None beyond a clean rebuild after any roadmap-only edits (docs-only is fine)

#### Manual Verification:

- Empty selection → error, no dialog, no file
- **Composite/group selection** (parent node, not individual leaf) → full geometry exports, not a silent subset — confirms `GeometryLeaves` is wired through `ExtractGrouped`
- Geometry-bearing selection → dialog → `.dwg` → success message; file opens in AutoCAD shaded with visible colors; no import errors
- Zero-triangle selection (if exercisable) → same error as empty geometry case; no file
- Cancel dialog → silent abort; no file
- Large/instanced selection does not hang unreasonably (fragment dedup still in effect); multi-mesh chunking does not crash if a fragment is huge
- Source Navisworks document unchanged after export
- Manage 2023 AutoCAD stub still loads and is unaffected
- `roadmap.md` sequencing text matches the 2026-first decision

**Implementation Note**: Phase complete only after human sign-off on the AutoCAD visual criteria.

---

## Testing Strategy

### Unit Tests:

- None — no test project in repo (`AGENTS.md`).

### Integration Tests:

- None automated. `tools/DwgWriterHarness` is the offline integration smoke for the writer.

### Manual Testing Steps:

1. Build harness → open DWG in AutoCAD/TrueView → confirm shaded + multi-color cube.
2. Elevated `dotnet build` AutoCad.2026 → confirm DLLs under Manage 2026 `Plugins\NavisworksExport.AutoCad.2026\`.
3. In Manage 2026: empty selection → error.
4. Select mesh objects → export → open DWG in AutoCAD → shaded colored geometry.
5. Cancel save → no file.
6. Re-open the Navisworks model → confirm unchanged.
7. Optional stress: large instanced selection; confirm completion and openable DWG.

## Performance Considerations

- Fragment Geometry-reference dedup already exists in the extractor — preserve it when implementing `ExtractGrouped`.
- Per-fragment meshes + 32k splits avoid a single giant entity; expect more entities than GLB’s single mesh, which is acceptable for FR-006.
- ACadSharp write cost is secondary to COM `GenerateSimplePrimitives`; no progress UI in MVP.

## Migration Notes

- Sequencing change vs original roadmap: S-04 becomes the AutoCAD proof host; S-02 is a future reverse-port, not a blocker.
- When S-02 returns, prefer linking `DwgWriter.cs` (or moving canonical source under `NavisworksExport.AutoCad/`) the same way Glb.2026 links `GlbWriter.cs` — out of scope here.
- NuGet: first ACadSharp reference in the repo; pin exactly `3.6.35`.

## References

- Related research: `context/changes/export-selection-autocad-2026/research.md`
- Prior research: `context/changes/export-selection-autocad/research.md`
- Pattern to mirror: `context/changes/export-selection-glb/plan.md`, `NavisworksExport.Glb.2026/GlbExportCommand.cs`, `tools/GlbWriterHarness/`
- Recurring pitfalls: `@context/foundation/lessons.md` (S-03 GLB 2026), `AGENTS.md` (Manage 2026 plugin checklist)
- 2026 twin pattern: `context/changes/export-selection-glb-2026/plan-brief.md`
- PRD: FR-004–FR-008, selection-only business rule, Non-Goals (no full BIM metadata, no cloud)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Grouped geometry API

#### Automated

- [x] 1.1 `dotnet build NavisworksExportPlugins.sln` succeeds with new Geometry types — b886e85
- [x] 1.2 Geometry and Geometry.2026 both build; GLB `Extract` call sites still compile — b886e85
- [x] 1.3 No ACadSharp types inside `NavisworksExport.Geometry*` — b886e85

#### Manual

- [x] 1.4 No standalone host check this phase — grouping proven when Phase 2/4 consume `ExtractGrouped` — b886e85

### Phase 2: DWG writer + harness

#### Automated

- [x] 2.1 `dotnet build` AutoCad.2026 succeeds with ACadSharp restore — ff77516
- [x] 2.2 `dotnet build tools/DwgWriterHarness` succeeds — ff77516
- [x] 2.3 Harness writes a non-empty `.dwg` and exits 0 — ff77516

#### Manual

- [x] 2.4 Harness DWG opens shaded/filled in AutoCAD or TrueView (or documented VPORT workaround) — ff77516
- [x] 2.5 Distinct per-face colors visible on the harness cube — ff77516
- [x] 2.6 Harness DWG opens with no import/recovery errors — ff77516

### Phase 3: Command wiring on AutoCad.2026

#### Automated

- [x] 3.1 `dotnet build NavisworksExportPlugins.sln` succeeds
- [x] 3.2 AutoCad.2026 output folder includes plugin + ACadSharp/Geometry dependency DLLs
- [x] 3.3 S-03 checklist landed: assembly resolver, NoInlining RunExport, temp log, ComApi reference, multi-DLL+PDB deploy

#### Manual

- [x] 3.4 Command exercised in Phase 4 host matrix

### Phase 4: Host verification & roadmap hygiene

#### Manual

- [ ] 4.1 Empty selection → error, no dialog, no file
- [ ] 4.2 Composite/group selection exports all child geometry (not selective/partial)
- [ ] 4.3 Geometry-bearing selection → DWG opens in AutoCAD shaded with colors; no import errors
- [ ] 4.4 Zero-triangle selection → error; no file
- [ ] 4.5 Cancel dialog → silent abort; no file
- [ ] 4.6 Large/instanced selection completes; huge-fragment chunking does not crash
- [ ] 4.7 Source Navisworks document unchanged after export
- [ ] 4.8 Manage 2023 AutoCAD stub still loads and is unaffected
- [ ] 4.9 `roadmap.md` reflects 2026-first AutoCAD sequencing (S-02 not a hard blocker)
