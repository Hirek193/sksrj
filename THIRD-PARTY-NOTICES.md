# Komponenty zewnętrzne / Third-party notices

SKSRJ Timetable generator jest rozpowszechniany na licencji GNU GPL v3 lub nowszej z warunkami
dodatkowymi (pliki `LICENSE` i `ADDITIONAL_TERMS`). Poniższe komponenty pozostają na własnych
licencjach – wszystkie są zgodne z GPLv3.

## Biblioteki (pakiety NuGet)

| Komponent | Licencja | Prawa autorskie |
|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | MIT | © The AvaloniaUI Project |
| Avalonia.Fonts.Inter (czcionka Inter) | MIT (pakiet), SIL OFL 1.1 (czcionka) | © The AvaloniaUI Project; © The Inter Project Authors |
| SkiaSharp, HarfBuzzSharp (przez Avalonię) | MIT | © Microsoft Corporation |
| PDFsharp | MIT | © empira Software GmbH |
| CommunityToolkit.Mvvm | MIT | © .NET Foundation and Contributors |
| Material.Icons, Material.Icons.Avalonia | MIT | © SKProCH and contributors |
| Material Design Icons (dane ikon) | Apache License 2.0 | © Pictogrammers |
| .NET Runtime | MIT | © .NET Foundation and Contributors |

## Czcionki dołączone do programu

`src/Sksrj.Core/Fonts` i `src/Sksrj.App/Assets/Fonts` – **Liberation Serif** i **Liberation Sans**,
licencja **SIL Open Font License 1.1** (tekst: `OFL.txt` w tych katalogach).
Digitized data copyright © 2010 Google Corporation with Reserved Font Arimo, Tinos and Cousine;
Copyright © 2012 Red Hat, Inc. with Reserved Font Name Liberation.
Czcionki nie są objęte licencją GPL – obowiązuje je wyłącznie licencja OFL.
Dokumenty PDF utworzone programem (z osadzonymi czcionkami) nie podlegają licencji OFL ani GPL.

## Instrukcja Ir-11

Wygląd kart rozkładu jazdy oraz słowniki znaków, skrótów i rodzajów pociągów
(`src/Sksrj.Core/Dictionaries/Ir11.cs`) i przykładowa karta (Załącznik 5) są oparte na
*Instrukcji o rozkładzie jazdy pociągów Ir-11* (PKP Polskie Linie Kolejowe S.A., 2021).
Prawa do instrukcji przysługują PKP Polskie Linie Kolejowe S.A. Projekt nie jest powiązany
z PKP PLK S.A. ani przez nią autoryzowany; służy do tworzenia rozkładów na potrzeby symulatorów.
