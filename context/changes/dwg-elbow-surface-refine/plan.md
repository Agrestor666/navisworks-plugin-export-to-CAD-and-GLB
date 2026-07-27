# DWG Elbow Surface Refine Implementation Plan

## Overview

Formalize elbow / curved-surface smoothing on the Manage 2026 AutoCAD export path so pipe bends in DWG look like smooth curves under AutoCAD flat shading — not polyhedral prisms. The Phong-style `CurvedSurfaceRefiner` is already wired always-on inside `DwgWriter.WriteDwg`; this change locks automated acceptance, proves visual quality on real Manage 2026 selections, and guards the host-load valuetype regression that previously hid the entire AutoCAD plugin.

## Current State Analysis

- Export flow: selection → `SelectionGeometryExtractor.ExtractGrouped` → `DwgWriter.WriteDwg` → **`CurvedSurfaceRefiner.Refine`** → colored `PolyfaceMesh` + Gouraud VPORT (`AutoCadExportCommand.cs`, `DwgWriter.cs:72`).
- Navisworks supplies tessellated triangles + normals only — no NURBS/arcs; smoothness in DWG = denser geometry (`research.md`).
- Defaults already in code: `SplitAngleDegrees = 10`, `MaxPasses = 4`, `TriangleBudget = 600_000`, `ProjectionWeight = 0.5` (`CurvedSurfaceRefiner.cs`).
- Harness already fails if refined elbow neighbor facets exceed `MaxElbowFacetDegrees = 12` (`tools/DwgWriterHarness/Program.cs`).
- Nested `Midpoint`/`Corner` are primitive-only so host `GetTypes` does not throw `ReflectionTypeLoadException` (`lessons.md`, `CurvedSurfaceRefiner.cs`).
- Diagnostic scripts exist for bare `GetTypes` on the deployed AutoCAD DLL (`tools/diag-bare-gettypes-bin.ps1`, `tools/diag-no-resolver.ps1`).

## Desired End State

A BIM coordinator exporting a selection with pipe elbows from Manage 2026 gets a DWG that, when opened shaded in AutoCAD, shows bends as visually smooth (not stacked prism panels), while straight pipe remains sane in wireframe/shaded view. Automated harness continues to enforce ≤12° max neighbor facet angle on the synthetic coarse elbow. The AutoCAD 2026 plugin remains loadable (ribbon present; bare `GetTypes` succeeds). Refine stays always-on with current knobs unless Phase 2 visual acceptance forces a documented tune.

**Verification**: `dotnet build` + `DwgWriterHarness` exit 0; elevated deploy; bare `GetTypes` OK; host export of elbow / pipe / mixed opens shaded in AutoCAD looking acceptably smooth; roadmap S-05 → done when archived or when this change closes.

### Key Discoveries:

- Refine call site is already production: `DwgWriter.cs:72` — do not add a parallel refine path in the command or Geometry.
- GLB must stay untouched — it shades via vertex normals; densifying there is out of scope (`research.md` Architecture Insights).
- Planning decisions (user): harness + real visual acceptance; keep defaults until host fails; always-on refine; host checks = elbow + straight pipe + mixed + ribbon/plugin load.

## What We're NOT Doing

- Reconstructing true CAD curves (`ARC` / `SWEEP` / torus fit) or `3DSOLID` / ACIS (locked by S-04).
- GLB / PowerPoint refine path.
- UI or config toggles to disable/tune refine (always-on; knobs stay compile-time constants unless Phase 2 forces a code tune).
- Manage 2023 reverse-port (S-02) — when it returns, refine travels with `DwgWriter`, not a separate 2023 algorithm.
- Full S-04 regression suite re-run (empty selection, cancel, huge selection, etc.) — already archived; this slice adds elbow-focused host checks only.
- New test project / CI runner — harness + manual host/AutoCAD only (`AGENTS.md`).
- Changing `CreaseAngleDegrees` / wireframe seam policy unless a Phase 2 visual failure clearly requires it.

## Implementation Approach

1. **Lock the automated bar** — treat harness elbow ≤12° and always-on refine as the S-05 automated contract; document locked knobs; run bare `GetTypes` as the host-load non-regression check after any touch to `CurvedSurfaceRefiner`.
2. **Host visual acceptance** — export representative elbow, straight pipe, and mixed selection from Manage 2026; open in AutoCAD shaded; confirm ribbon still shows the AutoCAD command.
3. **Conditional tune only** — if elbows still look prismatic (or flats look wrong / DWG explodes), adjust `CurvedSurfaceRefiner` constants, re-run harness + GetTypes, then re-check host. Do not pre-tune “just in case.”
4. **Close the slice** — update `roadmap.md` S-05 status when acceptance lands (implementer / archive step).

## Critical Implementation Details

### Nested valuetypes must stay primitive-only

Any edit to `CurvedSurfaceRefiner` that puts `Geometry.Vec3` / `Rgba` fields back on nested structs will make Manage 2026 drop the plugin at startup (`ReflectionTypeLoadException`). Rebuild `Vec3`/`Rgba` only at use sites. After such edits, run bare `GetTypes` (`tools/diag-bare-gettypes-bin.ps1` or equivalent) against the built/deployed AutoCAD 2026 DLL before claiming Phase 1/2 done.

### Tune is gated by Phase 2 visual failure

Do not change `SplitAngleDegrees` / `MaxPasses` / `TriangleBudget` / `ProjectionWeight` in Phase 1. Phase 2 may change them only with a one-line rationale in the PR/commit (what looked wrong, which knob moved, harness still green).

---

## Phase 1: Automated gate & invariants

### Overview

Make the existing harness the explicit automated acceptance gate for elbow smoothness, confirm always-on refine + locked default knobs, and prove the AutoCAD 2026 assembly still survives bare `GetTypes` (host-load invariant for this feature).

### Changes Required:

#### 1. Harness acceptance contract

**File**: `tools/DwgWriterHarness/Program.cs`

**Intent**: Keep `MaxElbowFacetDegrees = 12` and `CheckElbowSmoothness` as the hard fail for under-refined elbows. Only clarify comments/log strings if needed so the 12° bar is obviously the S-05 automated contract — no algorithm change required if already failing correctly.

**Contract**: Harness exit non-zero when refined elbow max neighbor facet angle > 12°. Synthetic elbow remains deliberately coarse (`ElbowSegments = 8`, `ElbowSteps = 6`). Path under test remains `DwgWriter.WriteDwg` (refine included).

#### 2. Knob lock documentation in refiner

**File**: `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs`

**Intent**: Document that current constants are the S-05 defaults and must not be changed without failing host visual acceptance (and re-running harness). No behavior change unless a later Phase 2 tune is authorized by visual failure.

**Contract**: Constants remain `SplitAngleDegrees = 10`, `MaxPasses = 4`, `TriangleBudget = 600_000`, `ProjectionWeight = 0.5`, `WeldEpsilon = 1e-6` after Phase 1. Nested `Midpoint`/`Corner` stay primitive-only.

#### 3. Host-load GetTypes check (tooling, no product UI)

**File**: `tools/diag-bare-gettypes-bin.ps1` (or `tools/diag-no-resolver.ps1` — use existing)

**Intent**: Use the existing diagnostic as the automated-ish non-regression step after building `NavisworksExport.AutoCad.2026` — no new product feature.

**Contract**: Bare `LoadFile` + `GetTypes` on `NavisworksExport.AutoCad.2026.dll` reports success (no `ReflectionTypeLoadException`). Document the command in phase success criteria; do not invent a second resolver path.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build NavisworksExportPlugins.sln`
- Harness passes (exit 0): `dotnet run --project tools/DwgWriterHarness/DwgWriterHarness.csproj` (or build + run exe equivalent)
- Bare GetTypes on AutoCAD 2026 plugin DLL succeeds via existing `tools/diag-*.ps1`

#### Manual Verification:

- Harness console shows elbow check OK (facets ≤12°) and pipe wireframe axial edges still visible (existing pipe check still green)

**Implementation Note**: After Phase 1 automated verification passes, pause for human confirmation of the harness console output before Phase 2.

---

## Phase 2: Host visual acceptance (+ conditional tune)

### Overview

Prove real Manage 2026 elbows look acceptably smooth in AutoCAD shaded view; confirm straight pipe and mixed selections; confirm the AutoCAD plugin still appears on the ribbon. Tune `CurvedSurfaceRefiner` knobs only if visual acceptance fails; then update roadmap S-05.

### Changes Required:

#### 1. Elevated deploy of AutoCAD 2026 plugin

**File**: `NavisworksExport.AutoCad.2026/NavisworksExport.AutoCad.2026.csproj` (deploy target only if broken — prefer no csproj churn)

**Intent**: Ensure current plugin + private DLLs are deployed under `{NavisworksInstallDir2026}Plugins\NavisworksExport.AutoCad.2026\` so host tests the same bits as the build.

**Contract**: Elevated build/deploy succeeds; ribbon shows Export to AutoCAD; no Message Center `ReflectionTypeLoadException` for this assembly.

#### 2. Conditional knob tune (only if visual fails)

**File**: `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs`

**Intent**: If host elbows still read as prism panels (or over-refine / bad flats), adjust the smallest set of constants needed, preserving primitive-only nested layouts.

**Contract**: If no visual failure → **no code change**. If tune required → harness still ≤12°, GetTypes still OK, commit message states which knob moved and why. Refine remains always called from `DwgWriter.WriteDwg` (no skip flag).

#### 3. Roadmap S-05 closure hygiene

**File**: `context/foundation/roadmap.md`

**Intent**: When host acceptance is confirmed, mark S-05 done in the glance table and detail section (and Done list when archiving), consistent with prior slices.

**Contract**: S-05 status reflects acceptance; unknowns about thresholds closed or noted as “defaults accepted on real models.” Do not reopen S-04/S-02 scope.

### Success Criteria:

#### Automated Verification:

- After any Phase 2 code tune: `dotnet build NavisworksExportPlugins.sln` succeeds
- After any Phase 2 code tune: harness exit 0 and bare GetTypes OK
- If no tune: prior Phase 1 automated results still stand (re-run harness once after deploy build)

#### Manual Verification:

- Manage 2026: AutoCAD export command visible on ribbon (plugin loaded)
- Export selection with **elbow / bend** → AutoCAD shaded: bend looks smooth (not stacked prism panels)
- Export **straight pipe** → shaded OK; wireframe does not erase the barrel (crease policy still sane)
- Export **mixed** selection (elbow + other geometry) → opens without errors; elbows smooth; no obvious one-sided refine artifacts
- Source Navisworks document unchanged after exports

**Implementation Note**: Pause after manual host/AutoCAD checks before marking the change implemented / archiving.

---

## Testing Strategy

### Unit Tests:

- None — no test project (`AGENTS.md`). Harness is the automated stand-in.

### Integration Tests:

- `tools/DwgWriterHarness` — synthetic pipe + coarse elbow + cube through full `DwgWriter.WriteDwg` (includes refine).

### Manual Testing Steps:

1. Build + run harness; confirm elbow ≤12° and pipe axial edges visible.
2. Run bare GetTypes diagnostic on AutoCAD 2026 DLL.
3. Elevated deploy; restart Manage 2026; confirm AutoCAD export on ribbon.
4. Export elbow selection → open DWG in AutoCAD (Gouraud/shaded) → accept smoothness.
5. Export straight pipe → shaded + quick wireframe sanity.
6. Export mixed selection → open + visual scan.
7. If any of 4–6 fail on smoothness/cost → tune knobs → re-run 1–6.

## Performance Considerations

- Always-on refine can grow triangle counts (budget 600k). Accept denser DWGs for curved selections; do not raise budget preemptively.
- If Phase 2 shows intolerably large/slow exports, prefer lowering passes or raising split angle slightly over removing refine.

## Migration Notes

- No data migration. Existing deployed plugins need elevated rebuild/deploy to pick up any Phase 2 tune.
- S-02 reverse-port later must carry `CurvedSurfaceRefiner` with `DwgWriter` — not reimplemented separately.

## References

- Related research: `context/changes/dwg-elbow-surface-refine/research.md`
- Prior AutoCAD proof: `context/archive/2026-07-26-export-selection-autocad-2026/`
- Lessons: `context/foundation/lessons.md` (nested valuetypes / AssemblyResolve)
- Call site: `NavisworksExport.AutoCad.2026/DwgWriter.cs:72`
- Refiner: `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs`
- Harness bar: `tools/DwgWriterHarness/Program.cs` (`MaxElbowFacetDegrees = 12`)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Automated gate & invariants

#### Automated

- [x] 1.1 Solution builds: `dotnet build NavisworksExportPlugins.sln` — db25ad8
- [x] 1.2 Harness passes (exit 0): `dotnet run --project tools/DwgWriterHarness/DwgWriterHarness.csproj` — db25ad8
- [x] 1.3 Bare GetTypes on AutoCAD 2026 plugin DLL succeeds via existing `tools/diag-*.ps1` — db25ad8

#### Manual

- [x] 1.4 Harness console shows elbow check OK (facets ≤12°) and pipe wireframe axial edges still visible — db25ad8

### Phase 2: Host visual acceptance (+ conditional tune)

#### Automated

- [x] 2.1 After any Phase 2 code tune: `dotnet build NavisworksExportPlugins.sln` succeeds
- [x] 2.2 After any Phase 2 code tune: harness exit 0 and bare GetTypes OK
- [x] 2.3 If no tune: re-run harness once after deploy build (Phase 1 results still stand)

#### Manual

- [x] 2.4 Manage 2026: AutoCAD export command visible on ribbon (plugin loaded)
- [x] 2.5 Export elbow/bend → AutoCAD shaded: bend looks smooth (not stacked prism panels)
- [x] 2.6 Export straight pipe → shaded OK; wireframe barrel not erased
- [x] 2.7 Export mixed selection → opens; elbows smooth; no one-sided refine artifacts
- [x] 2.8 Source Navisworks document unchanged after exports
