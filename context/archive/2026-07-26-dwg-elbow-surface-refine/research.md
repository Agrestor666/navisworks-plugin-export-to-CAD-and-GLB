---
date: 2026-07-26T22:54:28+01:00
researcher: Cursor Agent
git_commit: d3df4acf4efd49d8781bf208996e43e5a5425c19
branch: master
repository: navisworks-plugin-export-to-CAD
topic: "Jak z Navisworks wyeksportować wygładzone kolanka/krzywe do DWG"
tags: [research, codebase, autocad-export, dwg, polyfacemesh, curved-surface-refine, elbows, navisworks-2026]
status: complete
last_updated: 2026-07-26
last_updated_by: Cursor Agent
---

# Research: Jak z Navisworks wyeksportować wygładzone kolanka/krzywe do DWG

**Date**: 2026-07-26T22:54:28+01:00
**Researcher**: Cursor Agent
**Git Commit**: d3df4acf4efd49d8781bf208996e43e5a5425c19
**Branch**: master
**Repository**: navisworks-plugin-export-to-CAD

## Research Question

Dla roadmap slice **S-05 (`dwg-elbow-surface-refine`)** — jak z Navisworks wyeksportować wygładzone kolanka / powierzchnie krzywe do DWG, żeby w AutoCAD wyglądały jak gładkie gięcia, a nie wielościenne pryzmy?

## Summary

**Navisworks nie oddaje krzywych NURBS / łuków / brył rur — tylko tesselowane trójkąty (+ normalne).** Pipeline Manage 2026 już dziś: selekcja → COM `GenerateSimplePrimitives` → `ExtractedFragment` → `DwgWriter.WriteDwg` → **`CurvedSurfaceRefiner.Refine`** → kolorowe `PolyfaceMesh` + VPORT Gouraud.

Wygładzenie nie polega na eksporcie „prawdziwej krzywej”, tylko na **zagęszczeniu geometrii** przed zapisem: AutoCAD flat-shade’uje `PolyfaceMesh` (brak stored normals), więc gruba tesselacja hosta zawsze wygląda jak pryzma. Seed z S-04 jest **już podpięty** w `DwgWriter` (nie seed-only). S-05 formalizuje acceptance / strojenie progów na realnych kolankach oraz regresję host-load (nested valuetypes bez pól `Geometry.Vec3`).

Nie da się (w tym MVP) wypisać `ARC` / `SWEEP` / `3DSOLID` z geometrii NW — decyzja S-04: mesh-only, FR-006 = otwórz i pracuj dalej, nie „true solid”.

## Detailed Findings

### 1. End-to-end pipeline (gdzie żyje wygładzenie)

```
CurrentSelection
  → AutoCadExportCommand.RunExport
  → SelectionGeometryExtractor.ExtractGrouped   (COM triangles, world Z-up)
  → DwgWriter.WriteDwg
       → CurvedSurfaceRefiner.Refine            ← jedyny punkt wygładzania
       → weld / color-group / PolyfaceMesh
       → VPORT GouraudShaded
       → ACadSharp DWG AC1032
```

| Step | File | Role |
|------|------|------|
| Entry + guards | `NavisworksExport.AutoCad.2026/AutoCadExportCommand.cs` (~40–94) | Empty selection / zero triangles → error; dialog `.dwg`; `ExtractGrouped` then `WriteDwg` |
| Tessellation | `NavisworksExport.Geometry/SelectionGeometryExtractor.cs` | Leaves with `HasGeometry`; `GenerateSimplePrimitives`; world transform; **Triangle only** (Line/Point empty) |
| Refine | `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs` | Phong edge splits when endpoint normals diverge |
| Call site | `NavisworksExport.AutoCad.2026/DwgWriter.cs:72` | `fragments = CurvedSurfaceRefiner.Refine(fragments, log);` — **before** mesh emission |
| Emit | `DwgWriter.cs` | Per-color `PolyfaceMesh`, max 32k verts/entity, crease hide only ~1° seams |

**Implikacja dla planu:** dalsza praca S-05 = knobs + acceptance wokół istniejącego call site w `DwgWriter`, nie nowy extractor ani osobna komenda. GLB pozostaje bez refine (ma per-vertex normals).

### 2. Co Navisworks naprawdę dostarcza

- API używane: `InwOaFragment3.GenerateSimplePrimitives` → callback `Triangle` z pozycjami i (często) normalnymi.
- **Brak** NURBS, BREP, parametric elbow, łuków w tej ścieżce.
- Kolor COM vertex jest zawodny na 2026 — kolor z `ModelItem.Geometry` / appearance (lesson + extractor).
- Oś: world **Z-up**, bez swapu Y-up (to tylko GLB). Jednostki dokumentu — bez konwersji do metrów.

Dlatego „wygładzone kolanko w DWG” = **gęstsza siatka trójkątów**, nie rekonstrukcja geometrii CAD.

### 3. Algorytm `CurvedSurfaceRefiner` (seed)

Źródło: `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs`.

1. Markuj trójkąt, gdy na którejkolwiek krawędzi `dot(Ni, Nj) < cos(SplitAngleDegrees)`.
2. Na oznaczonym trójkącie **split wszystkich trzech krawędzi** (uniknięcie cienkich ridge triangles na rurach).
3. Nowy wierzchołek = chord midpoint + **Phong lift** na płaszczyzny styczne końców (`ProjectionWeight`).
4. Shared edges: pierwszy trójkąt wygrywa midpoint (bez T-junction).
5. Do `MaxPasses`; pass przekraczający `TriangleBudget` jest **odrzucany w całości**.

| Knob | Default | Znaczenie wizualne |
|------|---------|-------------------|
| `SplitAngleDegrees` | 10° | Kiedy normalne końców krawędzi „mówią” krzywiznę |
| `MaxPasses` | 4 | Górny limit zagęszczeń (~halving kąta na pass) |
| `TriangleBudget` | 600_000 | Sufit kosztów; chroni przed explosion |
| `ProjectionWeight` | 0.5 | Pełny Phong (1.0) wybrzusza poza łuk; 0.5 ≈ koło |
| `WeldEpsilon` | 1e-6 | Kwant pozycji do shared-edge id |

**I/O:** `IReadOnlyList<ExtractedFragment>` → nowa lista (bez mutacji in-place). Fragmenty bez splitów reużywane referencyjnie.

**Host-load (lesson):** nested `Midpoint` / `Corner` trzymają **tylko primitives** — pola `Vec3`/`Rgba` z `Geometry.2026` powodowały `ReflectionTypeLoadException` przy `GetTypes` hosta (`lessons.md`). `[ModuleInitializer]` nie pomaga przy skanie na net48.

### 4. Dlaczego bez refine DWG wygląda jak pryzma

Z remarków w kodzie (`CurvedSurfaceRefiner` + `DwgWriter`):

- GLB może zostawić grubą tesselację — viewer interpoluje shading po normals.
- `PolyfaceMesh` **nie przechowuje normals** → AutoCAD flat-shade każdej facety.
- Jedyna dźwignia w DWG: **więcej facet** o mniejszym kącie między sąsiadami.

Writer ukrywa tylko szwy ~1° (`CreaseAngleDegrees`) — nie ukrywa facet rury (~15° przy 24 segmentach), żeby wireframe 2D nie znikał.

### 5. Acceptance seed (harness, nie formalny S-05)

`tools/DwgWriterHarness/Program.cs`:

- Celowo gruby elbow: `ElbowSegments = 8`, `ElbowSteps = 6` (~45° / ~15° źródło).
- Po refine: sąsiednie facety **≤ `MaxElbowFacetDegrees` (12°)**.
- Ścieżka testu: ten sam `DwgWriter.WriteDwg` co host.

Roadmap S-05 nadal wymaga wizualnej akceptacji na **realnych** selekcjach Manage 2026 + AutoCAD — harness to proxy, nie zamknięcie slice’a.

### 6. Product / history constraints (nie re-litigować)

Z archiwum S-04 + PRD:

| Locked | Source |
|--------|--------|
| Entity = `PolyfaceMesh` (nie `3DSOLID` / ACIS) | `context/archive/2026-07-26-export-selection-autocad-2026/research.md` |
| FR-006 = otwórz w AutoCAD i kontynuuj pracę — nie „true solid”, nie smooth elbows | `context/foundation/prd.md` |
| ACadSharp, AC1032, true color, VPORT Gouraud | S-04 plan/research |
| DWG Z-up, bez Y-up swap | `DwgWriter` |
| Refine jedzie z `DwgWriter` (przy reverse-porcie S-02 — ta sama ścieżka) | roadmap S-05 |

Archived S-04 docs **nie nazywają** `CurvedSurfaceRefiner` — seed wpadł w implementacji; formalny slice to S-05.

## Code References

- `NavisworksExport.AutoCad.2026/AutoCadExportCommand.cs:75–94` — extract → write
- `NavisworksExport.AutoCad.2026/DwgWriter.cs:12–24` — Z-up, flat-shade rationale, refine-first
- `NavisworksExport.AutoCad.2026/DwgWriter.cs:72` — `CurvedSurfaceRefiner.Refine` call site
- `NavisworksExport.AutoCad.2026/DwgWriter.cs:38–43` — crease vs pipe facets
- `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs:9–46` — algorithm + knobs
- `NavisworksExport.AutoCad.2026/CurvedSurfaceRefiner.cs:48–77` — multi-pass + budget
- `NavisworksExport.Geometry/SelectionGeometryExtractor.cs` — COM tessellation only
- `NavisworksExport.Geometry/PrimitiveCallback.cs` — Triangle-only accumulation
- `tools/DwgWriterHarness/Program.cs:28–36` — coarse elbow + 12° acceptance proxy
- `context/foundation/lessons.md` — nested valuetype / AssemblyResolve rules

## Architecture Insights

1. **Format-specific compensation, not geometry API upgrade** — refine jest w projekcie AutoCAD, nie w shared Geometry, żeby GLB nie płacił kosztem trójkątów.
2. **Normals są „źródłem krzywizny”, nie celem eksportu** — DWG ich nie niesie; służą tylko do decyzji split / Phong lift.
3. **Heurystyka, nie rekonstrukcja rury** — złe/brakujące normals → under-refine lub bulging; nie ma detekcji „to jest elbow”.
4. **Budżet całoprzebiegowy** — pass all-or-nothing chroni przed asymetrycznym refine jednej strony obiektu.
5. **Host-load safety jest częścią feature’a** — każda zmiana layoutu nested structów w refinerze to ryzyko zniknięcia całego pluginu z ribbona.

## Historical Context (from prior changes)

- `context/archive/2026-07-26-export-selection-autocad-2026/research.md` — mesh-not-solid, PolyfaceMesh industry pattern, ACadSharp limits
- `context/archive/2026-07-26-export-selection-autocad-2026/plan.md` — entity/chunking/VPORT decisions; DXF/3DSOLID out of scope
- `context/changes/export-selection-autocad/research.md` — wcześniejszy S-02 research (ACadSharp/COM), bez elbow refine
- `context/foundation/roadmap.md` S-05 — outcome, unknowns (progi vs koszt; DWG-only MVP), risk over/under-refine
- `context/foundation/lessons.md` — `CurvedSurfaceRefiner` + `ReflectionTypeLoadException`

## Related Research

- `context/archive/2026-07-26-export-selection-autocad-2026/research.md` — solid-look + color (S-04)
- `context/changes/export-selection-autocad/research.md` — AutoCAD export library/path (S-02, parked)

## Open Questions

1. **Progi na realnych modelach** — czy 10° / 4 pass / 600k / harness 12° wystarczają na typowe kolanka BIM, czy trzeba poluzować budżet / zaostrzyć split? (Owner: user; block for final acceptance numbers.)
2. **Zakres formatów** — MVP = tylko DWG/AutoCAD path; GLB kiedyś? (Owner: user; block: no for planning.)
3. **Czy refine ma być zawsze włączony, czy opcjonalny / diagnostyczny log-only tuning?** — dziś zawsze w `WriteDwg`; nie ma UI switcha.
4. **Jak mierzyć sukces poza harnessem** — wizualna ocena w AutoCAD vs automatyczny max-facet-angle na ekstrakcie z hosta (wymaga reprezentatywnych NWD/selecji).
5. **Czy kiedykolwiek rozważać true curves** (fit cylinder/torus → CAD entities) — poza obecnym seedem i poza decyzją S-04 mesh-only; prawdopodobnie out of S-05.
