# Instrukcja obsługi

Po uruchomieniu programu wyświetla się scena demonstracyjna z trzema wielokątami. Wielokąty nie są tworzone ani usuwane przez użytkownika – pochodzą ze sceny początkowej (plik `Scenes/InitialScene.cs`).

## Mysz

- **LPM na wierzchołku** – przeciąganie wierzchołka. Wierzchołek zawsze podąża za kursorem; sąsiednie punkty kontrolne przesuwają się razem z nim, a pozostałe elementy dopasowują się do ograniczeń.
- **LPM na punkcie kontrolnym** (pomarańczowy) – przeciąganie punktu kontrolnego segmentu Béziera. Wierzchołki tego segmentu pozostają na miejscu, chyba że ograniczenia tego wymagają (zakładka „Założenia”).
- **LPM na krawędzi lub wewnątrz wielokąta** – przesuwanie całego wielokąta.
- **PPM** – menu kontekstowe elementu pod kursorem.

## Menu kontekstowe

Na wierzchołku:

- **Usuń wierzchołek** – niedostępne, gdy wielokąt ma tylko 3 wierzchołki.
- **Ciągłość → G0 / G1 / C1** – widoczne, gdy wierzchołek sąsiaduje z krzywą. Niedozwolone klasy są wyszarzone.

Na krawędzi (albo na jej punkcie kontrolnym):

- **Dodaj wierzchołek w środku**.
- **Odcinek / Krzywa Béziera / Łuk okręgu** – rodzaj krawędzi.
- **Ustal długość…** – okno z długością (domyślnie bieżąca). Jeśli ograniczenie już istnieje, pozycja nazywa się „Zmień długość” i zmienia jego wartość.
- **Równoległa do…** – po wybraniu należy kliknąć LPM drugą krawędź (może należeć do innego wielokąta). Esc anuluje wybór.
- **Usuń ograniczenie** – lista ograniczeń tej krawędzi.

Jeżeli zmiany nie da się pogodzić z istniejącymi ograniczeniami, jest ona cofana i pojawia się komunikat.

## Klawiatura

- **F1** – ta dokumentacja.
- **Delete** – usunięcie wierzchołka pod kursorem.
- **Esc** – anulowanie przeciągania (przywraca stan sprzed chwycenia elementu) lub anulowanie wyboru drugiej krawędzi.

## Panel boczny

- **Rysowanie odcinków** – wybór algorytmu: biblioteczny (GDI+, z wygładzaniem) albo własna implementacja algorytmu Bresenhama. Wybór dotyczy wszystkich linii: krawędzi, krzywych (rysowanych jako łamane) i wieloboków Béziera.
- **Dokumentacja (F1)**.

## Oznaczenia na scenie

- Czerwone punkty – wierzchołki; pomarańczowe – punkty kontrolne Béziera.
- Szara linia przerywana – wielobok Béziera.
- Czarne linie – odcinki; niebieskie – krzywe Béziera i łuki.
- Zielona obwódka / zielony kolor – element pod kursorem lub przeciągany.
- Żółte etykiety przy środku krawędzi – ograniczenia: liczba = zadana długość, **∥n** = równoległość (obie krawędzie pary mają ten sam numer n).
- Zielone etykiety przy wierzchołkach – klasa ciągłości (tylko przy wierzchołkach sąsiadujących z krzywą).

Pasek stanu na dole pokazuje element pod kursorem i podpowiedzi.
