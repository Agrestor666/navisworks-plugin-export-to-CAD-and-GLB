---
project: Navisworks Export Plugins
version: 1
status: shaping
created: 2026-07-26
updated: 2026-07-26
context_type: greenfield
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  frs_drafted: 8
  quality_check_status: accepted
timeline_budget:
  mvp_weeks: 3
  hard_deadline: null
  after_hours_only: false
product_type: desktop
target_scale:
  users: medium
---

# Shape Notes — Navisworks Export Plugins

Seed idea (verbatim):

> potrzebuje 2 dwa pluginy dzialajace w srodowisku navisworks manage. dla wersji 2023 i dla wersji 2025. Problem. mam duzy model w navisworks. z wybranych obiektow chce wyeksportowac model 3d compatibilny z power point (glb format) wtedy moge go umieszczac w prezentacji zamiast generowac screenshoty. drugi plugin eksportuje grupe wybranych obiektow do autocada . otwierajac je w autocadzie umozliwia mi kontynuowanie pracy nad projektem. ob pluginy najlepiej aby dzialaly na wersjach 23 i 25.

## Vision & Problem Statement

Koordynator BIM / projektant pracujący z dużymi modelami w Navisworks Manage nie ma prostego sposobu na przeniesienie **tylko zaznaczonej selekcji** obiektów do formatów użytecznych dalej w workflow — interaktywnego 3D w PowerPoint (GLB) ani pliku do kontynuacji pracy w AutoCAD. Obecnie kończy się to screenshotami (utrata interaktywności 3D) lub wieloetapowymi, ręcznymi obejściami, co generuje tarcie workflow, overhead koordynacyjny i blokuje geometrię w Navisworks.

Kluczowa obserwacja: brakuje eksportu **wyłącznie zaznaczonej selekcji** — nie całego modelu. Dwa osobne pluginy (GLB + AutoCAD) rozwiązują dwa różne momenty w pracy: prezentacja vs kontynuacja projektowania.

Pain categories: workflow friction, missing capability, data trapped, coordination overhead.

## User & Persona

**Primary persona:** Koordynator BIM / projektant w zespole wewnętrznym (kilku–kilkunastu użytkowników Navisworks Manage w firmie).

**Context:** Pracuje z dużymi, skonsolidowanymi modelami Navisworks. Często selekcjonuje fragmenty modelu do prezentacji klientowi/stakeholderom lub do przekazania dalej do AutoCAD.

**Moment of need:**
1. Przygotowuje prezentację PowerPoint — chce wstawić interaktywny model 3D (GLB) z wybranych obiektów zamiast generować screenshoty.
2. Wybrał grupę obiektów w Navisworks — chce otworzyć je w AutoCAD i kontynuować pracę projektową.

## Access Control

N/A — lokalne pluginy desktopowe w Navisworks Manage. Brak auth, brak ról.

- Plugin instalowany lokalnie na stanowisku użytkownika Navisworks.
- Dostęp do modelu kontrolowany przez Navisworks (istniejące uprawnienia projektu).
- Każdy członek zespołu z zainstalowanym pluginem ma pełny dostęp do obu eksportów (GLB + AutoCAD).
- Brak separacji ról w samym pluginie.

## Success Criteria

### Primary

**Flow GLB (PowerPoint 3D):**
1. Użytkownik otwiera duży model w Navisworks Manage 2023
2. Zaznacza grupę obiektów (selekcja)
3. Uruchamia plugin „Export to GLB”
4. Zapisuje plik `.glb`
5. Wstawia GLB do PowerPoint — interaktywny model 3D działa w prezentacji

**Flow AutoCAD:**
1. Użytkownik otwiera model w Navisworks Manage 2023
2. Zaznacza grupę obiektów
3. Uruchamia plugin „Export to AutoCAD”
4. Zapisuje plik (DWG/DXF)
5. Otwiera plik w AutoCAD — geometria selekcji jest gotowa do dalszej pracy

Oba flow muszą działać end-to-end w MVP. Navisworks Manage 2025 — poza MVP (v1.1).

### Secondary

Brak — sam core flow wystarczy w v1.

### Guardrails

- Plugin nie modyfikuje oryginalnego modelu Navisworks — tylko czyta selekcję i eksportuje.
- GLB musi być kompatybilny z PowerPoint 3D (Office 365) — plik otwiera się bez błędów.
- Plik AutoCAD otwiera się w wspieranej wersji AutoCAD bez błędów importu.

## Functional Requirements

### Plugin GLB (PowerPoint 3D)

- FR-001: Użytkownik może uruchomić plugin GLB z poziomu Navisworks Manage 2023. Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

- FR-002: Użytkownik może wyeksportować aktualną selekcję obiektów do pliku GLB. Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

- FR-003: Użytkownik może wstawić wyeksportowany GLB do PowerPoint jako interaktywny model 3D. Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

### Plugin AutoCAD

- FR-004: Użytkownik może uruchomić plugin AutoCAD z poziomu Navisworks Manage 2023. Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

- FR-005: Użytkownik może wyeksportować aktualną selekcję obiektów do pliku AutoCAD (DWG/DXF). Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

- FR-006: Użytkownik może otworzyć wyeksportowany plik w AutoCAD i kontynuować pracę. Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

### Wspólne (oba pluginy)

- FR-007: Użytkownik widzi komunikat błędu gdy selekcja jest pusta (brak obiektów do eksportu). Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

- FR-008: Użytkownik może wybrać lokalizację i nazwę pliku przy eksporcie (dialog zapisu). Priority: must-have
  > Socrates: Brak counter-argumentu — FR stoi jako napisane.

## User Stories

### US-01: Eksport selekcji do GLB dla PowerPoint

- **Given** użytkownik ma otwarty model w Navisworks Manage 2023 z zaznaczoną grupą obiektów
- **When** uruchamia plugin „Export to GLB” i zapisuje plik
- **Then** plik GLB otwiera się w PowerPoint jako interaktywny model 3D bez błędów

#### Acceptance Criteria

- Eksport obejmuje wyłącznie zaznaczone obiekty, nie cały model
- Plik GLB jest kompatybilny z funkcją Insert 3D Model w PowerPoint (Office 365)
- Przy pustej selekcji użytkownik widzi komunikat błędu zamiast pustego pliku
- Oryginalny model Navisworks pozostaje niezmieniony po eksporcie

## Business Logic

Eksport dotyczy **wyłącznie aktualnej selekcji obiektów** w Navisworks — nigdy całego modelu.

Plugin czyta bieżącą selekcję użytkownika jako granicę eksportu. Geometria poza selekcją nie wchodzi do pliku wyjściowego (GLB ani AutoCAD). Użytkownik decyduje co eksportować poprzez zaznaczenie obiektów w Navisworks przed uruchomieniem pluginu. Ta reguła obowiązuje identycznie w obu pluginach — różni się tylko format docelowy (GLB vs DWG/DXF).

## Non-Functional Requirements

- Model Navisworks pozostaje nietknięty po eksporcie — plugin tylko czyta selekcję, nie modyfikuje oryginalnego modelu.
- Plik GLB otwiera się w PowerPoint 3D (Office 365) bez błędów renderowania.
- Plik AutoCAD (DWG/DXF) otwiera się w wspieranej wersji AutoCAD bez błędów importu.
- MVP wspiera Navisworks Manage 2023; wsparcie 2025 planowane w v1.1.

## Non-Goals

- **Navisworks Manage 2025 w MVP** — wsparcie 2025 dopiero w v1.1; MVP targetuje wyłącznie NW 2023.
- **Eksport całego modelu** — pluginy eksportują tylko selekcję; pełny model poza scope.
- **Chmura / synchronizacja / backend** — brak cloud sync, współdzielonych ustawień, ani multi-user backend.
- **Pełne metadane BIM** — brak eksportu properties, IFC data, klasyfikacji; tylko geometria.
- **Batch export** — brak eksportu wielu selekcji naraz w jednej operacji.
- **Modyfikacja geometrii w Navisworks** — pluginy są read-only względem modelu źródłowego.
- **Dystrybucja komercyjna** — narzędzie wewnętrzne dla zespołu, nie produkt na zewnętrzny rynek.

## Quality cross-check

All elements present — quality_check_status: accepted.

- Access Control: present (N/A — local plugin, no auth)
- Business Logic: present (one-sentence selection boundary rule)
- Project artifacts: present
- Timeline-cost ack: present (mvp_weeks: 3)
- Non-Goals: present (7 entries)
- Preserved behavior: n/a (greenfield)
