# SKSRJ – System konstrukcji symulacyjnych rozkładów jazdy

Aplikacja desktopowa (C#, .NET 8, Avalonia 11, interfejs w stylu **Material Design 3**) do tworzenia
**służbowych rozkładów jazdy (kart rozkładu jazdy pociągu) w formacie PDF**, wyglądem odwzorowanych
na wzorze z *Instrukcji o rozkładzie jazdy pociągów Ir-11* (PKP PLK S.A., 2021 – § 32, Załączniki 4 i 5).
Program pozwala budować karty dla tras rzeczywistych i **fikcyjnych** – na potrzeby symulatorów (np. MaSzyna).

## Co generuje

Karta rozkładu jazdy zgodnie z § 32 ust. 6 Ir-11:

* **strona tytułowa** (Zał. 5) – wydawca, rodzaj rozkładu, „POCIĄGU NR …”, przewoźnik, wniosek,
  zarządzenie, „wygenerowano w systemie … w dniu … r. o godz. …”, stopka „Maszynisto!”;
* **strony szczegółowego rozkładu** – nagłówek (rodzaj, numer, nr systemowy, relacja, numer strony) i 9 kolumn:
  1. Nr linii, 2. Km (zmiany prędkości; pogrubione – zmiana na torze, dla którego opracowano rozkład),
  3. V<sub>P</sub>, 4. V<sub>L</sub> (połączone na odcinkach jednotorowych), 5. Stacja (nazwa – stacje
  pogrubione, węzłowe wersalikami; kilometr; skróty i znaki umowne; kursywą kilometraż zmiany linii),
  6. Godzina (przyjazd/odjazd z dokładnością do 0,1 min w indeksie górnym, „|” przy przejeździe,
  „———” na początku i końcu, ramka ze zmianą rodzaju/numeru pociągu, czasy jazdy normalny/skrócony),
  7. Lok I/II/III, 8. Obc. lok / Dł. poc., 9. Vmax / % / rodzaj hamulca – wartości niezmienione
  powtarzane na szaro;
* **strzałka kierunku** między kolumną 2 i 3 (tor prawy / jednotorowa) lub 4 i 5 (tor lewy),
  **oznaczenie szlaku** między kolumną 5 i 6 (gruba linia – jednotorowy, dwie cienkie – dwutorowy);
* pod tabelą: **odnośniki** `*1)`, `*2)` (na stronie, na której występują), na końcu **Kursuje:**
  i **Uwagi do rozkładu**;
* łamanie stron (ciągłość odcinków linii i prędkości na kolejnej stronie);
* arkusze **A5** pionowo albo **A4 poziomo – 2×A5** (jak we wzorze z Ir-11).

PDF zawiera osadzoną czcionkę Liberation Serif (metrycznie zgodną z Times New Roman, licencja SIL OFL 1.1).

## Uruchomienie

Wymagany [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/Sksrj.App
```

Samodzielny plik wykonywalny dla Windows (bez instalowania .NET):

```bash
dotnet publish src/Sksrj.App -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist/win-x64
```

(Analogicznie `-r linux-x64` lub `-r osx-arm64`.)

Narzędzie wiersza poleceń (np. do wsadowego generowania PDF):

```bash
dotnet run --project tools/Sksrj.Cli -- projekt.sksrj wynik.pdf [indeks|all] [a5|a4]
dotnet run --project tools/Sksrj.Cli -- --sample katalog   # projekt przykładowy + PDF-y
```

## Praca z programem

Rail nawigacyjny po lewej, podgląd karty na żywo po prawej (ten sam silnik co eksport PDF).

| Sekcja | Zawartość |
|---|---|
| **Pociąg** | rodzaj (podpowiedzi skrótów z Zał. 4: ROJ, EIJ, TME…), numer, nr systemowy, relacja, nagłówek; strona tytułowa; generator sygnatury zarządzenia wg § 35 (RRJ, KRJ, ZRJP…); stopka dla maszynisty |
| **Trasa** | kolejne odcinki linii (nr linii, km od–do, tor prawy/lewy) i punkty zmiany prędkości (km, V<sub>P</sub>, V<sub>L</sub>, jednotorowy, pogrubienie km) |
| **Posterunki** | lista posterunków w kolejności jazdy + szczegóły: nazwa, rodzaj (po, pzs, podg…), postój (ph, pt…), km, skróty (R1, H, SS…), kursywy, godziny, czasy jazdy, zmiana numeru pociągu, szlak do następnego, skład; „Przelicz godziny z czasów jazdy” i „Czasy jazdy z godzin” |
| **Uwagi** | „Kursuje:” (symbole terminów (1)…(7), (A)…(E)), odnośniki, uwagi do rozkładu |
| **Wydruk** | A5 / A4 2×A5, opcjonalny dopisek „rozkład symulacyjny”, nazwa projektu, motyw, ściąga rodzajów pociągów |

Skróty: `Ctrl+N`, `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`, `Ctrl+E` (eksport PDF), `F5` (odśwież podgląd).

### Wskazówki dot. danych

* Godziny: `06:28`, `06:28.1` (→ 06:28¹), `06:28:30`; czasy jazdy: `3`, `3.5` (→ 3⁵).
* Kilometr posterunku może zawierać dwie wartości na styku linii, np. `8.317/1.558`. Posterunek jest
  przypisywany do kolejnego odcinka linii, w którego zakresie leży jego kilometr (można też wpisać nr linii).
* Zmiany prędkości leżące na szlaku przed posterunkiem są rysowane w jego wierszu – wiersz wydłuża się,
  jeśli zmian jest dużo (jak w kartach SKRJ).
* Skróty w kilku wierszach – Enter w polu „Skróty”; ostatni wiersz drukowany jest obok kilometra.
* Pola składu puste = bez zmian (wartość powtarzana na szaro).

## Format pliku projektu

`*.sksrj` to czytelny JSON (`TimetableProject` → `Timetables[]` → `Sections[]`/`Points[]`/`Footnotes[]`),
łatwy do generowania z innych narzędzi (np. z danych scenerii).

## Struktura

```
src/Sksrj.Core   model, słowniki Ir-11, silnik układu karty (Layout/TimetableLayout.cs), eksport PDF (PDFsharp)
src/Sksrj.App    aplikacja Avalonia (MVVM, CommunityToolkit.Mvvm, Material.Icons), motyw MD3 w Styles/
tools/Sksrj.Cli  generowanie PDF z wiersza poleceń
```

Projekt przykładowy (wczytywany przy starcie) zawiera pociąg na trasie fikcyjnej oraz odwzorowanie
przykładowej karty z Załącznika 5 do Ir-11 – do porównania wyglądu.

## Licencje zależności

Avalonia (MIT), PDFsharp (MIT), CommunityToolkit.Mvvm (MIT), Material.Icons.Avalonia (MIT),
Liberation Serif (SIL OFL 1.1 – `src/Sksrj.Core/Fonts/OFL.txt`).
