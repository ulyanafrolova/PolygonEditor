# Przyjęte założenia

## Wariant zadania

Wariant zadania - A–K, więc zaimplementowałam ograniczenie równoległości krawędzi, a domyślną klasą ciągłości przy zamianie krawędzi na segment Béziera jest G1 (stała VariantSettings.DefaultBezierContinuity)

## Wielokąty i krawędzie

- Wielokąty mogą mieć samoprzecięcia. Wnętrze (do chwytania myszą) wyznacza reguła parzystości (even-odd).
- Wielokąt ma co najmniej 3 wierzchołki.
- Dodanie wierzchołka wstawia go w środku krawędzi, a dla krzywych – w punkcie P(1/2) na krzywej. Krawędź Béziera jest dzielona algorytmem de Casteljau w t = 1/2, więc kształt krzywej się nie zmienia. Łuk zamienia się w dwa odcinki przechodzące przez środek łuku (łuk jest wyznaczany z końców i ciągłości, więc jego połówki nie mogłyby zachować tego samego okręgu).
- Usunięcie wierzchołka zastępuje dwie jego krawędzie jednym odcinkiem.
- W obu przypadkach ograniczenia krawędzi, które przestały istnieć, są usuwane, a solver odtwarza relacje w sąsiednich wierzchołkach (wierzchołki dzielonej lub nowej krawędzi pozostają na miejscu). Przy podziale Béziera jest to konieczne mimo zachowania kształtu: każda połówka ma na starych końcach tylko połowę dawnego wektora stycznego, co łamie C1. Jeśli relacji nie da się odtworzyć, zmiana topologii zostaje, geometria wraca do stanu tuż po zmianie i pojawia się komunikat.
- Przy zamianie krawędzi na Béziera punkty kontrolne są umieszczane w 1/3 i 2/3 cięciwy (lekko odsunięte w bok), a następnie dopasowywane do ciągłości sąsiadów.

## Ograniczenia

- Długość i równoległość krawędzi zakrzywionej dotyczą jej **cięciwy** (odcinka między wierzchołkami).
- Równoległość dopuszcza zgodny i przeciwny kierunek krawędzi; solver zachowuje bieżącą orientację.
- Równoległość jest przechodnia (a ∥ c i c ∥ b daje a ∥ b), więc sprawdzane są całe klasy wzajemnie równoległych krawędzi. Nie można uczynić równoległymi: krawędzi z samą sobą, krawędzi już równoległych (także pośrednio) ani takiej pary, po której dodaniu dwie sąsiednie krawędzie jednego wielokąta byłyby równoległe – bezpośrednio lub przez inne równoległości (zlałyby się w jedną prostą).
- Krawędź może mieć jedno ograniczenie długości i dowolnie wiele ograniczeń równoległości.
- Przy dodawaniu długości nieruchomy pozostaje wierzchołek początkowy krawędzi; przy dodawaniu równoległości – pierwsza wybrana krawędź.
- Jeśli druga krawędź należy do innego wielokąta, ten wielokąt jest najpierw obracany jako całość (bryła sztywna) wokół środka tej krawędzi o mniejszy z dwóch kątów, które czynią ją równoległą. Dzięki temu jego kształt, krzywe i własne ograniczenia się nie zmieniają; solver poprawia tylko to, co jeszcze trzeba.
- Zmiana niemożliwa do spełnienia jest cofana (geometria wraca do stanu sprzed zmiany).

## Ciągłość

- Klasa ciągłości jest atrybutem **wierzchołka** (domyślnie G0) i ma znaczenie tylko wtedy, gdy sąsiaduje on z krzywą.
- Klasa wybrana przez użytkownika jest zapamiętywana jako „jawna” i nie jest nadpisywana wartością domyślną przy zamianie krawędzi na Béziera. Wartość domyślna jest nadawana tylko wtedy, gdy pozwalają na to reguły (np. drugi koniec łuku, który ma już G1 na pierwszym końcu, pozostaje G0).
- Dla łuku dozwolone są tylko G0 i G1, a G1 może mieć tylko jeden koniec łuku.
- Nie jest dozwolone G1 w wierzchołku między dwoma łukami: łuk z G1 bierze styczną od sąsiada, więc dwa łuki czekałyby na siebie nawzajem, a wspólny kierunek nie byłby wyznaczony.
- Przy zamianie krawędzi na łuk: C1 na końcu staje się G1, G1 między dwoma łukami staje się G0, a jeśli oba końce mają G1, koniec łuku przechodzi na G0.
- Wszystkie te reguły są w jednym miejscu (`ContinuityRules`), a spełnienie ciągłości jest sprawdzane na rzeczywistych wektorach stycznych krzywych – także łuków.
- Łuk nie ma własnych parametrów – jest wyznaczany z wierzchołków i ciągłości:
  - G0 na obu końcach: półokrąg o promieniu równym połowie cięciwy, rysowany po lewej stronie krawędzi (patrząc od wierzchołka początkowego do końcowego);
  - G1 na jednym końcu: łuk styczny w tym końcu do sąsiedniej krawędzi i przechodzący przez drugi koniec; gdy styczna pokrywa się z cięciwą (promień nieskończony), krawędź rysowana jest jako odcinek.
  - Jeśli drugi koniec leży „za” styczną, łuk musi zatoczyć pętlę (ponad 180°), a gdy ten koniec zbliża się do prostej stycznej, promień rośnie bez ograniczeń. Pętle o promieniu większym niż 2,5 długości cięciwy są traktowane jako niedopuszczalna geometria: zmiana, która by je wytworzyła, jest odrzucana, a przy przeciąganiu cały wielokąt jest przesuwany zamiast odkształcany. Łuki do półokręgu (choćby bardzo płaskie) są zawsze dopuszczalne.

## Edycja myszą

- Przeciągany element jest „przypięty” – zawsze znajduje się dokładnie pod kursorem.
- Przy przeciąganiu wierzchołka sąsiednie punkty kontrolne przesuwają się razem z nim (zachowują kształt krzywych przy wierzchołku).
- Przy przeciąganiu punktu kontrolnego najpierw przypinane są także wierzchołki jego segmentu. Dopiero gdy przy takim przypięciu ograniczeń nie da się spełnić (przypadek wyjątkowy), wierzchołki są zwalniane. Przykład: C1 obok odcinka o stałej długości L wymaga |V − P| = L/3, więc przesunięcie punktu kontrolnego P musi przesunąć wierzchołek V. W scenie demonstracyjnej taka sytuacja nie występuje – ograniczenie długości w W1 leży na krawędzi niesąsiadującej z krzywą.
- Przy G1 przesunięcie punktu kontrolnego obraca sąsiedni odcinek wokół wspólnego wierzchołka o ten sam kąt, o który obrócił się uchwyt. Krótki uchwyt i długi odcinek działają jak dźwignia: mały ruch kursora blisko wierzchołka może mocno przesunąć drugi koniec odcinka (a przez równoległość – także krawędź innego wielokąta). To poprawna geometria G1, a nie niestabilność solvera.
- Ostateczność: jeśli ograniczeń nie da się spełnić, cały wielokąt jest przesuwany równolegle (translacja zachowuje każde ograniczenie i każdą ciągłość). Pasek stanu informuje o tym.

## Rysowanie

- Wybrany algorytm (biblioteczny / Bresenham) dotyczy wszystkich linii. Znaczniki wierzchołków i etykiety są elementami pomocniczymi i zawsze rysuje je GDI+.
- Krzywe rysowane są jako łamane (ok. 4 piksele na odcinek).
