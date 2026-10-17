# Przyjęte założenia

## Wariant zadania

Nazwisko autorki zaczyna się na literę z zakresu A–K, dlatego zaimplementowano ograniczenie **równoległości** krawędzi, a domyślną klasą ciągłości przy zamianie krawędzi na segment Béziera jest **G1** (stała `VariantSettings.DefaultBezierContinuity`).

## Wielokąty i krawędzie

- Wielokąty mogą mieć samoprzecięcia. Wnętrze (do chwytania myszą) wyznacza reguła parzystości (even-odd).
- Wielokąt ma co najmniej 3 wierzchołki.
- Dodanie wierzchołka wstawia go w środku krawędzi. Krawędź Béziera jest dzielona algorytmem de Casteljau w t = 1/2, więc kształt krzywej się nie zmienia; łuk zamienia się w dwa odcinki.
- Usunięcie wierzchołka zastępuje dwie jego krawędzie jednym odcinkiem.
- W obu przypadkach ograniczenia krawędzi, które przestały istnieć, są usuwane. Wstawienie wierzchołka nie zmienia geometrii; po usunięciu wierzchołka solver odtwarza ciągłość w sąsiednich wierzchołkach (końce nowego odcinka pozostają na miejscu).
- Przy zamianie krawędzi na Béziera punkty kontrolne są umieszczane w 1/3 i 2/3 cięciwy (lekko odsunięte w bok), a następnie dopasowywane do ciągłości sąsiadów.

## Ograniczenia

- Długość i równoległość krawędzi zakrzywionej dotyczą jej **cięciwy** (odcinka między wierzchołkami).
- Równoległość dopuszcza zgodny i przeciwny kierunek krawędzi; solver zachowuje bieżącą orientację.
- Nie można uczynić równoległymi: krawędzi z samą sobą, dwóch sąsiednich krawędzi jednego wielokąta (zlałyby się w jedną prostą) ani tej samej pary dwa razy.
- Krawędź może mieć jedno ograniczenie długości i dowolnie wiele ograniczeń równoległości.
- Przy dodawaniu długości nieruchomy pozostaje wierzchołek początkowy krawędzi; przy dodawaniu równoległości – pierwsza wybrana krawędź.
- Zmiana niemożliwa do spełnienia jest cofana (geometria wraca do stanu sprzed zmiany).

## Ciągłość

- Klasa ciągłości jest atrybutem **wierzchołka** (domyślnie G0) i ma znaczenie tylko wtedy, gdy sąsiaduje on z krzywą.
- Klasa wybrana przez użytkownika jest zapamiętywana jako „jawna” i nie jest nadpisywana wartością domyślną przy zamianie krawędzi na Béziera.
- Dla łuku dozwolone są tylko G0 i G1, a G1 może mieć tylko jeden koniec łuku. Przy zamianie krawędzi na łuk C1 na końcu staje się G1, a jeśli oba końce mają G1, koniec łuku przechodzi na G0.
- Łuk nie ma własnych parametrów – jest wyznaczany z wierzchołków i ciągłości:
  - G0 na obu końcach: półokrąg o promieniu równym połowie cięciwy, rysowany po lewej stronie krawędzi (patrząc od wierzchołka początkowego do końcowego);
  - G1 na jednym końcu: łuk styczny w tym końcu do sąsiedniej krawędzi i przechodzący przez drugi koniec; gdy styczna pokrywa się z cięciwą (promień nieskończony), krawędź rysowana jest jako odcinek.

## Edycja myszą

- Przeciągany element jest „przypięty” – zawsze znajduje się dokładnie pod kursorem.
- Przy przeciąganiu wierzchołka sąsiednie punkty kontrolne przesuwają się razem z nim (zachowują kształt krzywych przy wierzchołku).
- Przy przeciąganiu punktu kontrolnego najpierw przypinane są także wierzchołki jego segmentu. Dopiero gdy przy takim przypięciu ograniczeń nie da się spełnić (przypadek wyjątkowy), wierzchołki są zwalniane. Przykład: C1 obok odcinka o stałej długości L wymaga |V − P| = L/3, więc przesunięcie punktu kontrolnego P musi przesunąć wierzchołek V.
- Ostateczność: jeśli ograniczeń nie da się spełnić, cały wielokąt jest przesuwany równolegle (translacja zachowuje każde ograniczenie i każdą ciągłość). Pasek stanu informuje o tym.

## Rysowanie

- Wybrany algorytm (biblioteczny / Bresenham) dotyczy wszystkich linii. Znaczniki wierzchołków i etykiety są elementami pomocniczymi i zawsze rysuje je GDI+.
- Krzywe rysowane są jako łamane (ok. 4 piksele na odcinek).
