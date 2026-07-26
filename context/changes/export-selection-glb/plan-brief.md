# Export Selection to GLB (PowerPoint 3D) — Plan Brief

> Full plan: `context/changes/export-selection-glb/plan.md`

## What & Why

Implement the real "Export to GLB" flow — read the current Navisworks selection, extract its geometry, write a `.glb`, and let PowerPoint render it as an interactive 3D model. This is roadmap `S-01`, the product's north star: the earliest end-to-end proof that "selection instead of screenshots" actually works.

## Starting Point

`NavisworksExport.Glb` is a loadable `net48`/x64 plugin (from `nw-plugin-scaffold`) with a stub command that only reports the selected-item count. No geometry extraction, glTF writing, dialogs, or error handling exist yet. A sibling research doc (`context/changes/export-selection-autocad/research.md`) already confirmed the *only* way to pull triangle geometry out of Navisworks is COM interop (`GenerateSimplePrimitives`), and flagged that path as genuinely shared with the future AutoCAD plugin.

## Desired End State

Clicking "Export to GLB" on a real selection opens a save dialog, writes a `.glb`, and that file opens in PowerPoint (Insert 3D Model) as a correctly oriented, colored, interactively rotatable model — with clear errors instead of a bad file for empty or non-geometric selections, and the source Navisworks document untouched.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Geometry-extraction architecture | New shared `NavisworksExport.Geometry` library, not embedded in the GLB plugin | Sibling research explicitly confirmed this COM/dedup path is real duplication with the future AutoCAD slice, not premature abstraction. |
| Visual fidelity | Geometry + per-vertex/material color | A flat-gray export is a weak proof of value; color is a baseline expectation matched by competitor tools and cheap to add with `eCOLOR`. |
| Fragment dedup | Implemented now, not deferred | Research explicitly warns naive extraction is too slow on real selections with instanced geometry; building it later means redoing the extractor. |
| Scene structure | Single flat mesh (no per-item hierarchy) | Simplest to build/verify; PowerPoint's static Insert-3D-Model viewer doesn't expose per-part interaction anyway. |
| Zero-triangle result handling | Treated as an error, same pattern as empty selection | A selection with no extractable geometry (e.g. annotations only) producing a near-empty file would be a confusing silent failure. |
| GLB library | `SharpGLTF.Toolkit` (NuGet, MIT) | Confirmed net48-compatible, has a direct `MeshBuilder` + vertex-color pattern, no native/COM dependencies. |
| Axis/unit conversion | Z-up→Y-up swap + `UnitConversion.ScaleFactor(...,Meters)` | glTF/PowerPoint require Y-up; Navisworks is Z-up like most BIM hosts — PowerPoint auto-normalizes absolute scale from the bounding box, so getting the axes right matters more than exact real-world units. |

## Scope

**In scope:** shared geometry-extraction library with COM interop + dedup; GLB writer with unit/axis conversion and vertex color; command wiring for empty-selection and zero-triangle errors (FR-007) and save dialog (FR-008); manual verification in Navisworks Manage 2023 + PowerPoint.

**Out of scope:** the AutoCAD/DWG/DXF plugin itself (S-02); BIM property/metadata export; per-item hierarchy/naming in the GLB; a formal test project; performance work beyond fragment dedup; Navisworks 2025 support.

## Architecture / Approach

Two new pieces sit between the existing stub command and the file on disk: `NavisworksExport.Geometry` (COM selection → world-space colored triangles, format-agnostic) feeds a new `GlbWriter` in `NavisworksExport.Glb` (triangles → unit/axis-converted glTF mesh → `.glb` via SharpGLTF), which `GlbExportCommand` orchestrates behind a save dialog and error checks.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Shared geometry-extraction library | COM selection → world-space colored triangles, with dedup | Dedup key/transform ordering is community-sourced, not officially documented — needs care during implementation |
| 2. GLB writer | Units/axis-converted, colored, single-mesh `.glb` via SharpGLTF | Axis-swap sign error flips normals/winding — verify with a hardcoded triangle before trusting real data |
| 3. Command wiring | Real FR-007/FR-008 behavior on `GlbExportCommand` | Easy to forget the "non-empty selection, zero triangles" edge case distinct from "empty selection" |
| 4. Host verification | Confirmed live in Navisworks 2023 + PowerPoint | Manual-only signal — must actually be run with a real model and real PowerPoint, not assumed |

**Prerequisites:** `nw-plugin-scaffold` (F-01) deployed and loading; Navisworks Manage 2023 with a real model available; PowerPoint (Office 365) available to verify Insert 3D Model; elevated terminal/IDE for the post-build deploy step.
**Estimated effort:** ~2-3 sessions across 4 phases (COM extraction + coordinate conversion are the parts most likely to need iteration).

## Open Risks & Assumptions

- The exact fragment `path`-array dedup key is sourced from Autodesk Community reports, not official documentation — the precise accessor/API shape needs to be verified against the installed Navisworks 2023 API during implementation.
- Assumes PowerPoint's auto-scale-normalization means exact real-world unit accuracy isn't required for this proof — if that assumption is wrong, revisit unit handling before declaring S-01 done.
- Assumes a "reasonably large" selection for the Phase 4 performance check, since the PRD sets no formal size/latency target.

## Success Criteria (Summary)

- A real Navisworks selection exports to `.glb` and opens in PowerPoint as an interactive, correctly oriented, colored 3D model.
- Empty selections and selections with no extractable geometry show a clear error instead of a bad or empty file.
- The source Navisworks document is unchanged after export, in every tested path.
