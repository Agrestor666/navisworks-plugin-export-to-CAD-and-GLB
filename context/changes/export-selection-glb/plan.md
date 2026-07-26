# Export Selection to GLB (PowerPoint 3D) Implementation Plan

## Overview

Implement the real "Export to GLB" flow on top of the `nw-plugin-scaffold` (F-01) host: read the current Navisworks selection, extract its triangle geometry via the COM API, convert it into a glTF/GLB scene, and let the user save it — proving the north-star hypothesis (selection → GLB → interactive 3D in PowerPoint) end to end.

## Current State Analysis

`NavisworksExport.Glb` is a loadable `net48`/x64 `AddInPlugin` (`GlbExportCommand`) that only reports the selected-item count in a message box — no geometry extraction, no file writing, no error/dialog handling exists yet. `NavisworksExport.AutoCad` is the sibling plugin, currently an identical stub. Neither project references any geometry, glTF, or CAD-writing library yet. The repo has no shared library between the two plugins (an explicit, revisited decision from F-01 — see Key Discoveries).

## Desired End State

Clicking "Export to GLB" in Navisworks Manage 2023 with a non-empty, geometry-bearing selection opens a save dialog, writes a `.glb` file containing that selection's geometry (with color) in the correct orientation/scale, and inserting that file into PowerPoint via Insert 3D Model renders an interactive, correctly oriented, colored 3D model. Empty selections, and selections that yield no extractable geometry, show a clear error instead of writing a file. The source Navisworks document is never modified.

**Verification**: build the solution, load the plugin in Navisworks Manage 2023, run it against a real selection, and open the resulting `.glb` in PowerPoint (Insert → 3D Models → This Device) to confirm it renders and rotates correctly.

### Key Discoveries:

- The only confirmed way to pull triangle geometry out of Navisworks is COM interop, not the .NET API: `Autodesk.Navisworks.Api.ComApi.ComApiBridge.ToInwOpSelection(selection)` → `path.Fragments()` → `frag.GenerateSimplePrimitives(nwEVertexProperty.eNORMAL | eCOLOR, callback)` with a `InwSimplePrimitivesCB` callback (`context/changes/export-selection-autocad/research.md:65-91`).
- That same research explicitly flags this extraction path as shared, real duplication between this slice and the future AutoCAD slice — not premature abstraction (`context/changes/export-selection-autocad/research.md:128,134`), which is why this plan extracts it into its own library now rather than embedding it in `NavisworksExport.Glb`.
- Navisworks shares underlying geometry data across `ModelItem` instances that are geometric copies of each other; naively calling `GenerateSimplePrimitives` once per fragment re-extracts identical data repeatedly and is reported as CPU-intensive on real selections. Community-confirmed mitigation: cache already-extracted local-space triangle data keyed by each fragment's COM `path` array, and only re-apply the per-instance local-to-world transform for repeat occurrences (Autodesk Community "Faster primitive data extraction" thread).
- PowerPoint's Insert-3D-Model viewer auto-normalizes display scale from the model's bounding box (`meterPerModelUnit`, computed as `1 / (2 * maxExtentOnAnyAxis)`), so exact real-world unit accuracy isn't load-bearing for the initial proof — but the model **must** be authored Y-up (Navisworks/most BIM/CAD systems are Z-up) and **must not** use Draco compression, or PowerPoint fails to read it (Microsoft 3D Content Guidelines; SOLIDWORKS/PowerPoint GLB export guidance).
- `SharpGLTF` (`SharpGLTF.Toolkit` NuGet, MIT) writes GLB and targets `netstandard2.0`, compatible with `net48`; `MeshBuilder<VertexPosition, VertexColor1, VertexEmpty>` + `SceneBuilder.AddRigidMesh(...).ToGltf2().SaveGLB(...)` is the toolkit's supported pattern for a colored mesh (Context7 `/vpenades/sharpgltf`).
- The `NavisworksAPIdlls2023` NuGet package (already used as the compile-time fallback in both existing `.csproj` files when Navisworks isn't installed locally) includes `Autodesk.Navisworks.ComApi.dll` and `Autodesk.Navisworks.Interop.ComApi.dll` alongside `Autodesk.Navisworks.Api.dll`, so the new geometry library can follow the exact same HintPath/NuGet-fallback pattern already established in `NavisworksExport.Glb.csproj:15-30`.
- `UnitConversion.ScaleFactor(Application.ActiveDocument.Units, Units.Meters)` gives the scalar needed to convert Navisworks document units to meters for the glTF output.

## What We're NOT Doing

- Anything in the AutoCAD/DWG/DXF plugin (`export-selection-autocad`, S-02) — the geometry library is designed to be format-agnostic so S-02 can consume it later, but wiring it up is out of scope for this plan.
- Full BIM property/metadata export — geometry + color only, per PRD Non-Goals.
- Preserving per-item hierarchy/naming in the GLB (flat single mesh only, per the confirmed scene-structure decision).
- A `.NET` unit-test project — no test project exists in the repo yet (`AGENTS.md`: "No test project or runner is configured"); verification for this plan is manual, as established in F-01.
- Progress/cancel UI, large-model chunking/streaming, or any performance work beyond the fragment dedup called out above.
- Navisworks 2025 support, batch export, or any cloud/backend behavior (PRD Non-Goals).

## Implementation Approach

Split the work into a reusable extraction layer and a GLB-specific writer, wired together by the command:

1. **`NavisworksExport.Geometry`** (new `net48`/x64 class library) — COM selection → world-space triangles with per-vertex/fallback color, with fragment dedup. Output is a plain, glTF/DWG-agnostic DTO.
2. **GLB writer** (in `NavisworksExport.Glb`) — consumes that DTO, applies the Navisworks→glTF unit and axis conversion, builds a single flat colored mesh via SharpGLTF, saves `.glb`.
3. **Command wiring** (`GlbExportCommand`) — empty-selection and zero-triangle-result error handling (FR-007), `SaveFileDialog` (FR-008), orchestrates 1→2, reports success/failure.
4. **Host verification** — build, deploy, manually verify in Navisworks Manage 2023 + PowerPoint.

This order front-loads the two riskiest, least-familiar pieces (COM extraction, coordinate conversion) before touching UI wiring, so problems surface while the surface area is still small.

## Critical Implementation Details

### Fragment dedup key and transform ordering

Navisworks shares geometry across instanced `ModelItem`s; the same underlying triangle data can appear behind many different fragments with different transforms. Cache extracted **local-space** triangles (pre-transform) keyed by each fragment's COM `path` array (`((Array)frag.path.GetData())` or equivalent — verify exact accessor against the installed API version), so `GenerateSimplePrimitives` runs once per unique geometry. Apply `frag.GetLocalToWorldMatrix()` **after** the cache lookup, per occurrence — the transform is instance-specific even when the geometry is shared, so caching must happen before the transform is applied, not after.

### Navisworks→glTF axis conversion

Navisworks (like most BIM/CAD hosts) is Z-up; glTF/PowerPoint require Y-up. Convert each world-space vertex as `gltf = (nw.X, nw.Z, -nw.Y) * unitsToMetersScale` (rotates Z-up into Y-up while preserving a right-handed system). Getting the sign wrong on the swapped axis flips triangle winding and inverts normals — verify visually in a viewer (Phase 2) before wiring the full command, not only at the end in PowerPoint.

## Phase 1: Shared geometry-extraction library

### Overview

Create `NavisworksExport.Geometry`, a new class library that turns a Navisworks selection into world-space triangle geometry with color, encapsulating the COM interop and dedup logic. No GLB/glTF-specific code lives here.

### Changes Required:

#### 1. New project scaffolding

**File**: `NavisworksExport.Geometry/NavisworksExport.Geometry.csproj`

**Intent**: A `net48`/x64 SDK-style class library mirroring the existing plugin projects' reference pattern, but referencing `Autodesk.Navisworks.Api.dll` **and** `Autodesk.Navisworks.ComApi.dll` + `Autodesk.Navisworks.Interop.ComApi.dll` (both host-local via `HintPath` when present, both covered by the `NavisworksAPIdlls2023` NuGet fallback otherwise). This project is a plain library, not a plugin — no `AddInPlugin`, no post-build deploy target.

**Contract**: Same conditional `HintPath`/`PackageReference` pattern as `NavisworksExport.Glb.csproj:15-30`, extended to the two extra COM assemblies. Add the project to `NavisworksExportPlugins.sln`.

#### 2. Extraction data model

**File**: `NavisworksExport.Geometry/ExtractedTriangle.cs`

**Intent**: A plain, output-format-agnostic DTO — three world-space vertex positions (in the source document's units, unconverted) plus a per-vertex RGBA color (falling back to the fragment's `Appearance` material color when per-vertex color isn't available). No glTF or DWG types leak into this file.

**Contract**: One type (or small set of types) representing "one triangle, world-space, with color" — the exact member layout is an implementation detail; keep it independent of any writer library so it stays reusable if `export-selection-autocad` consumes it later.

#### 3. COM primitive callback

**File**: `NavisworksExport.Geometry/PrimitiveCallback.cs`

**Intent**: Implements `InwSimplePrimitivesCB`; its `Triangle(v1, v2, v3)` method receives local-space vertices (with `eNORMAL | eCOLOR` data) from `GenerateSimplePrimitives` and forwards them to the extractor's per-fragment accumulator, to be transformed and cached per the dedup rule in Critical Implementation Details.

**Contract**: Implements the 4-method `InwSimplePrimitivesCB` interface (`Triangle`, `Line`, `Point`, `SnapPoint`); only `Triangle` is meaningful for this MVP (lines/points/snap points are ignored — no geometry from non-mesh primitives, per the confirmed scope).

#### 4. Extractor entry point

**File**: `NavisworksExport.Geometry/SelectionGeometryExtractor.cs`

**Intent**: The public API of this library — takes a `ModelItemCollection` (the current selection), converts it to a COM selection via `ComApiBridge.ToInwOpSelection`, walks `path.Fragments()`, applies the dedup cache + local-to-world transform, and returns the flattened list of `ExtractedTriangle`s across the whole selection.

**Contract**: One public method, e.g. `IReadOnlyList<ExtractedTriangle> Extract(ModelItemCollection selection)`. Never mutates the Navisworks document or its selection (read-only, per the PRD guardrail — this is naturally true since only COM read APIs are called).

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds with the new project added to the `.sln`
- `NavisworksExport.Geometry` builds as `net48`/x64 and resolves `Autodesk.Navisworks.Api`, `Autodesk.Navisworks.ComApi`, `Autodesk.Navisworks.Interop.ComApi` via the same HintPath/NuGet-fallback pattern as the existing plugin projects
- `NavisworksExport.Glb` still builds after Phase 3 adds a project reference to `NavisworksExport.Geometry` (checked here as a forward-looking build gate)

#### Manual Verification:

- No standalone manual verification for this phase — the extractor has no UI hook yet; it is exercised together with Phase 4's host verification (extraction correctness is observed indirectly through the Phase 2/3 GLB output and Phase 4's real-model test)

---

## Phase 2: GLB writer

### Overview

Turn the extractor's output into an actual `.glb` file: convert units and axes, build a single flat colored mesh, save via SharpGLTF.

### Changes Required:

#### 1. GLB writer

**File**: `NavisworksExport.Glb/GlbWriter.cs`

**Intent**: Takes the `ExtractedTriangle` list plus the source document's unit-to-meters scale factor, applies the Navisworks→glTF axis conversion (see Critical Implementation Details), builds one flat `MeshBuilder<VertexPosition, VertexColor1, VertexEmpty>` with all triangles as a single primitive, wraps it in a `SceneBuilder`, and saves the result as `.glb` to the given file path.

**Contract**: One public method, e.g. `void WriteGlb(IReadOnlyList<ExtractedTriangle> triangles, double unitsToMetersScale, string filePath)`. Uses `SharpGLTF.Toolkit`'s `MeshBuilder`/`SceneBuilder`/`ModelRoot.SaveGLB` — no Draco compression is ever enabled (SharpGLTF does not enable it by default; do not add any compression extension).

#### 2. Package reference

**File**: `NavisworksExport.Glb/NavisworksExport.Glb.csproj`

**Intent**: Add the `SharpGLTF.Toolkit` NuGet package reference needed by `GlbWriter`, plus a project reference to `NavisworksExport.Geometry`.

**Contract**: Standard `<PackageReference>` and `<ProjectReference>` entries.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds with `SharpGLTF.Toolkit` restored and the new `ProjectReference` resolved

#### Manual Verification:

- Feed `GlbWriter` a small hardcoded triangle list (e.g. a unit cube with distinct per-face colors) via a throwaway local harness (no Navisworks host needed for this check), open the resulting `.glb` in a standalone glTF viewer or directly in PowerPoint, and confirm: the model is upright (Y-up, not lying on its side), colors render per-face/vertex as expected, and it opens without the "content not supported" error that Draco compression would cause

---

## Phase 3: Command wiring

### Overview

Replace the `GlbExportCommand` stub with the real flow: validate the selection, prompt for a save location, run extraction + writing, and report the result — implementing FR-007 and FR-008.

### Changes Required:

#### 1. Command implementation

**File**: `NavisworksExport.Glb/GlbExportCommand.cs`

**Intent**: On `Execute`, check the current selection is non-empty (FR-007: empty selection → error message, no dialog, no file). If non-empty, show a `SaveFileDialog` filtered to `*.glb` with a sensible default filename (FR-008). Run `SelectionGeometryExtractor.Extract`; if it yields zero triangles from a non-empty selection (e.g. only lines/annotations/collapsed nodes with no mesh geometry), show the same error pattern as the empty-selection case rather than writing a near-empty file (confirmed decision). Otherwise read the document's unit scale via `UnitConversion.ScaleFactor(...)`, call `GlbWriter.WriteGlb`, and show a success message box; on cancel (user dismisses the save dialog) return quietly with no error. Wrap extraction/writing in a `try/catch` so any COM or I/O failure surfaces as a message box instead of crashing the host add-in.

**Contract**: Keep the existing `Execute(params string[] parameters) : int` signature and 0-success / non-zero-error return convention already used by the stub. The command only ever reads from `Application.ActiveDocument` — never calls any Navisworks write/mutate API, preserving the read-only guardrail.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds

#### Manual Verification:

- Empty selection → error message box, no save dialog shown, no file written
- Non-empty, geometry-bearing selection → save dialog appears, `.glb` written to the chosen path, success message shown
- Selection with zero extractable triangles (e.g. only annotation/line/collapsed nodes) → same error pattern as the empty-selection case, no file written
- Canceling the save dialog aborts silently — no error, no file written
- Source Navisworks document is unchanged after export (guardrail)

---

## Phase 4: Host verification

### Overview

Confirm the whole flow works live in Navisworks Manage 2023 against a real model, and that the resulting file actually renders as an interactive 3D model in PowerPoint — the actual north-star proof.

### Changes Required:

#### 1. Manual host + PowerPoint verification

**File**: n/a (verification-only phase)

**Intent**: Build and deploy both projects (elevated terminal/IDE, per the existing post-build deploy target), load Navisworks Manage 2023, run "Export to GLB" against a real selection from a real model, and confirm the output opens correctly in PowerPoint (Insert → 3D Models → This Device).

**Contract**: n/a.

### Success Criteria:

#### Automated Verification:

- `dotnet build NavisworksExportPlugins.sln` succeeds (full-solution final check)

#### Manual Verification:

- "Export to GLB" appears under Navisworks Manage 2023's Add-Ins tab and runs against a real selection from a real model
- Resulting `.glb` inserts into PowerPoint via Insert 3D Model and renders as an interactive, correctly oriented (upright), colored model that can be rotated/zoomed
- A reasonably large selection (dozens–hundreds of items with repeated/instanced geometry) extracts and exports without hanging, confirming the dedup strategy is working
- Original Navisworks document is unchanged after the full run (guardrail)

---

## Testing Strategy

### Unit Tests:

- None planned — no test project exists in the repo yet (`AGENTS.md`). The axis/unit conversion in `GlbWriter` is a pure function and would be a good first candidate if a test project is introduced later.

### Integration Tests:

- None (manual verification only, per F-01's established approach).

### Manual Testing Steps:

1. Build the solution in an elevated terminal/IDE so the post-build deploy step copies both DLLs into the Navisworks Plugins folder.
2. Open Navisworks Manage 2023 with a real model, select nothing, run "Export to GLB" → confirm error message, no dialog.
3. Select a group of objects with real mesh geometry, run "Export to GLB" → confirm save dialog, pick a location, confirm success message and that the `.glb` file exists on disk.
4. Open PowerPoint, Insert → 3D Models → This Device → select the `.glb` → confirm it renders upright, colored, and is interactively rotatable/zoomable.
5. Select a group with instanced/repeated geometry (e.g. an array of identical fittings) large enough to notice a performance difference, run the export, and confirm it completes without a long hang.
6. Select an item that has no mesh geometry (e.g. a collapsed grouping node or an annotation-only item), run "Export to GLB" → confirm the zero-triangle error path, not a near-empty file.
7. Re-open the original Navisworks model (or check it in-session) and confirm nothing about the source document changed.

## Performance Considerations

`GenerateSimplePrimitives` is reported as CPU-intensive on real selections; the fragment dedup cache (Critical Implementation Details) is the mitigation built into this plan. No further performance work (streaming, chunked export, background thread/progress UI) is in scope — if Phase 4's large-selection manual test shows unacceptable hang time, that becomes a follow-up change, not a blocker for this plan.

## Migration Notes

N/A — no existing data or prior export format to migrate from; this is the first working version of GLB export.

## References

- Related research: `context/changes/export-selection-autocad/research.md` (COM extraction path, shared-duplication observation, dedup performance warning)
- Prior foundation decision: `context/archive/2026-07-26-nw-plugin-scaffold/plan-brief.md` ("no shared core yet" — revisited here now that real duplication has surfaced, per the research doc's own note)
- Existing reference pattern: `NavisworksExport.Glb/NavisworksExport.Glb.csproj:15-30` (HintPath/NuGet-fallback convention to mirror in the new project)
- Existing stub to replace: `NavisworksExport.Glb/GlbExportCommand.cs`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Shared geometry-extraction library

#### Automated

- [x] 1.1 `dotnet build NavisworksExportPlugins.sln` succeeds with the new project added to the `.sln` — 6f72f9f
- [x] 1.2 `NavisworksExport.Geometry` builds as net48/x64 and resolves the three Navisworks assemblies via HintPath/NuGet fallback — 6f72f9f
- [x] 1.3 `NavisworksExport.Glb` still builds after the Phase 3 project reference is added — 6f72f9f

#### Manual

- [x] 1.4 No standalone check — exercised together with Phase 4 — 6f72f9f

### Phase 2: GLB writer

#### Automated

- [x] 2.1 `dotnet build NavisworksExportPlugins.sln` succeeds with `SharpGLTF.Toolkit` restored and the new `ProjectReference` resolved — b6d068e

#### Manual

- [x] 2.2 Hardcoded triangle list round-trips through `GlbWriter` and opens upright, colored, uncompressed in a viewer/PowerPoint — b6d068e

### Phase 3: Command wiring

#### Automated

- [x] 3.1 `dotnet build NavisworksExportPlugins.sln` succeeds — e10ee53

#### Manual

- [x] 3.2 Empty selection → error, no dialog, no file — e10ee53
- [x] 3.3 Non-empty selection → dialog, file written, success message — e10ee53
- [x] 3.4 Zero-triangle non-empty selection → same error pattern as empty selection — e10ee53
- [x] 3.5 Save dialog cancel → silent abort, no error, no file — e10ee53
- [x] 3.6 Source document unchanged after export — e10ee53

### Phase 4: Host verification

#### Automated

- [x] 4.1 `dotnet build NavisworksExportPlugins.sln` succeeds (final full-solution check)

#### Manual

- [x] 4.2 "Export to GLB" appears under Add-Ins and runs against a real selection
- [x] 4.3 Resulting `.glb` renders correctly and interactively in PowerPoint
- [x] 4.4 Large/instanced selection exports without hanging
- [x] 4.5 Original document unchanged after the full run
