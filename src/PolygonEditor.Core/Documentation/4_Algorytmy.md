# Algorytmy

## Solver ograniczeń

### Idea

Każda relacja (ograniczenie użytkownika lub relacja ciągłości) umie się „zrzutować”: przesunąć swoje wolne punkty tak, aby sama była spełniona. Solver powtarza rzutowania wszystkich relacji po kolei (metoda Gaussa–Seidla), aż wszystkie będą spełnione albo zostanie przekroczony limit 300 iteracji.

Relacje:

- ograniczenia użytkownika ze sceny (długość, równoległość);
- relacje ciągłości G1/C1 wyprowadzane z wierzchołków, przy których styka się segment Béziera z odcinkiem lub innym segmentem Béziera. Łuki nie potrzebują relacji – łuk z G1 jest konstruowany z wektora stycznego sąsiada. Gdy sąsiad też jest łukiem, jest on półokręgiem (jego drugi koniec ma G0, a G1 w stronę tego łuku jest pomijane), więc rekurencja ma głębokość co najwyżej 1.

Solver po spełnieniu relacji sprawdza jeszcze ciągłość zmierzoną na łukach. Jedyny przypadek, w którym łuk G1 nie może jej spełnić, to drugi koniec leżący na prostej stycznej za wierzchołkiem; wtedy wynik jest „nierozwiązywalny” i edytor stosuje zwykłe wyjście awaryjne (cofnięcie zmiany albo translacja wielokąta).

Pełna kontrola spójności (`ConstraintSolver.AllSatisfied`, używana w testach) sprawdza dodatkowo, że każda klasa ciągłości jest dozwolona i że ciągłość zmierzona na rzeczywistych krzywych (łącznie z łukami) jest spełniona w każdym wierzchołku.

Kryterium spełnienia: długość ±0,05 px, równoległość |sin kąta| ≤ 10⁻⁴, G1 – różnica wektorów jednostkowych ≤ 10⁻⁴, C1 – różnica wektorów stycznych ≤ 0,05.

### Kto się przesuwa – wagi

Element trzymany przez użytkownika jest przypięty (waga 0). Pozostałe punkty dostają wagę zależną od odległości od punktów przypiętych w grafie zależności:

- krawędź wielokąta (wierzchołek–wierzchołek) i połączenie wierzchołek–punkt kontrolny: koszt 1;
- połączenie przez relację ciągłości: koszt 1;
- połączenie przez ograniczenie użytkownika (często do innego wielokąta): koszt 2.

Odległości liczy algorytm Dijkstry, waga = odległość². Im punkt dalej od kursora, tym większą część poprawek przejmuje. Zmiany „rozchodzą się” od przeciąganego elementu, a przeciągany wielokąt zachowuje kształt, podczas gdy wielokąt z nim powiązany się dopasowuje.

Przesunięcie wierzchołka przez solver przesuwa też jego sąsiednie punkty kontrolne (o ile nie są przypięte).

### Rzutowanie: długość

Końce A, B przesuwane są wzdłuż prostej AB; błąd e = |AB| − L dzielony jest proporcjonalnie do wag: A o e·wA/(wA+wB) do środka, B o e·wB/(wA+wB) do środka. Przypięty koniec się nie rusza.

### Rzutowanie: równoległość

Kierunek wspólny wyznacza mniej ruchliwa krawędź (suma wag końców) – bliższa przeciąganemu elementowi; przy równej ruchliwości – kierunek średni. Każda krawędź jest obracana do tego kierunku z zachowaniem długości, wokół punktu (A·wB + B·wA)/(wA + wB) – przypięty koniec jest więc środkiem obrotu.

### Rzutowanie: ciągłość G1 i C1

W wierzchołku V każda strona jest opisana „uchwytem” H i współczynnikiem k, tak że wektor styczny wynosi k_in·(V − H_in) dla krawędzi wchodzącej i k_out·(H_out − V) dla wychodzącej:

- segment Béziera: H = sąsiedni punkt kontrolny, k = 3 (bo P′(0) = 3(V1 − V0), P′(1) = 3(V3 − V2));
- odcinek: H = drugi wierzchołek odcinka, k = 1 (bo P′(t) = B − A).

**G1** (zgodny kierunek): bardziej ruchliwy uchwyt jest obracany wokół V na prostą wyznaczoną przez drugi uchwyt, z zachowaniem odległości od V (dzięki temu odcinek o stałej długości nadal ją ma). Preferowane są punkty kontrolne (współczynnik 1), a nie dalekie wierzchołki odcinków (0,1). Gdy oba uchwyty są przypięte, V jest przesuwany na odcinek między nimi.

**C1** (równe wektory) to warunek liniowy f = (k_in + k_out)·V − k_in·H_in − k_out·H_out = 0. Stosowane jest rzutowanie ważone o minimalnej zmianie (jak w position-based dynamics):

x_i ← x_i − w_i·g_i·f / Σ w_j·g_j²,

gdzie gradient g wynosi k_in + k_out dla V, −k_in dla H_in i −k_out dla H_out. Jeden krok daje f = 0. Wagi uwzględniają preferencje: punkt kontrolny 1, daleki wierzchołek odcinka 0,1, sam wierzchołek V 0,25 (ruszany w ostateczności, np. obok odcinka o stałej długości).

### Kolejność prób przy edycji

- Przeciąganie wierzchołka: przypięty wierzchołek; gdy brak rozwiązania – translacja całego wielokąta.
- Przeciąganie punktu kontrolnego: przypięty punkt i wierzchołki jego segmentu; potem tylko punkt; na końcu translacja wielokąta.
- Przesuwanie wielokąta: sama translacja (nie narusza żadnej relacji, więc solver nie jest potrzebny).
- Dodanie/zmiana ograniczenia lub ciągłości: kilka wariantów przypięcia (np. dla długości – początek krawędzi, potem koniec); gdy żaden nie działa – zmiana jest cofana.

Translacja jest zawsze poprawnym wyjściem awaryjnym: zachowuje długości i kierunki krawędzi oraz wektory styczne, a przeciągany element trafia pod kursor.

## Rysowanie odcinków – algorytm Bresenhama

Algorytm punktu środkowego uogólniony na wszystkie oktanty, wyłącznie arytmetyka całkowita i dodawanie. Osią wiodącą jest ta o większym przyroście; przy |dx| ≥ |dy|:

- d₀ = 2·dy − dx, incrE = 2·dy, incrNE = 2·(dy − dx) (dx, dy – wartości bezwzględne);
- w każdym kroku x += sx; jeśli d < 0 – krok E (d += incrE), w przeciwnym razie krok NE (d += incrNE, y += sy).

Dla |dy| > |dx| role osi są zamienione. Linia przerywana: piksele rysowane naprzemiennie w seriach po 6.

## Segment Béziera – algorytm przyrostowy

Przejście do bazy potęgowej P(t) = A₃t³ + A₂t² + A₁t + A₀:

- A₀ = V₀, A₁ = 3(V₁ − V₀), A₂ = 3(V₂ − 2V₁ + V₀), A₃ = V₃ − 3V₂ + 3V₁ − V₀.

Różnice progresywne dla kroku d = 1/n:

- ΔP(0) = A₃d³ + A₂d² + A₁d, Δ²P(0) = 6A₃d³ + 2A₂d², Δ³P = 6A₃d³ (stała).

W pętli: P += ΔP, ΔP += Δ²P, Δ²P += Δ³P – trzy dodawania wektorów na punkt. Liczba kroków n jest proporcjonalna do długości wieloboku Béziera (ok. 4 px na krok, 4–400). Kolejne punkty łączone są wybranym algorytmem rysowania odcinków.

## Łuk okręgu

Łuk styczny w A do wektora t i przechodzący przez B: środek leży na normalnej n do t w punkcie A oraz na symetralnej AB, więc C = A + s·n, gdzie s = |AB|² / (2·n·AB). Kierunek obiegu wynika ze zgodności t z kierunkiem rosnącego kąta. Punkty łuku generowane są przyrostowo: wektor promienia obracany jest o stały kąt (cos i sin liczone raz), ok. 4 px łuku na krok (8–4096 kroków), a odcinki poza oknem są obcinane algorytmem Lianga–Barsky'ego przed rasteryzacją.

## Wyszukiwanie elementu pod kursorem

Priorytet: punkt kontrolny (8 px) > wierzchołek (8 px) > krawędź (6 px, odległość od łamanej) > wnętrze wielokąta (promień w poziomie, reguła parzystości – działa też dla samoprzecięć). Wielokąty rysowane później mają pierwszeństwo.

## Podział segmentu Béziera

Przy dodawaniu wierzchołka na krawędzi Béziera stosowany jest algorytm de Casteljau dla t = 1/2: nowy wierzchołek to P(1/2), a dwa nowe segmenty mają punkty kontrolne z kolejnych interpolacji – kształt się nie zmienia, a w nowym wierzchołku krzywa jest klasy C1.
