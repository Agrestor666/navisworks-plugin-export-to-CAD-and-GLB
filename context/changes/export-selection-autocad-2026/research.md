---
date: 2026-07-26T17:27:00+01:00
researcher: Cursor Agent
git_commit: 43b7d9a7086bac07edb8fad8b88073b6501c658e
branch: master
repository: navisworks-plugin-export-to-CAD
topic: "Najlepsze rozwiązania eksportu selekcji do AutoCAD (DWG/DXF) tak, żeby obiekty wyglądały jak solidy z kolorami — web, Autodesk MCP, Context7/ACadSharp"
tags: [research, autocad-export, dwg, dxf, solids, mesh, color, acadsharp, navisworks-api]
status: complete
last_updated: 2026-07-26
last_updated_by: Cursor Agent
---

# Research: Jak wyeksportować selekcję z Navisworks do AutoCAD tak, żeby obiekty wyglądały jak solidy, z kolorami

**Date**: 2026-07-26T17:27:00+01:00
**Researcher**: Cursor Agent
**Git Commit**: 43b7d9a7086bac07edb8fad8b88073b6501c658e
**Branch**: master
**Repository**: navisworks-plugin-export-to-CAD

## Research Question

Dla roadmap slice **S-04 (`export-selection-autocad-2026`)** — przeszukać sieć i Autodesk (web search, Autodesk Product Help MCP, Context7) i znaleźć najlepsze aktualne rozwiązania, żeby geometria selekcji wyeksportowana z Navisworks do AutoCAD (DWG/DXF) **wyglądała jak bryły (solidy)**, z zachowanymi **kolorami** — nie jak surowa chmura trójkątów/wireframe. To uzupełnienie do ogólniejszego `context/changes/export-selection-autocad/research.md` (trendy eksportu, wybór biblioteki ACadSharp), tym razem z ostrym naciskiem na wygląd „solid" + kolor.

## Summary

**Najważniejsze odkrycie: prawdziwe bryły ACIS (`3DSOLID`) nie są realną opcją dla tego projektu — ani od strony AutoCAD, ani od strony biblioteki ACadSharp.** Konwersja mesh→solid w AutoCAD (`CONVTOSOLID`/`SURFSCULPT`) wymaga siatki **całkowicie zamkniętej (watertight)**, a geometria selekcji z Navisworks (fragmenty BIM, częściowo nakładające się, nie zawsze domknięte) rutynowo tego wymogu nie spełnia — potwierdzone niezależnie przez 5 oficjalnych artykułów Autodesk Help, wątki Autodesk Community (nawet zdeterminowani użytkownicy z realnym biznesowym powodem nie potrafili tego niezawodnie osiągnąć) oraz dokumentację konkurencyjnych narzędzi. Dodatkowo, **ACadSharp (wybrana biblioteka) obecnie w ogóle pomija `3DSOLID`/`Region`/`CadBody` przy zapisie** (PR #1150 dodający zapis payloadu ACIS jest wciąż otwarty, niezmergowany na dzień badania) — i nawet gdyby się zmergował, tylko odtwarzałby istniejący payload ACIS 1:1, a nie generował nowej bryły z dowolnej siatki trójkątów (biblioteka nie zawiera jądra geometrycznego ACIS/SAT).

**Dobra wiadomość: nie jest to potrzebne.** Zarówno badanie rynkowe, jak i weryfikacja API biblioteki potwierdzają **spójny, branżowy wzorzec**: eksportery generycznej/BIM-owej geometrii mesh do DWG/DXF (Codemill DWG Exporter, AstraPlant DWG Exporter, natywny eksporter SketchUp→DWG, schemat eksportu Rhino→DWG) **domyślnie i celowo zatrzymują się na poziomie mesh** (`PolyFaceMesh` / `MESH` / `3DFACE`), a konwersję do prawdziwego solidu traktują jako opcjonalny, bramkowany warunkiem watertight krok — jeśli w ogóle go oferują. `PolyFaceMesh`/`MESH` w AutoCAD renderują się jako wypełnione, ocieniowane obiekty w trybach wizualizacji 3D (Realistic/Conceptual/Shaded) **bez żadnego wymogu domknięcia geometrii** — wizualnie „wyglądają jak solidy", mimo że formalnie nią nie są (nie obsługują fillet/chamfer/boolean, ale to jest poza zakresem PRD — FR-006 wymaga tylko „otwiera się i można kontynuować pracę").

**Rekomendacja entity type: `PolyfaceMesh` jako główny cel, nie `Mesh`.** Weryfikacja bezpośrednio w kodzie źródłowym ACadSharp ujawniła kluczową, nieoczywistą różnicę: **`Mesh` (nowoczesny DXF „MESH") w ACadSharp obsługuje tylko jeden kolor na całą encję** (writer na stałe zapisuje `0` jako liczbę nadpisań kolorów per-podencja — funkcja per-face override z oficjalnego DXF reference nie jest zaimplementowana), podczas gdy **`PolyfaceMesh` (starszy, ale wciąż w pełni wspierany format) obsługuje kolor per-face**, bo każdy rekord ściany (`VertexFaceRecord`) jest zapisywany jako niezależna encja DXF, dziedzicząca pełne `Color`/`Layer` — dokładnie potwierdzone też w oficjalnej dokumentacji .NET API AutoCADa dla `FaceRecord`. To oznacza, że nasz istniejący model danych (`ExtractedTriangle` z per-vertex RGBA + fallback per-fragment) może zmapować się na **kolor per-trójkąt** wprost na `PolyfaceMesh`, bez potrzeby rozbijania na `BlockReference` z osobnymi `3DFACE`/`Region` — co było wcześniej rozważanym fallbackiem.

**Nowy, wcześniej nieodkryty element: wygląd „solid" (ocieniowany) po otwarciu pliku zależy od ustawienia widoku (VPORT render mode / visual style), nie od samej encji.** Domyślny szablon AutoCAD często startuje w 2D Wireframe. Żeby plik otwierał się od razu ocieniowany (bez ręcznej zmiany stylu wizualizacji przez użytkownika), trzeba ustawić w tabeli `VPORT` (`*Active`) tryb renderowania na Gouraud shaded (DXF grupa `281` = `4`) — ACadSharp **potwierdzone wspiera** to przez `Viewport.RenderMode` (enum `RenderMode`). To konkretna, tania w implementacji poprawka jakości, którą warto uwzględnić w planie S-02/S-04.

**Format i wersja DWG:** DXF-first nadal ma sens (jak w poprzednim researchu), ale jeśli/gdy DWG — format `AC1032` (AutoCAD 2018) jest **nadal natywnym formatem AutoCAD 2018 przez 2027 włącznie** (potwierdzone tabelą kompatybilności Autodesk), czyli w pełni pokrywa hosta docelowego (Manage/AutoCAD 2026) i jest dokładnie tym, co pisze `ACadSharp.DwgWriter` (AC1014–AC1032).

## Detailed Findings

### 1. Ranking encji DXF/DWG pod kątem „wygląda jak solid" bez wymogu watertight

| Encja | Wymaga watertight? | Domyślnie ocieniowana w 3D visual style? | Kolor per-face? | Praktyczna dla trójkątów z BIM? |
|---|---|---|---|---|
| `3DFACE` | Nie | Tak | Tak (kolor całej encji = 1 trójkąt) | Tak, ale 1 encja/trójkąt (narzut przy dużych selekcjach) |
| `PolyfaceMesh` (legacy `AcDbPolyFaceMesh`) | Nie | Tak | **Tak** (per `VertexFaceRecord`) | Tak — **rekomendowane**; limit ~32 767 wierzchołków/encję (16-bit index) |
| `Mesh` (`AcDbSubDMesh`, DXF „MESH") | Nie | Tak | **Nie w ACadSharp** (tylko cały entity) | Tak geometrycznie, ale kolor tylko jednolity |
| `PolygonMesh` (siatka M×N) | Nie, ale zły model topologii | Tak | — | **Nie** — modeluje regularną siatkę, nie dowolną topologię BIM |
| `Region` | Tak (dla `SURFSCULPT`) | Tak | — | Nie — nie jest formatem na chmurę trójkątów |
| `3DSOLID` (ACIS) | Tak, w pełni | Tak (prawdziwy solid) | — | **Nie** — wymaga dokładnie tej konwersji, której chcemy uniknąć; ACadSharp jej dziś nie zapisuje wcale |
| `BLOCK`/`INSERT` opakowujący `3DFACE`/mesh | Dziedziczy | Dziedziczy | Dziedziczy | Wyłącznie organizacyjne/instancjonowanie (przydatne przy powtarzalnej geometrii) |

Źródła: oficjalny DXF reference Autodesk dla `POLYLINE`/polyface mesh, `MESH`, `3DFACE`, `REGION`; R12 DXF Reference PDF; `ezdxf` docs (MESH internals, MeshBuilder); Deelip.com analiza limitu 32 767 wierzchołków PolyFaceMesh.

### 2. `CONVTOSOLID`/`SURFSCULPT` wymagają watertight — potwierdzone bezpośrednio przez Autodesk Help (5 artykułów)

Zapytania do Autodesk Product Help MCP (`search_help_content`, produkt: AutoCAD) zwróciły wprost:

- **`CONVTOSOLID` (Command)** — oficjalna dokumentacja: *"Eligible objects include: 3D meshes that completely enclose a volume, called watertight meshes... Cannot convert separate objects unless they enclose a volume without gaps."*
- **„Object is not watertight" przy `SURFSCULPT`** — błąd 105001, „Solid creation failed, no watertight volume detected", z sugerowanymi obejściami (Cap Holes Modifier w 3ds Max, `LOFT` z opcją Solid, `THICKEN`).
- **„Mesh not converted because it is not closed or it self-intersects"** — typowy błąd przy imporcie z aplikacji trzecich (np. SketchUp) — dokładnie nasz scenariusz (geometria BIM, nie natywny AutoCAD).
- **„How to convert a polyface mesh to a 3D solid"** — cała procedura (`FACETERSMOOTHLEV`, `MESHSMOOTH`, `EXPLODE`, `REGION`, `SURFSCULPT` lub `SMOOTHMESHCONVERT`+`CONVTOSOLID`) jest wieloetapowa, ręczna i explicite zastrzega: *"The solutions work for relatively simple and smaller objects."*
- Niezależne od tego, wątek Autodesk Community *„Unable to Convert Mesh to Solid, Mesh not Water Tight"* pokazuje użytkownika z realnym powodem biznesowym (clash detection w Navisworks liczy podwójne kolizje na cienkich meshach), który mimo intensywnych prób naprawy geometrii osiągał konwersję tylko „sporadycznie", kończąc na ręcznej rekonstrukcji `LOFT` per obiekt.

**Wniosek:** próba budowania `3DSOLID` w pluginie eksportu byłaby powtórzeniem dokładnie tego samego, notorycznie zawodnego kroku, którego unikają nawet zdeterminowani użytkownicy z dostępem do pełnego AutoCADa.

### 3. Wygląd „ocieniowany/solid" zależy od ustawienia widoku (VPORT), nie samej encji — nowe odkrycie

- Style wizualizacji (`2D Wireframe`, `Realistic`, `Conceptual`, `Shaded`, itd.) to ustawienie **per-viewport**, przechowywane niezależnie od encji (`VISUALSTYLES` command doc).
- Persystowany w DXF stan to tabela `VPORT` (i `VIEWPORT`/`VIEW`): grupa `281` = „Render mode" (`0`=2D wireframe klasyczny, `1`=wireframe, `2`=hidden line, `3`=flat shaded, `4`=Gouraud shaded, `5`/`6`=shaded+wireframe), opcjonalnie `348`/`292` = uchwyt do nazwanego obiektu `AcDbVisualStyle`.
- Nowe rysunki z domyślnego szablonu AutoCAD zwykle startują w 2D Wireframe / niewycieniowanym stylu — więc **samo posiadanie encji `PolyfaceMesh`/`Mesh` nie gwarantuje ocieniowanego wyglądu przy pierwszym otwarciu pliku**, dopóki użytkownik ręcznie nie zmieni stylu wizualizacji.
- **Rekomendacja praktyczna:** zapisać (lub nadpisać) wpis `*Active` w tabeli `VPORT` z `281`=`4` (Gouraud shaded), ewentualnie wskazując też jeden ze standardowych stylów (`Realistic`/`Conceptual`) jeśli writer to obsługuje. **Zweryfikowane bezpośrednio w ACadSharp:** klasa `Viewport` ma właściwość `RenderMode` (enum `RenderMode`) — mechanizm jest dostępny w wybranej bibliotece, wystarczy go użyć przy zapisie.
- Zastrzeżenie: rekomendacja oparta na dokumentacji drugorzędnej (nie ma bezpośredniego, autorytatywnego stwierdzenia Autodesk o zachowaniu przy *otwarciu* pliku) — **warto zweryfikować empirycznie** (wygenerować mały plik testowy z `VPORT` `281`=4 i otworzyć w prawdziwym AutoCAD/DWG TrueView) przed poleganiem na tym w planie.

### 4. Kolor: ACI vs true color — oba wspierane w ACadSharp, ale per-face tylko na `PolyfaceMesh`

Zweryfikowane bezpośrednio w źródle ACadSharp (`Color.cs`, `Entity.cs`, writer w `DxfSectionWriterBase.cs`):

- `Color` struct wspiera **ACI (indeks 0–257, grupa DXF `62`)** oraz **true color (24-bit RGB, grupa DXF `420`)** — `new Color((short)aciIndex)` vs `Color.FromTrueColor(0xRRGGBB)`. `Entity.Color` jest otagowane obiema grupami kodów i writer poprawnie przełącza się między nimi (`entity.Color.IsTrueColor` → pisze `420`, inaczej `62`).
- **`Mesh` (DXF „MESH"): tylko jeden kolor na całą encję.** DXF reference dokumentuje protokół nadpisań właściwości per-podencja (grupy `90`/`91`/`92`, typ `0`=Color), ale writer ACadSharp **na stałe zapisuje liczbę nadpisań jako `0`** — funkcja nie jest zaimplementowana. `Mesh.Faces` to tylko `List<int[]>` (indeksy wierzchołków), bez pola koloru.
- **`PolyfaceMesh`: kolor per-face jest realnie osiągalny.** Każdy `VertexFaceRecord` jest zapisywany jako **niezależna encja DXF** (dziedziczy pełne `Color`/`Layer` po `Entity`), więc `faceRecord.Color = Color.FromTrueColor(...)` faktycznie wygeneruje osobny kolor per trójkąt w pliku wyjściowym. Potwierdzone też oficjalną dokumentacją .NET API AutoCADa dla `FaceRecord`: *"you can... assign them to layers, or give them colors."* Niezależnie potwierdzone przez zmieniony fork `AHe.NetDxf`, który dokumentuje `PolyfaceMeshFace.Color`/`.Layer` jako nazwaną, świadomą funkcję.
- **Ograniczenie:** to jest kolor **płaski per trójkąt (per-face)**, nie gładkie, interpolowane per-wierzchołek cieniowanie (Gouraud) jak w glTF/GLB z S-01. Dla danych wejściowych `ExtractedTriangle` (per-vertex RGBA + fallback per-fragment) oznacza to spłaszczenie do jednego koloru na trójkąt — np. uśrednienie 3 wierzchołków lub użycie koloru fallback fragmentu — analogicznie do tego, jak Codemill opisuje własne opcje „ambient/diffuse/geometry-node color".
- **Niezweryfikowane:** czy realne AutoCAD faktycznie renderuje kolor per-face-record na `PolyfaceMesh` we wszystkich trybach wizualizacji (Realistic/Gouraud) w aktualnych wersjach — potwierdzona jest tylko *autorska* strona API, nie zrzut ekranu z renderowania. Warto zweryfikować empirycznie przed zamknięciem architektury na tej ścieżce.

### 5. `3DSOLID` w ACadSharp — potwierdzony brak wsparcia zapisu (na dziś)

Bezpośrednio z kodu źródłowego `DxfSectionWriterBase.Entities.cs` (branch `master`, sprawdzone na dzień researchu):

```csharp
case ProxyEntity:
case TableEntity:
case Solid3D:
case CadBody:
case Region:
    this.notify($"Entity type not implemented {entity.GetType().FullName}", NotificationType.NotImplemented);
    return false;
```

`Solid3D`/`Region`/`CadBody` są dziś **jawnie pomijane** przez writer DXF. Chronologia zmian: PR #191 (2025-09-24, tylko reader DWG) → PR #1139 (2026-07-09, **odczyt** surowego payloadu ACIS z DXF+DWG) → PR #1150 (**otwarty, niezmergowany** na 2026-07-26, dodałby zapis payloadu z powrotem). Nawet po zmergowaniu #1150, biblioteka tylko **odtworzy istniejący** payload ACIS bit-w-bit — nie potrafi wygenerować nowej geometrii SAT/SAB z dowolnej siatki trójkątów (nie zawiera jądra ACIS; `ModelerGeometry.AcisData` to czysty `byte[]`). Autorzy PR-ów jawnie ograniczają zakres do „payload preservation", nie „solid modeling" (odniesienie do issue #935).

**Wniosek:** `3DSOLID` nie jest dziś technicznie osiągalny przez ACadSharp dla naszego przypadku (dowolna siatka trójkątów z Navisworks), niezależnie od kwestii watertight.

### 6. Walidacja rynkowa — wszyscy konkurenci zatrzymują się na poziomie mesh

- **Codemill DWG™ Exporter** (Navisworks, wersja 2.0.1, 11/2025, wsparcie do 2026) — własna dokumentacja produktu (nie tylko marketing): *„Depending on settings the DWG Exporter will create 3D DWGs that contains either PolyFaceMeshes, SubDMeshes or Blocks."* Steruje źródłem koloru (ambient/diffuse/geometry-node) i layerem. **Brak jakiejkolwiek wzmianki o `3DSOLID`/ACIS** w całej dokumentacji.
- **AstraPlant DWG Exporter for Navisworks** (nowszy, wydany 2025-06-22, aktualizacja 2025-12-16) — jawnie oferuje 3 tryby: „Polyface Meshes, 3D Faces, or native Autodesk® AutoCAD® Solids", ale z explicit zastrzeżeniem producenta: *„Solid export requires AutoCAD installation and a closed mesh geometry. Other export formats work independently."* — czyli nawet konkurent oferujący opcję solid **wymaga do tego żywej instalacji AutoCADa** (prawdopodobnie odpala natywny `CONVTOSOLID`/faceter) **i** zamkniętej geometrii — dokładnie potwierdzając nasze ograniczenia.
- **Rhino → DWG** (natywny eksporter, dojrzały od dekad): schemat eksportu explicite pyta „Export surfaces as: Solid (3DSolid/SAT) or Polyface meshes" oraz „Export meshes as: Polyface mesh or exploded to individual 3DFaces" — te same dwie praktyczne opcje.
- **SketchUp → DWG** (natywny eksporter Trimble): oficjalna dokumentacja wprost mówi: *„SketchUp faces are exported as a triangulated polyface mesh with interior hidden lines to better simulate SketchUp geometry."* Zero wzmianki o solid/ACIS. Potwierdzone niezależnie przez artykuł pomocy Autodesk.
- **Automesher/Automapki (IFC→AutoCAD)** — nawet narzędzie wyspecjalizowane w BIM→CAD daje solid jako *opcję* z jawnym ostrzeżeniem: *„most imported meshes contain open boundaries or non-manifold edges that cause [CONVTOSOLID] to fail"*; ich wartość dodana to automatyczna naprawa geometrii przed próbą konwersji solid — osobny, większy zakres prac niż MVP S-04.

**Wniosek:** to nie jest niszowe obejście — to **branżowy standard** dla eksportu generycznej/BIM-owej geometrii do DWG. Żaden zbadany eksporter nie buduje ACIS solid bezpośrednio z dowolnej siatki bez przejścia przez zawodną, natywną konwersję AutoCADa.

### 7. Wersja DWG i limity skali

- Format DWG `AC1032` (AutoCAD 2018) jest **nadal natywnym formatem dla AutoCAD/Manage 2018 przez 2027 włącznie** (potwierdzone oficjalną tabelą kompatybilności Autodesk) — czyli w pełni pokrywa hosta docelowego S-04 (Manage 2026) i jest zgodny z zakresem zapisu `ACadSharp.DwgWriter` (AC1014–AC1032) z poprzedniego researchu.
- **Twardy limit `PolyfaceMesh`: ~32 767 wierzchołków na encję** (16-bitowy indeks w legacy strukturze danych, niezmieniony przez wszystkie rewizje formatu DWG — potwierdzone szczegółową analizą techniczną). Dla większych selekcji trzeba rozbić na wiele encji `PolyfaceMesh` (analogicznie do dedup fragmentów z S-01/`SelectionGeometryExtractor`).
- Wzorzec skalowania widoczny u konkurentów (Codemill, AstraPlant): (a) spawanie/dedup współdzielonych wierzchołków w obrębie fragmentu, (b) `BlockReference` do deduplikacji identycznej geometrii powtarzającej się w modelu (dokładnie ten sam problem instancjonowania, który S-01 rozwiązuje cache'em per-fragment), (c) rozbicie pojedynczego fragmentu na wiele encji tylko jeśli przekroczy limit wierzchołków `PolyfaceMesh`.
- Brak zmian w AutoCAD 2025/2026 release notes dotyczących encji `MESH`/`PolyFaceMesh`, `CONVTOSOLID` czy domyślnego zachowania renderowania przy otwarciu pliku — mechanizm z sekcji 3 pozostaje aktualny.

## Code References

Ten research nie modyfikuje ani nie cytuje kodu z tego repozytorium (plugin AutoCAD wciąż jest stubem — `NavisworksExport.AutoCad(.2026)/AutoCadExportCommand.cs` tylko pokazuje liczbę zaznaczonych obiektów, bez logiki eksportu ani referencji do ACadSharp) — potwierdzone przez `Glob`/`Read` przed rozpoczęciem badania. Poniżej referencje do zewnętrznych źródeł wykorzystanych do wniosków:

- `NavisworksExport.Geometry/ExtractedTriangle.cs` (istniejący, z S-01) — DTO per-vertex RGBA, punkt startowy dla mapowania koloru na `PolyfaceMesh.VertexFaceRecord.Color`.
- `github.com/DomCR/ACadSharp` — `Entities/PolyfaceMesh.cs`, `Entities/VertexFaceMesh.cs`, `Entities/VertexFaceRecord.cs`, `Entities/Mesh.cs`, `Entities/Solid3D.cs`, `Entities/ModelerGeometry.cs`, `Entities/Entity.cs`, `Color.cs`, `IO/DXF/DxfStreamWriter/DxfSectionWriterBase.Entities.cs`, `IO/DXF/DxfStreamWriter/DxfSectionWriterBase.cs`, `Entities/Viewport.cs` (via Context7 `/domcr/acadsharp` + bezpośrednie fetch z GitHub).
- ACadSharp GitHub issues/PRs: #794, #1011, #470, issue-1038, #1084, #1117, #191, #1139, #1150, #1155, #185, #197.
- Autodesk Help (przez `user-autodesk-product-help` MCP): `CONVTOSOLID (Command)`, „Object is not watertight… SURFSCULPT", „How to convert a polyface mesh to a 3D solid", „Mesh not converted because it is not closed or it self-intersects", „Unable to select solids… STL", „Visual style… Realistic/2D Wireframe" (5 artykułów), „Export lists of objects by color in Navisworks", „Color overrides from Revit… Navisworks".
- Autodesk DXF Reference: `MESH (DXF)`, `Polyface Meshes (DXF)`, R12 DXF Reference PDF, `POLYLINE (DXF)`, `REGION` (documentation.help mirror), `VPORT`/`VIEW` DXF reference, `EXPLODE` command reference, `Create Polyface Meshes (.NET)`.
- `ezdxf` docs — MESH internals, MeshBuilder (`render_mesh`/`render_polyface`/`render_3dfaces`/`render_3dsolid`), Face3d, GfxAttribs — użyte jako niezależna, dobrze udokumentowana implementacja referencyjna potwierdzająca semantykę DXF.
- Konkurenci: `apps.autodesk.com` (Codemill DWG Exporter, AstraPlant DWG Exporter), `codemill.fi`, `automapki.com` (Automesher/IFC-to-AutoCAD), `docs.mcneel.com` (Rhino `AcadSchemes`), `help.sketchup.com` + Autodesk support (SketchUp→AutoCAD import).
- Deelip.com — „The long and short of AutoCAD's PolyFace Mesh" (limit 32 767 wierzchołków).
- Autodesk Community/Developer Blog: `acdbGetObjectMesh` thread, „How to Convert Polyfacemesh to 3dSolid", „Unable to Convert Mesh to Solid, Mesh not Water Tight", ObjectARX `setVisualStyle` wątki.

## Architecture Insights

- **Rewizja wcześniejszej rekomendacji „DXF-first + ACadSharp"**: nadal aktualna, ale teraz z konkretnym wyborem encji: **`PolyfaceMesh`, nie `Mesh`**, ze względu na wsparcie koloru per-face w ACadSharp. To bezpośrednio odwraca początkową intuicję (nowszy format = lepszy) — w tym konkretnym wypadku starszy format ma bogatszą, w pełni zaimplementowaną funkcję kolorowania.
- **Ponowne wykorzystanie `NavisworksExport.Geometry`**: `ExtractedTriangle` (per-vertex RGBA + fallback) z S-01 mapuje się wprost na wejście `PolyfaceMesh` writer'a — jeden `VertexFaceMesh` per unikalny wierzchołek trójkąta (lub bez spawania, po prostu 3 na trójkąt) + jeden `VertexFaceRecord` per trójkąt z kolorem (np. uśrednienie 3 kolorów wierzchołków lub kolor fallback fragmentu, analogicznie do podejścia Codemill). To potwierdza wcześniejszą decyzję architektoniczną (format-agnostic geometry library) — pisanie DWG/DXF writer'a dla S-02/S-04 może być strukturalnie bardzo podobne do `GlbWriter` z S-01 (`NavisworksExport.Glb/GlbWriter.cs`), tylko z inną konwersją docelową (spłaszczenie koloru per-face zamiast per-vertex Gouraud).
- **Nowy krok w planie**: writer powinien jawnie ustawić `Viewport.RenderMode` (Gouraud shaded) na aktywnym viewport przy zapisie — inaczej plik może otworzyć się w 2D Wireframe i sprawiać wrażenie, że eksport „nie zadziałał" (użytkownik zobaczy tylko krawędzie, nie wypełnione, kolorowe trójkąty), mimo że dane są poprawne. To tania w implementacji poprawka jakości percepcyjnej, warta uwzględnienia w kryteriach akceptacji S-02/S-04 (analogicznie do „upright, not lying on its side" w planie S-01 GLB).
- **Limit skali `PolyfaceMesh` (~32k wierzchołków/encję)** to nowy, konkretny parametr do uwzględnienia w implementacji obok już znanego problemu dedup fragmentów (`SelectionGeometryExtractor`) — przy dużych selekcjach trzeba dzielić na wiele encji `PolyfaceMesh`, nie jedną.
- **ACadSharp jest młodą, aktywnie łatanaą biblioteką w newralgicznych miejscach**: writer `PolyfaceMesh` miał realny bug-fix w PR #1011 (2026-03-24, ~4 miesiące przed tym researchem), a `Mesh` entity miał fixy jeszcze w maju/czerwcu 2026. To nie dyskwalifikuje biblioteki (nadal najlepsza dostępna opcja open-source dla DWG+DXF w .NET), ale sugeruje, że **warto zaplanować wczesny, mały prototyp/harness** (analogicznie do `tools/GlbWriterHarness` użytego w S-01) do empirycznej weryfikacji: (a) czy `PolyfaceMesh` z wieloma kolorami per-face faktycznie renderuje się poprawnie w AutoCAD, (b) czy ustawienie `Viewport.RenderMode` faktycznie daje ocieniowany widok przy otwarciu.

## Historical Context (from prior changes)

- `context/changes/export-selection-autocad/research.md` — pierwszy przebieg badania (S-02, ogólne trendy): potwierdził brak natywnego eksportu Navisworks→DWG/DXF, wybrał ACadSharp jako bibliotekę, opisał ścieżkę ekstrakcji geometrii (COM API + `GenerateSimplePrimitives`) i zarekomendował DXF-first. Ten dokument (S-04) **rozszerza** tamten research o konkretny wybór typu encji (`PolyfaceMesh` > `Mesh` > `3DFACE`, nie `3DSOLID`) i mechanizm gwarantowanego ocieniowanego wyglądu (VPORT render mode) — nie zmienia wcześniejszych wniosków o ekstrakcji geometrii ani o samej bibliotece.
- `context/changes/export-selection-glb/plan.md` — zrealizowany plan S-01: `NavisworksExport.Geometry` (biblioteka format-agnostic, `ExtractedTriangle` z per-vertex RGBA + fallback per-fragment koloru), dedup fragmentów po COM `path`, `GlbWriter` jako osobny writer konsumujący DTO. Ten sam wzorzec (osobny writer, wspólna biblioteka ekstrakcji) ma bezpośrednie zastosowanie do przyszłego `DwgWriter`/`DxfWriter` w S-02/S-04 — potwierdzone też przez fakt, że `NavisworksExport.Geometry/ExtractedTriangle.cs` już istnieje i jest w pełni format-agnostic (żadnych typów glTF/DWG w środku), więc S-02/S-04 może go re-używać bez modyfikacji.
- `context/foundation/roadmap.md` — S-04 jest dziś `blocked` (zależy od dostarczenia S-02 na hoście 2023 najpierw, zgodnie ze świadomą decyzją kolejności `nw-plugin-scaffold-2026`/F-02). Ten research jest wejściem do planowania **obu** wycinków (S-02 i S-04), ponieważ wybór formatu/encji/koloru jest identyczny niezależnie od hosta (2023 vs 2026) — różni się tylko `Autodesk.Navisworks.Api.dll`/`ComApi` referencje (już rozwiązane wzorcem z F-02).
- `context/changes/nw-plugin-scaffold-2026/plan.md` — potwierdza, że `NavisworksExport.AutoCad.2026` to dziś czysty stub (kopia F-01), bez żadnej logiki eksportu ani referencji do ACadSharp — zgodne z tym, co potwierdzono na początku tego researchu (`Glob`/`Read` projektu).

## Related Research

- `context/changes/export-selection-autocad/research.md` — poprzedni, ogólniejszy research S-02 (ten dokument go rozszerza, nie zastępuje).

## Open Questions

1. **Czy AutoCAD faktycznie renderuje kolor per-face-record na `PolyfaceMesh` we wszystkich trybach wizualizacji 3D w aktualnych wersjach?** — Potwierdzona tylko autorska strona API (Autodesk .NET docs, ACadSharp writer). Wymaga empirycznej weryfikacji (mały wygenerowany plik testowy z 2-3 kolorowymi trójkątami, otwarty w prawdziwym AutoCAD/DWG TrueView) przed zamknięciem architektury S-02/S-04 na tej ścieżce. Owner: implementacja S-02. Block: no (można prototypować równolegle z resztą planu).
2. **Czy ustawienie `Viewport.RenderMode` (Gouraud shaded) w `*Active` VPORT faktycznie daje ocieniowany widok od razu przy otwarciu pliku w AutoCAD 2026?** — Rekomendacja oparta na wtórnych źródłach, nie autorytatywnym stwierdzeniu Autodesk o zachowaniu przy otwarciu. Wymaga tej samej empirycznej weryfikacji co pkt 1. Owner: implementacja S-02. Block: no.
3. **Strategia spłaszczenia koloru per-vertex → per-face**: uśrednienie 3 kolorów wierzchołków trójkąta, czy zawsze fallback na kolor fragmentu/materiału (jak `Rgba.Gray` w `ExtractedTriangle.cs`)? Wpłynie na wierność wizualną względem oryginalnego modelu Navisworks. Owner: plan S-02. Block: no.
4. **Podział dużych selekcji na wiele encji `PolyfaceMesh` przy przekroczeniu limitu ~32 767 wierzchołków** — czy robić to per-fragment (jak dedup w `SelectionGeometryExtractor`), czy per-arbitralny-batch trójkątów? Wymaga decyzji w planie, nie w tym researchu. Owner: plan S-02.
5. **DXF vs DWG jako pierwszy target** — nierozstrzygnięte jeszcze w poprzednim researchu (open question #1 tam), nadal aktualne; ten research nie zmienia tamtej rekomendacji (DXF-first).
6. Zależność S-04 od S-02 (świadomie odłożone w roadmapie) — nierozwiązana, ale poza zakresem tego researchu; oba wycinki mogą jednak współdzielić dokładnie tę samą decyzję o encji/kolorze/viewport wypracowaną tutaj.
