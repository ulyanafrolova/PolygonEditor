# Model danych i architektura

## Warstwy

- **PolygonEditor.Core** – cała logika, bez zależności od WinForms: model, geometria, ograniczenia i solver, edycja, rasteryzacja, scena początkowa, dokumentacja.
- **PolygonEditor.App** – interfejs WinForms: okno, płótno, menu, okna dialogowe. Przekazuje zdarzenia do warstwy Core i wyświetla wynik.

Dzięki temu algorytmy są testowane testami jednostkowymi (projekt `PolygonEditor.Core.Tests`).

## Model (folder Model)

- **Scene** – korzeń modelu: lista wielokątów i lista ograniczeń (usunięcie wielokąta usuwa też jego ograniczenia). Ograniczenia są przechowywane w scenie, a nie w krawędziach, ponieważ jedno ograniczenie może łączyć krawędzie różnych wielokątów.
- **Polygon** – nazwa (W1, W2, …), lista wierzchołków i lista krawędzi. Krawędź i łączy wierzchołek i z wierzchołkiem (i + 1) mod n.
- **Vertex** – pozycja, klasa ciągłości (G0 / G1 / C1) i znacznik, czy klasę ustawił użytkownik.
- **Edge** – wierzchołek początkowy i końcowy, rodzaj (`Line`, `Bezier`, `Arc`) oraz punkty kontrolne V1, V2 (używane dla Béziera; V0 i V3 to wierzchołki krawędzi).
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
- **EditorController** – maszyna stanów myszy (najechanie ⇄ przeciąganie, tryb wyboru drugiej krawędzi, tryb tworzenia wielokąta), niezależna od WinForms.
- **HitTester** – wyszukiwanie elementu pod kursorem.
- **GeometrySnapshot** – zapis pozycji wszystkich punktów (anulowanie Esc, cofanie zmian).

## Rysowanie (folder Rendering)

- **IRenderTarget** – powierzchnia rysowania (SetPixel, linia biblioteczna, etykiety); implementuje ją warstwa App (`BitmapRenderTarget` na buforze `DirectBitmap`).
- **ILineDrawer** – strategia rysowania odcinka: `LibraryLineDrawer` albo `BresenhamLineDrawer`.
- **CurveRasterizer** – zamiana krzywych na łamane.
- **SceneRenderer** – rysuje całą scenę.

## Scena początkowa

Plik `Scenes/InitialScene.cs`. Dodanie wielokąta to jedno wywołanie, a krawędzie numerowane są od 0:

```
var w4 = scene.AddPolygon((100, 600), (250, 580), (300, 700));
w4.Edges[0].MakeBezier();
scene.AddFixedLength(w4.Edges[1]);
scene.AddParallel(w4.Edges[2], w1.Edges[3]);
```

Punkty kontrolne wystarczy podać w przybliżeniu: na końcu `Create` solver (z przypiętymi wszystkimi wierzchołkami) dopasowuje je do wymaganej ciągłości. Długości i równoległości muszą być spełnione przez same wierzchołki – w przeciwnym razie tworzenie sceny kończy się wyjątkiem, a test `InitialScene_SatisfiesItsConstraints` to wykrywa.
