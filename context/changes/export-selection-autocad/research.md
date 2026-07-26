---
date: 2026-07-26T11:47:00+01:00
researcher: Cursor Agent
git_commit: 418da57
branch: master
repository: navisworks-plugin-export-to-CAD
topic: "Aktualne podejścia i trendy eksportu z Navisworks do AutoCAD (DWG/DXF) — web, Context7, Autodesk MCP"
tags: [research, autocad-export, navisworks-api, dwg, dxf, acadsharp, aps]
status: complete
last_updated: 2026-07-26
last_updated_by: Cursor Agent
---

# Research: Aktualne trendy eksportu z Navisworks do AutoCAD (DWG/DXF)

**Date**: 2026-07-26T11:47:00+01:00
**Researcher**: Cursor Agent
**Git Commit**: 418da57
**Branch**: master
**Repository**: navisworks-plugin-export-to-CAD

## Research Question

Sprawdzić (web search, Context7, Autodesk Product Help MCP) jakie są aktualne trendy/podejścia do eksportu selekcji z Navisworks do AutoCAD (DWG/DXF) — jako wejście do planowania roadmap slice `S-02: export-selection-autocad`.

## Summary

**Kluczowe odkrycie: Navisworks nie ma i nigdy nie miał wsparcia dla eksportu do DWG/DXF — ani w UI, ani w API.** To potwierdza zarówno wyszukiwanie w sieci (fora Autodesk, GitHub issue zamknięty jako "Functionality Not Supported"), jak i official Autodesk Help artykuł zwrócony przez Autodesk MCP. Autodesk sam rekomenduje okrężną drogę: Navisworks → FBX → 3ds Max/FormIt → DWG — co jest nieakceptowalne dla naszego PRD (musi być jeden plugin, jeden krok, tylko selekcja).

Z tego wynika, że **plugin AutoCAD w tym repo musi sam zbudować plik DWG/DXF z geometrii selekcji** — nie ma gotowej ścieżki "Navisworks eksportuje, my tylko wołamy API". To jest dokładnie nisza, którą wypełniają istniejące komercyjne pluginy trzecich firm (Codemill DWG Exporter, Vision Workplace DXF Converter) — obie działają identycznie do naszego PRD: czytają selekcję (Selection Tree / search sets / on-screen pick), konwertują geometrię do DWG entities (Mesh/PolylineMesh/BlockReference), mapują properties na DWG attributes.

**Ścieżka techniczna geometrii jest wspólna z S-01 (GLB):** Navisworks .NET API **nie eksponuje** trójkątów/geometrii wprost — trzeba przejść przez **COM API** (`ComBridge.ToInwOpSelection` → `path.Fragments()` → `frag.GenerateSimplePrimitives(...)` z callbackiem `InwSimplePrimitivesCB`). To jest jedyna potwierdzona przez inżynierów Navisworks metoda ekstrakcji trójkątów, znana z bycia CPU-intensive, wymaga deduplikacji fragmentów i transformacji `LocalToWorld`. Ten sam kod ekstrakcji geometrii może/powinien być współdzielony (lub bardzo podobny) między pluginem GLB i pluginem AutoCAD.

**Do zapisu samego pliku DWG/DXF, najlepiej trafiający w constraints projektu (`AGENTS.md`: brak cloud/backend, C#/.NET, offline) jest `ACadSharp`** — open-source (MIT), aktywnie rozwijana (ostatni release 2026-06-16, 775+ stars), natywna biblioteka .NET (net48/.NET Standard 2.0/net5+) do zapisu DWG (AC1014–AC1032, czyli AutoCAD 2000 do najnowszych wersji) i DXF, z klasami `Mesh`, `BlockRecord`, `Layer`, `DwgWriter`. Nie wymaga runtime'u AutoCAD/ObjectARX ani chmury — idealnie zgodne z guardrail "plugin nie modyfikuje/nie zależy od zewnętrznych serwisów".

**Cloud-based alternatywy (Autodesk Platform Services) zostały sprawdzone i odrzucone jako nieadekwatne dla MVP:**
- Model Derivative API nie konwertuje NWC/NWD → DWG (tylko do SVF/SVF2 dla web viewera, lub z formatów źródłowych typu F3D → DWG, ale nie z Navisworks).
- Design Automation API for AutoCAD (`AutoCAD.PlotToPDF+prod`, custom activities na silniku AutoCAD 2026/2027 "Watt") pozwala uruchamiać AutoCAD w chmurze i konwertować pliki — ale wymaga uploadu do cloud storage (OSS/S3), autoryzacji APS, i i tak nie rozwiązuje problemu wejścia (Navisworks nadal nie wypluwa DWG/DXF/geometrii bez custom ekstrakcji). To jest ważny trend rynkowy (Autodesk mocno inwestuje w cloud automation, AutoCAD 2026 "Watt" engine, migracja z AutoCAD 2021 engine), ale niezgodny z PRD (`Non-Goals`: brak chmury/backendu w MVP) i z `AGENTS.md` (brak backend/auth w MVP).

## Detailed Findings

### 1. Navisworks nie eksportuje natywnie do DWG/DXF (potwierdzone z 3 niezależnych źródeł)

- Official Autodesk Help ("Convert a Navisworks NWD file to an AutoCAD DWG file") — jedyna oficjalna droga to NWD → export do FBX → import do 3ds Max lub FormIt → export do DWG. Alternatywnie: attach NWD jako reference w AutoCAD (bez konwersji na natywne obiekty DWG).
- GitHub `Autodesk/revit-ifc` issue #575 — Autodesk support engineer odpowiada oficjalnie: *"Navisworks cannot do this, whether in the UI or with the API"* i zamyka ticket jako "Functionality Not Supported".
- Autodesk Community forum — cytat: *"Please be aware that the Navisworks API hardly ever supports any functionality that is not also available in the user interface. […] research the optimal manual approach to a solution first, before attacking the task programmatically."*
- Autodesk MCP `search_help_content` (product: Navisworks Manage) nie znalazł żadnego dedykowanego oficjalnego eksportera DWG/DXF w bazie pomocy — najbliższe trafienia dotyczą eksportu do Excel/CSV (properties) i selection/search sets do XML, nie geometrii do CAD.

**Wniosek:** nasz plugin nie "woła" wbudowanej funkcji eksportu — musi samodzielnie zbudować plik wyjściowy z surowej geometrii selekcji.

### 2. Konkurencyjne pluginy trzecich firm — walidacja podejścia produktowego

- **Codemill DWG™ Exporter** (Autodesk App Store, wersja 2.0.1, aktualizacja 11/2025, wspiera Navisworks Manage/Simulate 2021–2026):
  - Eksportuje selekcję (Selection Tree, on-screen, search/selection sets, Find Item) do DWG.
  - Geometria → `PolylineMesh` / `SubDMesh` / `BlockReference` (użytkownik wybiera typ).
  - Mapuje properties Navisworks → DWG block attributes (z renamingiem).
  - Steruje kolorem (ambient/diffuse/geometry-node) i layerem (current layer selekcji lub layer rodzica).
  - **Reguła 1:1** — tyle obiektów DWG ile wybranych model items (potwierdza że "selekcja = granica eksportu" jest standardem rynkowym, zgodnym z naszym PRD).
- **Vision Workplace DXF Converter** (aktywnie wersjonowany, ostatnia aktualizacja 04/2026, wsparcie do Navisworks 2027):
  - Eksport do DXF z opcją section-box, kolorów/materiałów (bez tekstur), "pixel culling" (skip małych entities dla wydajności plików).
  - Selected parts convertible, hidden parts skipped — czyli respektuje widoczność + selekcję.

Oba produkty potwierdzają: (a) to jest realny, płatny rynek nisza-produktów (nie ma darmowego/wbudowanego rozwiązania), (b) architektura "selekcja → mesh/block entities → DWG/DXF z property mapping" jest sprawdzonym, oczekiwanym przez użytkowników wzorcem.

### 3. Ekstrakcja geometrii z Navisworks — jedyna potwierdzona metoda (COM API, nie .NET API)

Z community/Stack Overflow (potwierdzone przez inżynierów Navisworks w wątku Autodesk Community):

```csharp
ModelItemCollection selection = Autodesk.Navisworks.Api.Application.ActiveDocument.CurrentSelection.SelectedItems;
ComApi.InwOpState state = ComBridge.State;
ComApi.InwOpSelection comSelection = ComBridge.ToInwOpSelection(selection);

foreach (ComApi.InwOaPath3 path in comSelection.Paths())
{
    foreach (ComApi.InwOaFragment3 frag in path.Fragments())
    {
        frag.GenerateSimplePrimitives(ComApi.nwEVertexProperty.eNORMAL | ComApi.nwEVertexProperty.eCOLOR, callback);
    }
}
// callback implements InwSimplePrimitivesCB.Triangle(v1, v2, v3) — jedno wywołanie per trójkąt
```

Kluczowe ograniczenia potwierdzone przez community i samych inżynierów Autodesk:
- `.PrimitiveTypes` na .NET API pokazuje *typ* prymitywu (Triangles/Lines), ale nie same dane — dane trzeba wyciągnąć przez COM.
- `GenerateSimplePrimitives` jest CPU-intensive; trzeba deduplikować fragmenty (wiele model items często współdzieli tę samą geometrię z różną transformacją) — bez tego eksport dużej selekcji będzie zbyt wolny.
- Trójkąty czasem wracają jako triangle strips, nie zawsze w "logicznym" porządku — worth testować z realną geometrią przed budową pipeline'u.
- `frag.GetLocalToWorldMatrix()` (`InwLTransform3f3`) musi być zaaplikowany do współrzędnych wierzchołków, żeby dostać world-space geometrię (potrzebne, bo eksport ma reprezentować faktyczne położenie w modelu, nie lokalne).
- Kolor per-wierzchołek dostępny jako `v.colors` (4 floats) przy `eCOLOR` fladze; materiał na poziomie fragmentu przez `frag.Appearance` (14-elementowa tablica, mapująca się na `InwOaMaterial`).

**To jest prawdopodobnie identyczna ścieżka ekstrakcji geometrii, której będzie potrzebować S-01 (GLB)** — silny argument za współdzielonym (lub bardzo podobnym) modułem ekstrakcji geometrii między dwoma pluginami, mimo decyzji z `nw-plugin-scaffold` o "no shared core yet" (ta decyzja explicite mówi "avoids premature abstraction before real duplication appears in S-01/S-02" — ta duplikacja właśnie się ujawniła w badaniach, ale jest to sygnał do rewizji przy planowaniu S-01/S-02, nie do zmiany w F-01).

### 4. Biblioteki do zapisu DWG/DXF po stronie .NET — trend: open-source wygrywa z komercyjnym ODA

| Biblioteka | Licencja | DWG Write | DXF Write | Status (07/2026) | Uwagi |
|---|---|---|---|---|---|
| **ACadSharp** (`DomCR/ACadSharp`) | MIT (open source) | ✅ AC1014–AC1032 | ✅ ASCII + binary | Aktywna: 775★, ostatni release v3.6.29 (2026-06-16), 30 kontrybutorów | Czysty C#/.NET, brak zależności od AutoCAD/ObjectARX; targety `net48`, `net5.0`+, `.NET Standard 2.0` — pokrywa `net48` wymagany przez host Navisworks. Ma klasy `Mesh` (Vertices/Edges), `BlockRecord.Entities`, `Layer`, `DwgWriter`/`DxfWriter`. |
| **netDxf** (oryginał) | MIT | n/a (tylko DXF) | ✅ | **Zarchiwizowany od 2023** | Nie rekomendowane — brak aktywnego maintenance. |
| **netDxf (fork `anhellwig/NetDxf`)** | MIT | n/a | ✅ AutoCad2000–2018 | Aktywny fork (hobby project, brak gwarancji) | Tylko DXF, mniejszy zakres niż ACadSharp. |
| **ODA (Open Design Alliance) Teigha/ODA SDK** | Komercyjna, closed-source | ✅ | ✅ | Przemysłowy standard (ODIS component używany przez sam Navisworks do DWG/RealDWG) | Wymaga płatnej licencji + członkostwa ODA; overkill i niezgodne z budżetem/timeline (3 tygodnie, solo dev) i z brakiem komercyjnej dystrybucji w Non-Goals. |
| **RealDWG (Autodesk)** | Komercyjna, wymaga umowy z Autodesk | ✅ | ✅ | Używane wewnętrznie przez Navisworks (patrz sekcja 5 — ODIS/RealDWG errors) | Wymaga formalnego partnerstwa z Autodesk (ADN + RealDWG license) — nieadekwatne dla wewnętrznego narzędzia zespołu. |

**Wniosek/trend:** rynek bibliotek open-source do zapisu DWG dojrzał w ostatnich ~2 latach — `ACadSharp` (utworzony 2021, ale najintensywniejszy rozwój 2024–2026) jest obecnie realną alternatywą dla drogich, ciężkich SDK typu ODA/RealDWG dla przypadków "generuj DWG programowo z własnych danych", co dokładnie opisuje nasz use case (mesh z Navisworks → DWG entities). To jest **zgodne z guardrails projektu**: brak chmury, brak zewnętrznych zależności komercyjnych, C#/.NET, działa offline na stacji dewelopera/użytkownika.

### 5. Autodesk Platform Services (APS/Forge) — silny trend rynkowy, ale niezgodny z MVP scope

- **Model Derivative API**: tłumaczy pliki (w tym Navisworks NWD/NWC) na SVF/SVF2 do web viewera oraz konwertuje part formaty źródłowe (np. F3D) do DWG/FBX/IGES/OBJ/STEP/STL — **ale nie NWC/NWD → DWG**. Odrzucone jako nieadekwatne dla naszego use case.
- **Design Automation API for AutoCAD** ("Automation API"): pozwala uruchomić prawdziwy silnik AutoCAD w chmurze (`Autodesk.AutoCAD+26_0` = AutoCAD 2027 "Watt", .NET 10) do przetwarzania/tworzenia DWG programowo — świeży trend, Autodesk aktywnie inwestuje (deprecates starsze silniki np. AutoCAD 2021 engine, dodaje nowe co ~rok). To jest **realny trend "cloud-native CAD automation"**, ale wymaga: konta APS, cloud storage (OSS/S3), auth po stronie serwera, workitem/activity setup — czysty backend, co jest explicite w Non-Goals PRD ("Chmura / synchronizacja / backend") i w `AGENTS.md` ("No cloud sync, auth, backend... in MVP"). **Nie rekomendowane dla S-02 MVP**, ale warto odnotować jako potencjalny kierunek post-MVP/v2 gdyby zespół zdecydował się na architekturę serwerową.

### 6. Ryzyka natury operacyjnej znalezione przy okazji (z Autodesk Help)

- Błąd `"No plugin exists that will open xxx.dwg"` przy próbie otwarcia DWG *w* Navisworks wskazuje że host Navisworks *czyta* DWG przez komponent **ODIS/RealDWG** (osobny, aktualizowany komponent) — nie mylić z naszym przypadkiem (my *piszemy* DWG z Navisworks, nie odczytujemy DWG w Navisworks), ale potwierdza że Autodesk sam używa RealDWG wewnętrznie do interoperacji DWG↔Navisworks, co pokazuje skalę potrzebnej infrastruktury gdyby szło się drogą "pełnej" kompatybilności DWG (nie jest to potrzebne dla naszego MVP, gdzie wystarczy zapis nowych entities).

## Code References

Ten research nie modyfikuje ani nie cytuje kodu z tego repozytorium (repo zawiera na razie tylko scaffold hosta pluginów z `nw-plugin-scaffold`, bez logiki eksportu) — poniżej referencje do zewnętrznych źródeł/dokumentacji wykorzystanych do wniosków:

- Autodesk Help: `Convert-Naviswork-file-NWD-to-an-AutoCAD-DWG-file.html` — brak natywnej konwersji NWD→DWG.
- GitHub `Autodesk/revit-ifc#575` — oficjalne zamknięcie zgłoszenia jako "Functionality Not Supported".
- `apps.autodesk.com/NAVIS` Codemill DWG Exporter listing — architektura konkurenta.
- `visionworkplace.com/products/dxf-converter-for-autodesk-navisworks` — architektura konkurenta #2.
- `github.com/DomCR/AcadSharp` + Context7 `/domcr/acadsharp` docs — `Mesh`, `DwgWriter`, `BlockRecord.Entities`, `Layer`.
- Autodesk Community forum threads (`GenerateSimplePrimitives`, `ComBridge.ToInwOpSelection`) — jedyna metoda ekstrakcji geometrii z Navisworks .NET/COM API.
- `aps.autodesk.com/model-derivative-api-2d-3d-conversions`, `aps.autodesk.com/automation-apis`, `aps.autodesk.com/blog/autocad-2026-watt-now-available-design-automation-api` — stan cloud APIs (odrzucone dla MVP, odnotowane jako kontekst rynkowy).

## Architecture Insights

- **Wspólny bottleneck z S-01**: ekstrakcja geometrii selekcji (COM API + `GenerateSimplePrimitives`) jest identycznym problemem dla GLB i AutoCAD. Warto rozważyć przy planowaniu S-01/S-02 (nie teraz, nie w F-01) wspólny mały moduł "SelectionGeometryExtractor" zwracający listę trójkątów+kolor+transform w world space, konsumowany osobno przez glTF writer (S-01) i DWG/DXF writer (S-02) — to jest właśnie "real duplication" na którą `plan-brief.md` F-01 czekało, żeby nie budować abstrakcji przedwcześnie.
- **Format wyjściowy AutoCAD**: rekomendacja z badania — **DXF jako pierwszy cel** (prostszy format tekstowy/binarny, ACadSharp ma pełniejsze wsparcie DXF write niż DWG dla starszych wersji, łatwiejszy debug), z DWG jako kolejny krok przez `DwgWriter` (ACadSharp wspiera write dla AC1014-AC1032, czyli pokrywa realistyczne, wspierane wersje AutoCAD). PRD wymaga "(DWG/DXF)" bez preferencji — DXF-first obniża ryzyko przy zachowaniu zgodności z FR-005/FR-006.
- **Property mapping (FR-008 sąsiaduje)**: obaj konkurenci (Codemill, Vision Workplace) budują wartość produktową głównie wokół property→attribute mapping i layer/color control — sugeruje że nawet "MVP" eksport DWG/DXF powinien pilnować minimalnie: layer per obiekt (lub per selekcja) + kolor, bo inaczej plik będzie wizualnie nieużyteczny (jednolity szary blob), nawet jeśli PRD nie wymaga pełnych metadanych BIM (Non-Goals już to wyklucza — ale kolor/layer to nie metadane BIM, to podstawowa czytelność CAD).

## Historical Context (from prior changes)

- `context/changes/nw-plugin-scaffold/plan-brief.md` — decyzja "Project structure: Two separate plugin projects, no shared core yet", z rationale że unikamy przedwczesnej abstrakcji dopóki "real duplication" się nie ujawni w S-01/S-02. Ten research pokazuje że **duplikacja geometry-extraction już się ujawniła** (ta sama ścieżka COM/`GenerateSimplePrimitives` potrzebna w obu pluginach) — warto to odnotować jako input do planowania S-01 i S-02, nie zmieniać teraz w F-01.
- `context/changes/nw-plugin-scaffold/plan-brief.md` — "Command stub scope: Read-only selection-count proof… De-risks the exact API surface (`CurrentSelection`) both S-01 and S-02 depend on" — potwierdzone: `CurrentSelection.SelectedItems` jest właśnie punktem wejścia do `ComBridge.ToInwOpSelection` w tym researchu.
- `context/foundation/roadmap.md` (S-02) — Unknown: "Brak sformalizowanej US-02 (AutoCAD) w PRD" — nierozwiązane, nie blokuje planowania (FR-004–006 wystarczają).
- `context/foundation/prd.md` — Non-Goals explicite wykluczają chmurę/backend i pełne metadane BIM — oba te punkty zawężyły rekomendacje w sekcji 5 (odrzucenie APS) i w Architecture Insights (property mapping ograniczone do layer/color, nie pełnych properties).

## Related Research

Brak wcześniejszych dokumentów `research.md` w innych change'ach — to jest pierwszy artefakt tego typu w repo.

## Open Questions

1. **DXF vs DWG jako pierwszy target formatu** — rekomendacja (DXF-first) wymaga potwierdzenia przez użytkownika/plan S-02; PRD nie różnicuje.
2. **Wersja DWG do wygenerowania** (jeśli DWG) — ACadSharp wspiera write do AC1014 (AutoCAD 2000) aż do AC1032 (najnowsze) — trzeba zdecydować target zgodny z "wspierana wersja AutoCAD" z PRD Guardrails (nieokreślona wersja).
3. **Zakres property mapping w MVP** — czy S-02 potrzebuje *jakiegokolwiek* mapowania properties→attributes (jak konkurenci), czy czysto geometria+kolor+layer wystarcza do "otwiera się i można kontynuować pracę" (FR-006)? To wpłynie na zakres planu S-02.
4. **Współdzielony moduł ekstrakcji geometrii między S-01/S-02** — do rozstrzygnięcia przy `/10x-plan export-selection-glb` i `/10x-plan export-selection-autocad`, nie w tym research dokumencie.
5. **Wydajność `GenerateSimplePrimitives` na dużych selekcjach** — nieprzetestowane w tym repo; warto zweryfikować empirycznie przy pierwszej implementacji (community zgłasza to jako CPU-intensive).
