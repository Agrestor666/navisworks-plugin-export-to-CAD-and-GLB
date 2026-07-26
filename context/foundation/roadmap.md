---
project: Navisworks Export Plugins
version: 1
status: draft
created: 2026-07-26
updated: 2026-07-26
prd_version: 1
main_goal: speed
top_blocker: skills
---

# Roadmap: Navisworks Export Plugins

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Vision recap

Koordynator BIM nie ma prostego sposobu na przeniesienie **tylko zaznaczonej selekcji** z Navisworks Manage do formatów użytecznych dalej — interaktywnego 3D w PowerPoint (GLB) ani pliku do pracy w AutoCAD. Dwa osobne pluginy rozwiązują dwa momenty workflow: prezentacja vs kontynuacja projektowania. MVP waliduje flow na Navisworks Manage 2023; kolejna fala hosta to Manage **2026** (te same dwa pluginy). Manage **2025** pozostaje poza zakresem (brak instalacji). Eksport zawsze ogranicza się do bieżącej selekcji, nigdy całego modelu.

## North star

**S-01: Użytkownik może wyeksportować zaznaczoną selekcję do GLB i otworzyć ją jako interaktywny model 3D w PowerPoint** — przy `main_goal: speed` to najwcześniejszy end-to-end dowód, że hipoteza z Vision (selekcja zamiast screenshotów) działa.

> North star tu oznacza: najmniejszy wycinek end-to-end, którego dostarczenie udowodniłoby główną hipotezę produktu — ustawiony tak wcześnie, jak pozwalają Prerequisites, bo reszta ma sens dopiero gdy to działa.

## At a glance

| ID | Change ID | Outcome (user can …) | Prerequisites | PRD refs | Status |
|---|---|---|---|---|---|
| F-01 | nw-plugin-scaffold | (foundation) minimalny scaffold pluginu Navisworks Manage 2023 ładuje się w hoście i udostępnia punkty wejścia komend | — | NFR (NW 2023), Access Control | done |
| S-01 | export-selection-glb | użytkownik może wyeksportować zaznaczoną selekcję do GLB i otworzyć ją jako interaktywny model 3D w PowerPoint | F-01 | US-01, FR-001, FR-002, FR-003, FR-007, FR-008 | done |
| S-02 | export-selection-autocad | użytkownik może wyeksportować zaznaczoną selekcję do pliku AutoCAD (DWG/DXF) i otworzyć go do dalszej pracy | F-01 | FR-004, FR-005, FR-006, FR-007, FR-008 | proposed |
| F-02 | nw-plugin-scaffold-2026 | (foundation) minimalny scaffold pluginów Navisworks Manage 2026 ładuje się w hoście i udostępnia punkty wejścia komend | F-01 | NFR (host wave), Access Control | done |
| S-03 | export-selection-glb-2026 | użytkownik może wyeksportować zaznaczoną selekcję do GLB z Navisworks Manage 2026 i otworzyć ją jako interaktywny model 3D w PowerPoint | F-02, S-01 | US-01, FR-001, FR-002, FR-003, FR-007, FR-008 | done |
| S-04 | export-selection-autocad-2026 | użytkownik może wyeksportować zaznaczoną selekcję do pliku AutoCAD (DWG/DXF) z Navisworks Manage 2026 i otworzyć go do dalszej pracy | F-02 | FR-004, FR-005, FR-006, FR-007, FR-008 | done |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme | Chain | Note |
|---|---|---|---|
| A | Host & GLB 2023 (north star) | `F-01` → `S-01` | Ścieżka walidacji MVP na Manage 2023. |
| B | AutoCAD 2023 | `S-02` | Zależy od `F-01`; **odłożone** jako reverse-port po proof na 2026 (`S-04`). |
| C | Host & pluginy 2026 | `F-02` → `S-03` → `S-04` | Te same możliwości co Stream A/B; host Manage 2026. AutoCAD proof = `S-04` first (nie blokowane na `S-02`). |

## Baseline

What's already in place in the codebase as of `2026-07-26` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** absent — brak UI framework / `package.json` / assetów klienta
- **Backend / API:** partial — tymczasowy ASP.NET Core scaffold (`Program.cs` weatherforecast); brak entry pointów pluginu Navisworks
- **Data:** absent — brak DB/ORM/migracji
- **Auth:** per tech-stack.md: no auth (`has_auth: false`)
- **Deploy / infra:** absent — intencja self-host + GitHub Actions tylko w docs; brak workflow / Dockerfile na dysku
- **Observability:** partial — domyślne `Logging` w `appsettings.json`; brak error tracking / metrics

## Foundations

### F-01: Scaffold pluginu Navisworks Manage 2023

- **Outcome:** (foundation) minimalny scaffold pluginu Navisworks Manage 2023 ładuje się w hoście i udostępnia punkty wejścia komend — bez pełnej logiki eksportu.
- **Change ID:** nw-plugin-scaffold
- **PRD refs:** NFR (MVP wspiera Navisworks Manage 2023), Access Control (lokalna instalacja na stanowisku)
- **Unlocks:** S-01, S-02; redukuje blocker `skills` (wejście w API hosta zamiast webapi scaffoldu)
- **Prerequisites:** —
- **Parallel with:** —
- **Blockers:** Navisworks Manage 2023 zainstalowany na maszynie deweloperskiej (referencje API hosta + weryfikacja ładowania)
- **Unknowns:** —
- **Risk:** Sequenced first because baseline Backend/API is partial (wrong host shape) and `top_blocker: skills` — without a loadable plugin shell, neither export slice is plannable end-to-end. Risk: scaffold overbuilds into a full “plugin platform” instead of the smallest loadable command host.
- **Status:** done

### F-02: Scaffold pluginów Navisworks Manage 2026

- **Outcome:** (foundation) minimalny scaffold pluginów Navisworks Manage 2026 ładuje się w hoście i udostępnia punkty wejścia komend — bez pełnej logiki eksportu; ten sam układ co F-01, osobny target hosta.
- **Change ID:** nw-plugin-scaffold-2026
- **PRD refs:** NFR (host wave — PRD nadal mówi 2025/v1.1; cel operacyjny to 2026), Access Control (lokalna instalacja na stanowisku)
- **Unlocks:** S-03, S-04; redukuje ryzyko ładowania API Manage 2026 przed portem logiki eksportu
- **Prerequisites:** F-01
- **Parallel with:** S-02 (gdyby wrócić do AutoCAD 2023; obecnie odłożone)
- **Blockers:** Navisworks Manage 2026 zainstalowany na maszynie deweloperskiej (referencje API hosta + weryfikacja ładowania)
- **Unknowns:**
  - Czy PRD / AGENTS.md należy przepisać z „2025 w v1.1” na „2026 jako kolejna fala hosta” (2025 pominięte — brak instalacji)? — Owner: user. Block: no (planowanie F-02/S-03 może iść równolegle; docs powinny dogonić przed archiwum zmiany).
- **Risk:** Sequenced after proven 2023 host shell so the 2026 wave is a host retarget, not a second greenfield. Risk: treating 2026 as a full rewrite instead of the smallest loadable twin of F-01.
- **Status:** done

## Slices

### S-01: Eksport selekcji do GLB (PowerPoint 3D)

- **Outcome:** użytkownik może wyeksportować zaznaczoną selekcję do GLB i otworzyć ją jako interaktywny model 3D w PowerPoint
- **Change ID:** export-selection-glb
- **PRD refs:** US-01, FR-001, FR-002, FR-003, FR-007, FR-008
- **Prerequisites:** F-01
- **Parallel with:** S-02
- **Blockers:** PowerPoint (Office 365) dostępny do weryfikacji Insert 3D Model
- **Unknowns:** —
- **Risk:** North star — earliest proof under `speed` that selection→GLB→PowerPoint works. Risk: GLB from NW geometry fails PowerPoint compatibility; catch that before investing in AutoCAD path breadth.
- **Status:** done

### S-02: Eksport selekcji do AutoCAD

- **Outcome:** użytkownik może wyeksportować zaznaczoną selekcję do pliku AutoCAD (DWG/DXF) i otworzyć go do dalszej pracy
- **Change ID:** export-selection-autocad
- **PRD refs:** FR-004, FR-005, FR-006, FR-007, FR-008
- **Prerequisites:** F-01
- **Parallel with:** —
- **Blockers:** wspierana wersja AutoCAD dostępna do weryfikacji otwarcia pliku
- **Unknowns:**
  - Brak sformalizowanej US-02 (AutoCAD) w PRD — Owner: user. Block: no.
- **Risk:** Nadal w MVP Success Criteria, ale **świadomie odłożone** jako reverse-port na Manage 2023 po proof ścieżki AutoCAD na 2026 (`S-04`). Prefer link/`DwgWriter` reuse z `NavisworksExport.AutoCad.2026`. Risk: DWG vs DXF choice and geometry fidelity expand scope; keep to “opens in AutoCAD for further work”.
- **Status:** proposed

### S-03: Eksport selekcji do GLB na Navisworks Manage 2026

- **Outcome:** użytkownik może wyeksportować zaznaczoną selekcję do GLB z Navisworks Manage 2026 i otworzyć ją jako interaktywny model 3D w PowerPoint
- **Change ID:** export-selection-glb-2026
- **PRD refs:** US-01, FR-001, FR-002, FR-003, FR-007, FR-008
- **Prerequisites:** F-02, S-01
- **Parallel with:** —
- **Blockers:** PowerPoint (Office 365) dostępny do weryfikacji Insert 3D Model; Navisworks Manage 2026 na stanowisku weryfikacji
- **Unknowns:**
  - Jakie breaking changes w API geometrii / COM między Manage 2023 a 2026 blokują reuse `NavisworksExport.Geometry` / writer GLB? — Owner: team. Block: no (odkrywane w `/10x-plan` + implementacji na F-02).
- **Risk:** Port udowodnionej ścieżki S-01 na nowy host — waliduje, że „te same pluginy” działają poza 2023. Risk: ukryte różnice API/host loader zmuszają do forka logiki zamiast retargetu projektów.
- **Status:** done

### S-04: Eksport selekcji do AutoCAD na Navisworks Manage 2026

- **Outcome:** użytkownik może wyeksportować zaznaczoną selekcję do pliku AutoCAD (DWG/DXF) z Navisworks Manage 2026 i otworzyć go do dalszej pracy
- **Change ID:** export-selection-autocad-2026
- **PRD refs:** FR-004, FR-005, FR-006, FR-007, FR-008
- **Prerequisites:** F-02
- **Parallel with:** —
- **Blockers:** wspierana wersja AutoCAD dostępna do weryfikacji otwarcia pliku; Navisworks Manage 2026 na stanowisku weryfikacji
- **Unknowns:** —
- **Risk:** 2026-first AutoCAD proof (PolyfaceMesh + ACadSharp) — odblokowane bez `S-02`. `S-02` wraca później jako reverse-port na Manage 2023. Risk: dual-host maintenance later if 2023 COM/API differs from proven 2026 path.
- **Status:** done

## Backlog Handoff

| Roadmap ID | Change ID | Suggested issue title | Ready for `/10x-plan` | Notes |
|---|---|---|---|---|
| F-01 | nw-plugin-scaffold | Scaffold pluginu Navisworks Manage 2023 (loadable command host) | no | done / archived |
| S-01 | export-selection-glb | Eksport selekcji do GLB (PowerPoint 3D) | no | done (implemented); archive when ready |
| S-02 | export-selection-autocad | Eksport selekcji do AutoCAD (DWG/DXF) | no | **Odłożone** — reverse-port po `S-04` |
| F-02 | nw-plugin-scaffold-2026 | Scaffold pluginów Navisworks Manage 2026 (loadable command host) | no | done / archived |
| S-03 | export-selection-glb-2026 | Eksport selekcji do GLB na Manage 2026 (PowerPoint 3D) | no | done — archived |
| S-04 | export-selection-autocad-2026 | Eksport selekcji do AutoCAD na Manage 2026 (DWG/DXF) | no | done — archived |

## Open Roadmap Questions

1. **User story for AutoCAD export flow (US-02)?** — Owner: user. Block: no (FRs cover the capability; story would tighten acceptance criteria). Gates: S-02 acceptance polish only, not planning.
2. **Host wave: 2026 zamiast 2025?** — PRD §NFR / Non-Goals nadal mówią „2025 w v1.1”; operacyjnie kolejny host to Manage 2026 (brak 2025). Owner: user. Block: no for F-02/S-03 planning; yes for closing the docs gap (prd.md, AGENTS.md, tech-stack.md).

## Parked

- **Navisworks Manage 2025** — Why parked: brak instalacji/licencji; PRD kiedyś celował w 2025 jako v1.1 — supersedowane przez falę **2026** (F-02 / S-03 / S-04).
- **Eksport całego modelu** — Why parked: PRD §Non-Goals; tylko selekcja.
- **Chmura / synchronizacja / backend** — Why parked: PRD §Non-Goals.
- **Pełne metadane BIM** — Why parked: PRD §Non-Goals; tylko geometria.
- **Batch export** — Why parked: PRD §Non-Goals.
- **Modyfikacja geometrii w Navisworks** — Why parked: PRD §Non-Goals; pluginy read-only.
- **Dystrybucja komercyjna** — Why parked: PRD §Non-Goals; narzędzie wewnętrzne.
- **CI/CD GitHub Actions na starcie** — Why parked: `main_goal: speed` + progressive disclosure; lokalny build względem hosta wystarczy do pierwszej walidacji S-01.
- **S-02 AutoCAD na Manage 2023 (na teraz)** — Why parked temporarily: świadoma decyzja kolejności — AutoCAD proof najpierw na Manage 2026 (`S-04`); `S-02` wraca później jako reverse-port na 2023.

## Done

- **F-01: (foundation) minimalny scaffold pluginu Navisworks Manage 2023 ładuje się w hoście i udostępnia punkty wejścia komend — bez pełnej logiki eksportu.** — Archived 2026-07-26 → `context/archive/2026-07-26-nw-plugin-scaffold/`. Lesson: —.
- **S-01: użytkownik może wyeksportować zaznaczoną selekcję do GLB i otworzyć ją jako interaktywny model 3D w PowerPoint** — Implemented 2026-07-26 (`context/changes/export-selection-glb/`); archive pending. Lesson: —.
- **F-02: (foundation) minimalny scaffold pluginów Navisworks Manage 2026 ładuje się w hoście i udostępnia punkty wejścia komend — bez pełnej logiki eksportu; ten sam układ co F-01, osobny target hosta.** — Archived 2026-07-26 → `context/archive/2026-07-26-nw-plugin-scaffold-2026/`. Lesson: —.
- **S-03: użytkownik może wyeksportować zaznaczoną selekcję do GLB z Navisworks Manage 2026 i otworzyć ją jako interaktywny model 3D w PowerPoint** — Archived 2026-07-26 → `context/archive/2026-07-26-export-selection-glb-2026/`. Lesson: Manage 2026 wymaga assembly resolver, column-major macierzy, rozwijania selekcji do liści — patrz `context/foundation/lessons.md`.
- **S-04: użytkownik może wyeksportować zaznaczoną selekcję do pliku AutoCAD (DWG/DXF) z Navisworks Manage 2026 i otworzyć go do dalszej pracy** — Archived 2026-07-26 → `context/archive/2026-07-26-export-selection-autocad-2026/`. Lesson: —.
