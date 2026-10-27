# Instrukcja obsługi

Po uruchomieniu programu wyświetla się scena demonstracyjna z trzema wielokątami (plik `Scenes/InitialScene.cs`). Zgodnie z zadaniem wielokąty nie są tworzone ani usuwane przez użytkownika – pochodzą ze sceny początkowej albo z wczytanego pliku sceny.

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
- **Grubość → 1–10 px** – grubość rysowania tej krawędzi (odcinka, krzywej lub łuku). Nie wpływa na geometrię ani ograniczenia; nowe krawędzie powstałe przy dodawaniu wierzchołka dziedziczą grubość.
- **Ustal długość…** – okno z długością (domyślnie bieżąca). Jeśli ograniczenie już istnieje, pozycja nazywa się „Zmień długość” i zmienia jego wartość. Na krawędzi zakrzywionej pozycja i okno mówią o długości cięciwy.
- **Równoległa do…** – po wybraniu należy kliknąć LPM drugą krawędź (może należeć do innego wielokąta). Esc anuluje wybór.
- **Usuń ograniczenie** – lista ograniczeń tej krawędzi.

Jeżeli zmiany nie da się pogodzić z istniejącymi ograniczeniami, jest ona cofana i pojawia się komunikat.

## Klawiatura

- **F1** – ta dokumentacja.
- **Ctrl+S** – zapis sceny do pliku, **Ctrl+O** – wczytanie sceny z pliku.
- **Delete** – usunięcie wierzchołka pod kursorem.
- **Esc** – anulowanie przeciągania (przywraca stan sprzed chwycenia elementu) albo wyboru drugiej krawędzi.

## Menu (u góry okna)

- **Plik → Zapisz scenę… (Ctrl+S) / Wczytaj scenę… (Ctrl+O)** – zapis i odczyt całej sceny (wielokąty, rodzaje krawędzi, punkty kontrolne, ciągłość, ograniczenia, grubości) w pliku JSON. Niepoprawny plik nie zmienia bieżącej sceny – pojawia się komunikat.
- **Pomoc → Dokumentacja (F1)**.

## Pasek narzędzi (pod menu)

- **Rysowanie odcinków** – przyciski radiowe, wybór algorytmu: biblioteczny (GDI+, z wygładzaniem), własna implementacja algorytmu Bresenhama albo własna implementacja algorytmów Wu z antyaliasingiem (odcinki i łuki okręgów). Wybór dotyczy wszystkich linii: krawędzi, krzywych Béziera (rysowanych jako łamane), łuków i wieloboków Béziera. W wąskim oknie przyciski przechodzą do drugiego wiersza.
- Grubość krawędzi: w trybie bibliotecznym realizuje ją GDI+ (szerokość pióra), w trybach własnych – ręcznie (Bresenham: powielanie pikseli, Wu: pasmo z antyaliasingiem; w obu łamane mają zaokrąglone łączenia). W scenie początkowej grubsze są: krzywa Béziera W1 (4 px), łuk W2 (6 px), krawędź 3 W2 (3 px) i łuk W3 (3 px) – po przełączeniu algorytmu widać różnicę.

Gdy okno jest mniejsze niż scena, pojawiają się paski przewijania.

## Oznaczenia na scenie

- Czerwone punkty – wierzchołki; pomarańczowe – punkty kontrolne Béziera.
- Szara linia przerywana – wielobok Béziera.
- Czarne linie – odcinki; niebieskie – krzywe Béziera i łuki.
- Zielona obwódka / zielony kolor – element pod kursorem lub przeciągany.
- Żółte etykiety przy środku krawędzi – ograniczenia: **↔L** = zadana długość L (na krawędzi zakrzywionej z dopiskiem „cięciwa”), **∥n** = równoległość (obie krawędzie pary mają ten sam numer n).
- Zielone etykiety przy wierzchołkach – klasa ciągłości (tylko przy wierzchołkach sąsiadujących z krzywą).

Pasek stanu na dole pokazuje element pod kursorem i podpowiedzi.
