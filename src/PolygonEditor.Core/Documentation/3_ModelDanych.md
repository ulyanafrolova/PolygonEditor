# Model danych i architektura

## Warstwy

- **PolygonEditor.Core** – cała logika, bez zależności od WinForms: model, geometria, ograniczenia i solver, edycja, rasteryzacja, scena początkowa, dokumentacja.
- **PolygonEditor.App** – interfejs WinForms: okno, płótno, menu, okna dialogowe. Przekazuje zdarzenia do warstwy Core i wyświetla wynik.

Dzięki temu algorytmy są testowane testami jednostkowymi (projekt `PolygonEditor.Core.Tests`).

## Model (folder Model)

- **Scene** – korzeń modelu: lista wielokątów i lista ograniczeń. Ograniczenia są przechowywane w scenie, a nie w krawędziach, ponieważ jedno ograniczenie może łączyć krawędzie różnych wielokątów.
- **Polygon** – nazwa (W1, W2, …), lista wierzchołków i lista krawędzi. Krawędź i łączy wierzchołek i z wierzchołkiem (i + 1) mod n.
- **Vertex** – pozycja, klasa ciągłości (G0 / G1 / C1) i znacznik, czy klasę ustawił użytkownik.
- **Edge** – wierzchołek początkowy i końcowy, rodzaj (`Line`, `Bezier`, `Arc`), punkty kontrolne V1, V2 (używane dla Béziera; V0 i V3 to wierzchołki krawędzi) oraz grubość rysowania (1–10 px).
- **ContinuityRules** – jedno źródło prawdy o ciągłości: dozwolone klasy (łuk: bez C1, G1 tylko na jednym końcu), przeliczanie wartości domyślnych (niejawnych) po zmianie rodzaju krawędzi lub topologii, normalizacja końców łuku oraz pomiar ciągłości na rzeczywistych wektorach stycznych.

Metody zmieniające model (pozycje, rodzaj krawędzi, ciągłość, dodawanie ograniczeń) są `internal`: z zewnątrz biblioteki model można zmieniać tylko przez `SceneEditor`, który pilnuje reguł i uruchamia solver.

Końce krawędzi są niezmienne. Wstawienie lub usunięcie wierzchołka tworzy nowe obiekty krawędzi, więc ograniczenia „starych” krawędzi łatwo odnaleźć i usunąć.

## Dane wyprowadzane (nieprzechowywane)

- **Łuk** – środek, promień i kąty są liczone z wierzchołków i ciągłości (`ArcGeometry`), więc łuk zawsze jest zgodny z sąsiadami.
- **Relacje ciągłości G1/C1** dla segmentów Béziera – tworzone przy każdym uruchomieniu solvera na podstawie klas ciągłości wierzchołków i rodzajów krawędzi (`ContinuityConstraint`).

## Ograniczenia (folder Constraints)

- **Constraint** – klasa bazowa: numer, lista krawędzi, tekst ikony, opis, błąd (`Error`), tolerancja i metoda `Project` (rzutowanie, patrz „Algorytmy”).
- **FixedLengthConstraint** – zadana długość krawędzi.
- **ParallelConstraint** – równoległość dwóch krawędzi.
- **Solver/** – `ConstraintSolver`, `ContinuityConstraint`, `PointRef` (jednolite odwołanie do wierzchołka lub punktu kontrolnego), `SolverContext` (wagi punktów podczas rozwiązywania).

Nowy rodzaj ograniczenia wymaga tylko nowej klasy z metodami `Error` i `Project`.

## Edycja (folder Editing)

- **SceneEditor** – jedyny punkt zmian modelu (mysz, menu, klawiatura). Po każdej zmianie uruchamia solver; obsługuje próby z różnym przypinaniem i cofanie zmian.
- **EditorController** – maszyna stanów myszy (najechanie ⇄ przeciąganie, tryb wyboru drugiej krawędzi), niezależna od WinForms.
- **HitTester** – wyszukiwanie elementu pod kursorem.
- **GeometrySnapshot** – zapis pozycji wszystkich punktów (anulowanie Esc, cofanie zmian).

## Rysowanie (folder Rendering)

- **IRenderTarget** – powierzchnia rysowania (SetPixel, linia biblioteczna, etykiety); implementuje ją warstwa App (`BitmapRenderTarget` na buforze `DirectBitmap`).
- **ILineDrawer** – strategia rysowania odcinka, łamanej i łuku (z grubością): `LibraryLineDrawer`, `BresenhamLineDrawer` albo `WuLineDrawer` (antyaliasing; łuki rysuje bezpośrednio algorytmem Wu dla okręgu, pozostałe strategie – jako łamane).
- **CurveRasterizer** – zamiana krzywych na łamane.
- **SceneRenderer** – rysuje całą scenę.

## Zapis i odczyt sceny (folder Persistence)

- **SceneFile** – zapis i odczyt sceny w formacie JSON. Plik zawiera wielokąty (wierzchołki z ciągłością i znacznikiem „ustawione przez użytkownika”, krawędzie z rodzajem, punktami kontrolnymi Béziera i grubością) oraz ograniczenia z numerami; krawędzie ograniczeń wskazywane są parą (numer wielokąta, numer krawędzi).
- Łuki nie mają dodatkowych danych – ich kształt wynika z wierzchołków i ciągłości.
- Pozycje zapisywane są dokładnie, więc wczytana scena spełnia ograniczenia bez ponownego uruchamiania solvera.
- Przy odczycie sprawdzane są: format i wersja, liczba wierzchołków i krawędzi, poprawność odwołań ograniczeń, długości dodatnie, zakres grubości oraz dozwolone klasy ciągłości. Błąd daje `InvalidDataException`, a bieżąca scena pozostaje bez zmian.

Przykład fragmentu pliku:

```
{
  "format": "PolygonEditor.Scene",
  "version": 1,
  "polygons": [ { "vertices": [ { "x": 120, "y": 140, "continuity": "G0", "explicit": false }, … ],
                  "edges": [ { "kind": "Line", "thickness": 1 }, { "kind": "Bezier", "control1": { … }, "control2": { … }, "thickness": 4 }, … ] } ],
  "constraints": [ { "type": "Parallel", "id": 2, "edges": [ { "polygon": 0, "edge": 0 }, { "polygon": 1, "edge": 2 } ] } ]
}
```

## Scena początkowa

Plik `Scenes/InitialScene.cs`. Dodanie wielokąta to jedno wywołanie, a krawędzie numerowane są od 0:

```
var w4 = scene.AddPolygon((600, 600), (820, 540), (760, 700));
w4.Edges[0].MakeBezier();
scene.AddFixedLength(w4.Edges[1]);
scene.AddParallel(w4.Edges[2], w1.Edges[3]);
```

W przykładzie krawędź 2 trójkąta W4, od (760, 700) do (600, 600), ma kierunek (−160, −100) – taki sam jak krawędź 3 wielokąta W1, od (250, 420) do (90, 320) – więc równoległość jest spełniona już przez podane współrzędne.

Na końcu `Create` uruchamiany jest solver (`Settle`):

- najpierw z przypiętymi wszystkimi wierzchołkami – wierzchołki zostają dokładnie tam, gdzie je zapisano, a dopasowywane są tylko punkty kontrolne (do wymaganej ciągłości), więc punkty kontrolne wystarczy podać w przybliżeniu;
- jeśli zapisane wierzchołki nie spełniają długości lub równoległości (współrzędne przybliżone), przypinany jest tylko pierwszy wierzchołek każdego wielokąta, a solver poprawia pozostałe.

Wyjątek pojawia się tylko przy ograniczeniach wzajemnie sprzecznych. Przykład z tej strony sprawdza test `DocumentationExample_AddsPolygonWithoutMovingVertices`, a całą scenę – test `InitialScene_SatisfiesItsConstraints`.
