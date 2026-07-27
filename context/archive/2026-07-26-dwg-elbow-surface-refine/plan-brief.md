# DWG Elbow Surface Refine — Plan Brief

> Full plan: `context/changes/dwg-elbow-surface-refine/plan.md`
> Research: `context/changes/dwg-elbow-surface-refine/research.md`

## What & Why

AutoCAD flat-shades `PolyfaceMesh`, so coarse Navisworks elbows look like prisms. We already densify curved surfaces with always-on `CurvedSurfaceRefiner` before DWG write; this slice locks acceptance (harness + real host) and prevents host-load regressions.

## Starting Point

Manage 2026 AutoCAD export works end-to-end. Refine runs at `DwgWriter.WriteDwg` line 72 with defaults 10° / 4 passes / 600k tris / Phong 0.5. Harness already fails elbows above 12° neighbor facet angle.

## Desired End State

User exports pipe bends from Manage 2026 and opens a shaded DWG where elbows look smoothly curved; straight pipe stays sane; AutoCAD plugin remains on the ribbon. Defaults kept unless host visual forces a documented tune.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| -------- | ------ | ---------------- | ------ |
| Acceptance bar | Harness ≤12° + visual on real elbows | Harness alone can miss BIM tessellation quirks | Plan |
| Knob strategy | Keep defaults; tune only if host fails | Seed already harness-green; avoid blind over-refine | Plan |
| Refine mode | Always on (status quo) | No UI; elbows never regress via a forgotten flag | Plan / Research |
| Host verification | Elbow + pipe + mixed + ribbon load | Covers under/over-refine and GetTypes lesson | Plan |
| Entity / curves | Stay on denser `PolyfaceMesh` | No NURBS from NW; S-04 locked mesh-not-solid | Research |
| GLB refine | Out of scope | GLB has vertex normals; DWG-only MVP | Research |

## Scope

**In scope:** Harness gate clarity, knob lock docs, GetTypes non-regression, host visual (elbow/pipe/mixed/ribbon), conditional tune, roadmap S-05 hygiene.

**Out of scope:** True CAD curves / `3DSOLID`, GLB refine, UI toggles, S-02 reverse-port, full S-04 re-suite, new test project.

## Architecture / Approach

```
Selection → ExtractGrouped → DwgWriter.WriteDwg
                              → CurvedSurfaceRefiner.Refine  (always)
                              → PolyfaceMesh + shaded VPORT
```

Work stays in AutoCAD 2026 writer/refiner + harness/diagnostics — not in shared Geometry.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| ----- | ---------------- | -------- |
| 1. Automated gate & invariants | Harness ≤12° + GetTypes OK + knobs locked | Accidental valuetype fields hide plugin |
| 2. Host visual (+ conditional tune) | Real elbows accepted; roadmap updated | Under-refine on real models → knob churn |

**Prerequisites:** Manage 2026 + AutoCAD; representative elbow selections; elevated deploy.
**Estimated effort:** ~1–2 sessions (Phase 1 mostly verify/docs; Phase 2 is the real gate).

## Open Risks & Assumptions

- Real BIM elbows may need a knob tune even if harness stays green.
- Over-refine can bloat DWG; budget 600k is the backstop, not a target.
- Nested struct layout remains a load-bearing constraint for every refiner edit.

## Success Criteria (Summary)

- Harness exit 0 with elbow facets ≤12°.
- Host: smooth shaded elbows; sane pipe; mixed OK; ribbon present.
- No `ReflectionTypeLoadException` / missing AutoCAD plugin after changes.
