# SKSRJ Timetable generator

[English](README.md) · **Polski**

**Generator symulacyjnych kart rozkładu jazdy** – aplikacja desktopowa (C#, .NET 8, Avalonia 11,
interfejs w stylu Material Design 3) do tworzenia **służbowych rozkładów jazdy pociągów (kart rozkładu
jazdy) w formacie PDF** dla symulatorów kolejowych, np. [MaSzyna](https://eu07.pl).

Wygląd kart odwzorowuje wzór z *Instrukcji o rozkładzie jazdy pociągów Ir-11* (PKP PLK S.A., 2021 –
§ 32, Załączniki 4 i 5). Trasy pociągów układa się na **planie schematycznym scenerii**, a czasy jazdy
liczone są według **§ 21 Ir-11** z uwzględnieniem **ostrzeżeń stałych i doraźnych**. Program działa
z trasami rzeczywistymi i **fikcyjnymi**.

![Schemat scenerii z ułożoną trasą pociągu](docs/img/schemat.png)

> **Uwaga:** program służy wyłącznie do celów symulacyjnych. Nie jest powiązany z PKP Polskie Linie
> Kolejowe S.A. ani przez nią autoryzowany i nie może być używany do prowadzenia rzeczywistego ruchu
> kolejowego.

---

## Spis treści

- [Możliwości](#możliwości)
- [Zrzuty ekranu](#zrzuty-ekranu)
- [Instalacja i uruchomienie](#instalacja-i-uruchomienie)
- [Praca z programem](#praca-z-programem)
- [Obliczenia czasu jazdy](#obliczenia-czasu-jazdy-ir-11--21)
- [Format pliku projektu](#format-pliku-projektu)
- [Narzędzie wiersza poleceń](#narzędzie-wiersza-poleceń)
- [Struktura repozytorium](#struktura-repozytorium)
- [Licencja i autorstwo](#licencja-i-autorstwo)

## Możliwości

### Karta rozkładu jazdy (PDF)

- **Strona tytułowa** wg Załącznika 5: wydawca, rodzaj rozkładu, „POCIĄGU NR …”, przewoźnik, wniosek,
  zarządzenie (z generatorem sygnatur wg § 35), „wygenerowano w systemie … w dniu … r. o godz. …”,
  stopka „Maszynisto!”.
- **Strony szczegółowego rozkładu** z nagłówkiem i 9 kolumnami:
  nr linii · km zmian prędkości (pogrubione zmiany na torze rozkładu) · V<sub>P</sub> · V<sub>L</sub>
  (połączone na odcinkach jednotorowych) · stacja (pogrubienie stacji, wersaliki stacji węzłowych,
  kilometr, skróty i znaki, kursywą kilometraż zmiany linii) · godzina (0,1 min w indeksie górnym,
  „|” przy przejeździe, ramka zmiany numeru pociągu, czasy jazdy normalny/skrócony) · Lok I/II/III ·
  obciążenie i długość · Vmax / % / rodzaj hamulca (wartości niezmienione na szaro).
- **Strzałka kierunku jazdy** (tor prawy / lewy), **oznaczenie szlaku** (jedno- / dwutorowy),
  **odnośniki** \*1), \*2) na stronie, na której występują, **„Kursuje:”** i **„Uwagi do rozkładu”**.
- Strona **„Wykaz ostrzeżeń na trasie pociągu”** ze stratą czasu na każdym ostrzeżeniu.
- Łamanie stron z zachowaniem ciągłości odcinków linii; arkusze **A5** lub **A4 poziomo – 2×A5**.
- Czcionka Times New Roman, jak w kartach SKRJ – z systemu, gdy jest zainstalowana (podzbiór osadzany w PDF); w przeciwnym razie wbudowana Liberation Serif, metrycznie zgodna, więc układ karty jest identyczny.

![Strona tytułowa i strona 2 karty](docs/img/karta.png)

### Schemat scenerii

- Wiele linii kolejowych: numer, nazwa, kolor, linia przerywana, prędkości domyślne, jedno/dwutorowa.
- Posterunki: stacja (kółko), przystanek / posterunek (kreska), zamknięty (przekreślony), koniec linii;
  ikony obsługi podróżnych, posterunku technicznego ⌘ i lotniska.
- Odcinki prowadzone pod kątem 0/45/90° (5 stylów) z dowolną liczbą załamań, kilometraż na końcach,
  zmiany prędkości V<sub>P</sub>/V<sub>L</sub>, blokada liniowa, elektryfikacja, omijanie bocznic przy
  wyznaczaniu tras.
- **Zmiana numeru linii** – na schemacie i we wszystkich kartach, ostrzeżeniach i ograniczeniach posterunków, które się do niej odwołują.
- **Inspektor**: metadane klikniętego posterunku (nazwa, rodzaj, pogrubienie / wersaliki, skróty,
  domyślny postój, czas postoju, godziny czynności, perony, notatki), odcinka lub linii.
- **Ograniczenia prędkości posterunku** – dla wybranej linii, zakresu km i toru (np. geometria
  głowicy, rozjazdy): wpisywane do kolumn 3–4 karty albo jako pozycja wykazu ostrzeżeń stałych.
- **Tryb ciemny schematu** – przełącznik na pasku narzędzi (eksport PDF pozostaje jasny).
- **Auto-kilometraż** linii według długości rysunku, eksport schematu do PDF (A3) z legendą.
- **Import rozkładów MaSzyny** (pliki tekstowe `*.txt` z katalogu rozkładów scenerii): posterunki, kilometraż, prędkości, skróty i jedno-/dwutorowość trafiają na schemat jako linia, a opcjonalnie powstaje karta pociągu z godzinami, czasami jazdy i składem z pliku.
- **Schemat jako osobny plik** (`*.sksrjs`) – eksport schematu i import do innego projektu (import działa też z projektu `*.sksrj`).

### Trasa pociągu ze schematu

Kliknij na schemacie początek, punkty pośrednie i koniec – droga wyznaczy się sama (najkrótsza,
z preferencją jazdy tą samą linią). „Ułóż trasę w karcie” tworzy odcinki linii i wszystkie posterunki
na drodze, ustala postoje według wybranej zasady (osobowy / przyspieszony / tylko wskazane / bez
postojów) i od razu liczy czasy jazdy. Przystanki osobowe otrzymują rodzaj „po”, a postój handlowy
„ph” pojawia się tylko tam, gdzie pociąg pasażerski faktycznie się zatrzymuje; przystanków, przez które
pociąg przejeżdża bez postoju, nie drukuje się w karcie (zostają na liście posterunków, a czas jazdy obejmuje
cały szlak do następnego drukowanego posterunku). Zmiana zasady postojów
od razu poprawia kartę. Zakładki **Posterunki** i **Trasa** służą potem do dopracowania szczegółów
(np. jazda torem lewym); ręczne zmiany są zapamiętywane i nie są nadpisywane przy ponownym ułożeniu trasy.
Każdy szlak między posterunkami jest osobnym odcinkiem, więc tor prawy / lewy można wybrać dla dowolnego
fragmentu trasy – w karcie kolejne odcinki tej samej linii drukowane są razem, a strzałka przechodzi
między kolumnami w miejscu zmiany toru. Prędkości w kolumnach V<sub>P</sub>/V<sub>L</sub> są ograniczane
do prędkości maksymalnej pociągu (Vmax), zgodnie z Ir-11 § 32 ust. 6 pkt 2 lit. e i f.

### Ostrzeżenia

Wykaz ostrzeżeń **stałych** (wliczanych do czasu technicznego) i **doraźnych** (rezerwa C) – linia,
tor, km od–do, prędkość, rodzaj pociągów, przyczyna, ważność – oraz **generator ostrzeżeń doraźnych**
(losowych, opcjonalnie z ziarnem) do urozmaicania jazd w symulatorze.

## Zrzuty ekranu

| Inspektor posterunku | Posterunki pociągu |
|---|---|
| ![Inspektor](docs/img/inspektor.png) | ![Posterunki](docs/img/posterunki.png) |

| Czasy jazdy i wykres v(s) | Ostrzeżenia |
|---|---|
| ![Czasy jazdy](docs/img/czasy-jazdy.png) | ![Ostrzeżenia](docs/img/ostrzezenia.png) |

| Ekran startowy | Schemat w PDF |
|---|---|
| ![Ekran startowy](docs/img/ekran-startowy.png) | ![Schemat PDF](docs/img/schemat-pdf.png) |

## Instalacja i uruchomienie

### Gotowa wersja dla Windows

Pobierz paczkę z zakładki *Releases* (lub zbuduj ją sam – poniżej), rozpakuj i uruchom `SKSRJ.exe`.
Każdy push na `main` jest automatycznie budowany przez GitHub Actions (`.github/workflows/prerelease.yml`) i publikowany jako **prerelease** `build-1.0.<n>` z paczką dla Windows (wymaga .NET 8), samodzielnym `.exe` dla Windows, wersją dla Linuksa i przykładowymi kartami; zachowywanych jest 10 ostatnich prerelease'ów.
Wymagany jest [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).

### Ze źródeł

Wymagany [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/Hirek193/sksrj.git
cd sksrj
dotnet run --project src/Sksrj.App
```

Publikacja:

```bash
# wymaga zainstalowanego .NET 8 Desktop Runtime
dotnet publish src/Sksrj.App -c Release -r win-x64 --self-contained false -p:DebugType=none -o dist/SKSRJ

# samodzielny plik wykonywalny (bez instalowania .NET)
dotnet publish src/Sksrj.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist/win-x64
```

Analogicznie dla `-r linux-x64` lub `-r osx-arm64`.

## Praca z programem

| Sekcja | Zawartość |
|---|---|
| **Schemat** | plan scenerii, narzędzia rysowania, inspektor, układanie trasy pociągu |
| **Pociąg** | rodzaj i numer pociągu, numer systemowy generowany jak w skryptach MaSzyny (`hash(scenariusz[:5]) % 299 + 1`), relacja, nagłówek (z przyciskiem „Automatycznie”), strona tytułowa, przyciski „Dziś” przy datach, sygnatura zarządzenia, stopka; **bank** nagłówków wydawców i przewoźników do ponownego użycia |
| **Trasa** | odcinki linii (kolumny 1–4): km, prędkości V<sub>P</sub>/V<sub>L</sub>, tor prawy/lewy |
| **Posterunki** | posterunki (kolumny 5–9): nazwa, km, skróty, kursywy, godziny, czasy jazdy, numer pociągu, skład; zbiorcze ustawianie czasów postojów handlowych dla przystanków, stacji i stacji węzłowych (zaznacz / odznacz wszystkie) |
| **Czasy jazdy** | profil trakcyjny, rezerwy, wykres v(s), wyniki A/B/C/D, wpisanie czasów do karty |
| **Ostrzeżenia** | wykaz stałych i doraźnych, generator, ostrzeżenia na trasie pociągu |
| **Uwagi** | „Kursuje:”, odnośniki, uwagi do rozkładu |
| **Wydruk** | A5 / A4 2×A5, wykaz ostrzeżeń, szare tło godzin postojów handlowych (ph), dopisek „rozkład symulacyjny”, projekt, motyw |

**Skróty klawiszowe:** `Ctrl+N` nowy projekt · `Ctrl+O` otwórz · `Ctrl+S` zapisz · `Ctrl+Shift+S`
zapisz jako · `Ctrl+E` eksport PDF · `F5` odśwież podgląd.
**Na schemacie:** `V` zaznacz · `N` posterunek · `C` połącz · `T` trasa · `F` dopasuj widok ·
`Delete` usuń · `Esc` przerwij · kółko myszy – powiększenie · prawy przycisk – przesuwanie ·
dwuklik na odcinku – załamanie · `Shift` – bez przyciągania do siatki.

**Wskazówki dot. danych**

- Godziny: `06:28`, `06:28.1` (→ 06:28¹), `06:28:30`; czasy jazdy: `3`, `3.5` (→ 3⁵).
- Kilometr posterunku może mieć dwie wartości na styku linii, np. `8.317/1.558`.
- Skróty w kilku wierszach – Enter w polu „Skróty”; ostatni wiersz drukowany jest obok kilometra.
- Puste pola składu = bez zmian względem poprzedniego posterunku (na karcie na szaro).
- Tor nr 1 = tor prawy przy jeździe w kierunku rosnącego kilometrażu.

## Obliczenia czasu jazdy (Ir-11 § 21)

| Składnik | Sposób liczenia |
|---|---|
| **A** – techniczny czas jazdy | całkowanie ruchu co 10 m: rozruch (przyspieszenie ograniczone mocą jednostkową i oporami ruchu), hamowanie służbowe, prędkości dopuszczalne, ostrzeżenia stałe, Vmax składu i długość pociągu |
| **B** – rezerwa eksploatacyjna | wg § 21 ust. 6 pkt 2: EIJ 2,5; EC/EN/EI/MH/MM/MP/RP 3; TC/TD 3; pozostałe pasażerskie 5; towarowe i utrzymaniowe 7; niehandlowe 5; luzem 3 min/100 km (lub wartość własna) |
| **C** – rezerwa modernizacyjna | strata czasu na ostrzeżeniach doraźnych |
| **D** – postoje | minimalne wg § 22 ust. 11 (0,5 min; 0,3 min dla zespołów trakcyjnych; 1 min MP/MM z wagonów; 2 min z wagonami sypialnymi) lub podane |

Licznik (czas normalny) = A+B+C, mianownik (czas skrócony) = A+C. Godziny zaokrąglane wg § 21 ust. 9
(stacje początkowe i końcowe – pełne minuty, węzłowe – 0,5 min, pozostałe – 0,1 min).
Dostępnych jest 10 profili trakcyjnych (EN57, Elf, SA, ED250, EU07/EP09 z wagonami, ET22 z 2000 t,
pociąg intermodalny, lokomotywy luzem). Model trakcyjny jest uproszczony – wyniki są szacunkiem na
potrzeby symulacji.

## Format pliku projektu

`*.sksrj` to czytelny JSON: `TimetableProject` → `Scenery` (linie, posterunki, odcinki),
`Restrictions` (ostrzeżenia) i `Timetables[]` (karty: odcinki linii, posterunki, odnośniki, trasa
na schemacie, parametry obliczeń). Format łatwo generować z innych narzędzi, np. z danych scenerii.

Projekt przykładowy (wczytywany przy starcie, także w `samples/`) zawiera schemat scenerii
Kaletnik – Rypinek – Leosin – Sosiny (9 linii, 82 posterunki), dwa pociągi ułożone na schemacie
(osobowy ROJ 63215 i towarowy TME 44210), przykładowe ostrzeżenia, kartę wpisaną ręcznie oraz
odwzorowanie przykładowej karty z Załącznika 5 do Ir-11.

## Narzędzie wiersza poleceń

```bash
dotnet run --project tools/Sksrj.Cli -- projekt.sksrj wynik.pdf [indeks|all] [a5|a4]   # karty
dotnet run --project tools/Sksrj.Cli -- --scheme projekt.sksrj schemat.pdf             # schemat
dotnet run --project tools/Sksrj.Cli -- --sample katalog                               # przykłady
```

## Struktura repozytorium

```
src/Sksrj.Core     model danych, słowniki Ir-11, skład karty (Layout/), schemat, trasy,
                   obliczenia trakcyjne i ostrzeżenia (Engine/), eksport PDF (Pdf/)
src/Sksrj.App      aplikacja Avalonia (MVVM), motyw Material Design 3 (Styles/), widoki (Views/)
tools/Sksrj.Cli    generowanie PDF z wiersza poleceń
samples/           projekt przykładowy i wygenerowane z niego PDF-y
docs/img/          zrzuty ekranu
```

## Licencja i autorstwo

**SKSRJ Timetable generator** – Copyright © 2026 Oskar Jagodziński.

Program jest wolnym oprogramowaniem: możesz go rozpowszechniać i modyfikować na warunkach
[GNU General Public License](LICENSE) w wersji 3 lub (według uznania) dowolnej późniejszej, wraz
z [warunkami dodatkowymi](ADDITIONAL_TERMS) wg § 7 tej licencji. W skrócie warunki dodatkowe wymagają:

1. **zachowania atrybucji** w plikach źródłowych i w informacjach prawnych programu (w interfejsie –
   w miejscu łatwo dostępnym, np. na ekranie startowym lub w oknie „O programie”):

   ```
   Based on SKSRJ Timetable generator originally created by Oskar Jagodziński.
   Original source: https://github.com/Hirek193/sksrj
   ```

2. **wyraźnego oznaczania zmienionych wersji** jako innych niż oryginał,
3. **nieużywania nazwy „SKSRJ Timetable generator” i logo** jako nazwy własnej wersji zmienionej bez
   pisemnej zgody autora.

Program jest rozpowszechniany **bez jakiejkolwiek gwarancji**. Pełne teksty: [`LICENSE`](LICENSE),
[`ADDITIONAL_TERMS`](ADDITIONAL_TERMS). Licencje użytych bibliotek i czcionek (MIT, Apache 2.0,
SIL OFL 1.1) opisuje [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md); są one dostępne również
w programie (przycisk ⓘ „O programie”).

Wygląd kart rozkładu jazdy oraz słowniki znaków i skrótów oparto na instrukcji Ir-11; prawa do
instrukcji przysługują PKP Polskie Linie Kolejowe S.A.
