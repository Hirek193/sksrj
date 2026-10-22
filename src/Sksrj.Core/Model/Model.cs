// SKSRJ Timetable generator
// Copyright (C) 2026 Oskar Jagodziński
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version, together with the additional terms
// under Section 7 of that License (see the files LICENSE and ADDITIONAL_TERMS).
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
//
// Based on SKSRJ Timetable generator originally created by Oskar Jagodziński.
// Original source: https://github.com/Hirek193/sksrj

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sksrj.Core.Model;

/// <summary>Plik projektu (*.sksrj) – zbiór kart rozkładu jazdy.</summary>
public partial class TimetableProject : ObservableObject
{
    [ObservableProperty] private int _formatVersion = 1;
    [ObservableProperty] private string _projectName = "Nowy projekt";
    [ObservableProperty] private PrintSettings _print = new();

    public ObservableCollection<Timetable> Timetables { get; set; } = new();

    /// <summary>Plan schematyczny scenerii, na którym układane są trasy pociągów.</summary>
    [ObservableProperty] private Scenery _scenery = new();

    /// <summary>Wykaz ostrzeżeń stałych i doraźnych (wspólny dla wszystkich pociągów projektu).</summary>
    public ObservableCollection<SpeedRestriction> Restrictions { get; set; } = new();
}

public enum PageLayoutKind
{
    /// <summary>Każda strona karty na osobnym arkuszu A5 (pionowo).</summary>
    A5,
    /// <summary>Dwie strony A5 obok siebie na arkuszu A4 poziomo (jak we wzorze Ir-11).</summary>
    A4TwoUp,
}

public partial class PrintSettings : ObservableObject
{
    [ObservableProperty] private PageLayoutKind _layout = PageLayoutKind.A5;
    /// <summary>Drobny dopisek „rozkład symulacyjny” w stopce stron (domyślnie wyłączony, by zachować wygląd 1:1).</summary>
    [ObservableProperty] private bool _simulationNote;
    [ObservableProperty] private string _simulationNoteText = "Rozkład symulacyjny – wyłącznie do użytku w symulatorze";
    /// <summary>Dołącz do karty stronę z wykazem ostrzeżeń na trasie pociągu.</summary>
    [ObservableProperty] private bool _includeRestrictionList = true;
    /// <summary>Szare tło godzin przyjazdu i odjazdu na posterunkach z postojem handlowym „ph” (kolumna 6, rubryka 1).</summary>
    [ObservableProperty] private bool _shadeStopTimes;
}

public enum TrackSide
{
    /// <summary>Tor prawy / kierunek zasadniczy (strzałka między kolumną 2 i 3).</summary>
    Right,
    /// <summary>Tor lewy / kierunek przeciwny do zasadniczego (strzałka między kolumną 4 i 5).</summary>
    Left,
}

public enum TrackCount
{
    /// <summary>Szlak dwu- lub więcej torowy (dwie cienkie linie).</summary>
    Double,
    /// <summary>Szlak jednotorowy (linia gruba).</summary>
    Single,
}

public enum BoldMode { Auto, Yes, No }

/// <summary>Karta rozkładu jazdy jednego pociągu.</summary>
public partial class Timetable : ObservableObject
{
    // ---- identyfikacja pociągu --------------------------------------------------------
    [ObservableProperty] private string _trainCategory = "ROJ";
    [ObservableProperty] private string _trainNumber = "";
    [ObservableProperty] private string _systemNumber = "";
    [ObservableProperty] private string _relationFrom = "";
    [ObservableProperty] private string _relationTo = "";
    /// <summary>Jeśli niepuste – zastępuje automatycznie składany nagłówek stron rozkładu.</summary>
    [ObservableProperty] private string _headerOverride = "";

    // ---- strona tytułowa --------------------------------------------------------------
    [ObservableProperty] private string _issuer = DefaultIssuer;
    [ObservableProperty] private string _timetableKind = "ROCZNY ROZKŁAD JAZDY";
    [ObservableProperty] private string _carrier = "";
    [ObservableProperty] private string _applicationNumber = "";
    [ObservableProperty] private string _applicationDate = "";
    [ObservableProperty] private string _orderNumber = "";
    [ObservableProperty] private string _orderDate = "";
    [ObservableProperty] private string _systemName = "SKSRJ";
    [ObservableProperty] private bool _useCurrentGenerationTime = true;
    [ObservableProperty] private string _generationDate = "";
    [ObservableProperty] private string _generationTime = "";
    [ObservableProperty] private string _driverMessage = DefaultDriverMessage;
    [ObservableProperty] private bool _underlineLastMessageLine = true;

    // ---- dół ostatniej strony ---------------------------------------------------------
    [ObservableProperty] private string _runningDays = "";
    [ObservableProperty] private string _remarks = "";
    /// <summary>Prędkości w kolumnach 3–4 ograniczone do prędkości maksymalnej pociągu (Vmax, kol. 9) – Ir-11 § 32 ust. 6 pkt 2 lit. e, f.</summary>
    [ObservableProperty] private bool _capSpeedsToVmax = true;
    /// <summary>Przystanki osobowe („po”), na których pociąg nie ma postoju, nie są drukowane w karcie.</summary>
    [ObservableProperty] private bool _hidePassedHalts = true;
    /// <summary>Czas postoju handlowego [min] na przystankach osobowych (puste = minimum wg Ir-11 § 22 ust. 11).</summary>
    [ObservableProperty] private string _dwellHalt = "";
    /// <summary>Czas postoju handlowego [min] na stacjach.</summary>
    [ObservableProperty] private string _dwellStation = "";
    /// <summary>Czas postoju handlowego [min] na stacjach węzłowych (nazwa wersalikami).</summary>
    [ObservableProperty] private string _dwellJunction = "";

    public ObservableCollection<Footnote> Footnotes { get; set; } = new();
    public ObservableCollection<LineSection> Sections { get; set; } = new();
    public ObservableCollection<RoutePoint> Points { get; set; } = new();

    /// <summary>Trasa ułożona na schemacie scenerii.</summary>
    [ObservableProperty] private SceneryRoute _route = new();
    /// <summary>Parametry obliczeń trakcyjnych.</summary>
    [ObservableProperty] private RunCalcSettings _runCalc = new();
    /// <summary>Odcinki linii były ręcznie zmieniane po wygenerowaniu ze schematu.</summary>
    [ObservableProperty] private bool _manualSections;

    /// <summary>Domyślny wydawca karty (nagłówek strony tytułowej).</summary>
    public const string DefaultIssuer = "FLK Fikcyjne Linie Kolejowe S.F.";

    public const string DefaultDriverMessage =
        "Maszynisto!\n" +
        "Podczas jazdy na sygnał zastępczy lub rozkaz pisemny obserwuj drogę\n" +
        "przebiegu i zachowaj szczególną ostrożność.\n" +
        "Jeśli zauważysz zagrożenie lub urządzenia pokładowe sygnalizują\n" +
        "odstępstwa od normy natychmiast zatrzymaj pociąg i wyjaśnij\n" +
        "przyczynę z dyżurnym ruchu.\n" +
        "Nie bój się konsekwencji opóźnienia pociągu.\n" +
        "Bezpieczeństwo jest najważniejsze.";

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var n = $"{TrainCategory} {TrainNumber}".Trim();
            var rel = $"{RelationFrom} – {RelationTo}".Trim(' ', '–');
            if (string.IsNullOrWhiteSpace(n)) n = "(bez numeru)";
            return string.IsNullOrWhiteSpace(rel) ? n : $"{n}  ·  {rel}";
        }
    }

    partial void OnTrainCategoryChanged(string value) => OnPropertyChanged(nameof(DisplayName));
    partial void OnTrainNumberChanged(string value) => OnPropertyChanged(nameof(DisplayName));
    partial void OnRelationFromChanged(string value) => OnPropertyChanged(nameof(DisplayName));
    partial void OnRelationToChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}

/// <summary>Odnośnik „*1)” wraz z treścią, drukowany pod tabelą na stronie, na której występuje.</summary>
public partial class Footnote : ObservableObject
{
    [ObservableProperty] private string _marker = "*1)";
    [ObservableProperty] private string _text = "";
}

/// <summary>Odcinek linii kolejowej (kolumna 1 i 2), po którym kursuje pociąg.</summary>
public partial class LineSection : ObservableObject
{
    [ObservableProperty] private string _lineNumber = "";
    [ObservableProperty] private string _kmStart = "0.000";
    [ObservableProperty] private string _kmEnd = "0.000";
    [ObservableProperty] private TrackSide _track = TrackSide.Right;
    /// <summary>Opis odcinka w edytorze (np. „Kaletnik → Młynów”) – nie jest drukowany.</summary>
    [ObservableProperty] private string _label = "";

    /// <summary>Punkty zmiany dopuszczalnej prędkości (kolumny 2–4). Pierwszy wpis określa prędkość od początku odcinka.</summary>
    public ObservableCollection<SpeedChange> Changes { get; set; } = new();
}

public partial class SpeedChange : ObservableObject
{
    [ObservableProperty] private string _km = "0.000";
    /// <summary>Prędkość po torze prawym (V<sub>P</sub>).</summary>
    [ObservableProperty] private string _vp = "";
    /// <summary>Prędkość po torze lewym (V<sub>L</sub>).</summary>
    [ObservableProperty] private string _vl = "";
    /// <summary>Odcinek jednotorowy – kolumny 3 i 4 połączone.</summary>
    [ObservableProperty] private bool _singleTrack;
    [ObservableProperty] private BoldMode _boldKm = BoldMode.Auto;
}

/// <summary>Posterunek ruchu / przystanek / punkt na trasie (kolumny 5–9).</summary>
public partial class RoutePoint : ObservableObject
{
    [ObservableProperty] private string _name = "";
    /// <summary>Stacja – nazwa wytłuszczona.</summary>
    [ObservableProperty] private bool _bold;
    /// <summary>Stacja węzłowa – nazwa wersalikami.</summary>
    [ObservableProperty] private bool _uppercase;
    /// <summary>Skrót rodzaju posterunku dopisywany za nazwą (np. stth, pzs, podg, po, gt).</summary>
    [ObservableProperty] private string _typeSuffix = "";
    /// <summary>Rodzaj postoju (ph, pt, pn, pm, zp, zd …) – drukowany po średniku.</summary>
    [ObservableProperty] private string _stopKinds = "";
    [ObservableProperty] private string _km = "";
    /// <summary>Numer linii, na której leży posterunek (opcjonalnie – inaczej wyznaczany automatycznie).</summary>
    [ObservableProperty] private string _line = "";
    /// <summary>Skróty i znaki umowne (R1, H, SS, *1) …). Kolejne wiersze – Enter.</summary>
    [ObservableProperty] private string _abbreviations = "";
    /// <summary>Kursywa nad nazwą – np. kilometraż zmiany linii „0.346/6.754”.</summary>
    [ObservableProperty] private string _noteTop = "";
    /// <summary>Kursywa pod kilometrem posterunku.</summary>
    [ObservableProperty] private string _noteBottom = "";

    [ObservableProperty] private string _arrival = "";
    [ObservableProperty] private string _departure = "";
    /// <summary>Postój rozkładowy (godziny drukowane pogrubioną czcionką).</summary>
    [ObservableProperty] private bool _stop;
    /// <summary>Czas postoju w minutach – używany przy przeliczaniu godzin.</summary>
    [ObservableProperty] private string _dwell = "";
    /// <summary>Czas jazdy od poprzedniego posterunku – normalny (licznik).</summary>
    [ObservableProperty] private string _travelNormal = "";
    /// <summary>Czas jazdy od poprzedniego posterunku – skrócony (mianownik).</summary>
    [ObservableProperty] private string _travelShort = "";

    /// <summary>Zmiana rodzaju/numeru pociągu od tego posterunku (pusty = bez zmiany).</summary>
    [ObservableProperty] private string _trainCategory = "";
    [ObservableProperty] private string _trainNumber = "";

    [ObservableProperty] private TrackCount _tracksToNext = TrackCount.Double;

    // kolumny 7–9; pusty = bez zmian względem poprzedniego posterunku
    [ObservableProperty] private string _loco1 = "";
    [ObservableProperty] private string _loco2 = "";
    [ObservableProperty] private string _loco3 = "";
    [ObservableProperty] private string _locoLoad = "";
    [ObservableProperty] private string _trainLength = "";
    [ObservableProperty] private string _vmax = "";
    [ObservableProperty] private string _brakePercent = "";
    [ObservableProperty] private string _brakeType = "";

    /// <summary>Posterunek schematu, z którego pochodzi punkt (puste = punkt dodany ręcznie).</summary>
    [ObservableProperty] private string _nodeId = "";
    /// <summary>Dane infrastruktury (nazwa, km, skróty, uwagi) zmieniono ręcznie – nie nadpisuj przy ponownym ułożeniu trasy.</summary>
    [ObservableProperty] private bool _manualInfra;
    /// <summary>Postój (postój, rodzaj postoju, czas postoju) zmieniono ręcznie – nie nadpisuj zasadą postojów trasy.</summary>
    [ObservableProperty] private bool _manualStop;
    /// <summary>Posterunek pomijany w karcie (przystanek osobowy bez postoju) – tylko informacja dla edytora.</summary>
    [ObservableProperty]
    [property: JsonIgnore]
    private bool _hiddenInCard;

    [JsonIgnore]
    public string ListTitle => string.IsNullOrWhiteSpace(Name) ? "(bez nazwy)" : (Uppercase ? Name.ToUpperInvariant() : Name) + (string.IsNullOrWhiteSpace(TypeSuffix) ? "" : " " + TypeSuffix);
    [JsonIgnore]
    public string ListSubtitle
    {
        get
        {
            var t = new List<string>();
            if (!string.IsNullOrWhiteSpace(Km)) t.Add("km " + Km);
            if (!string.IsNullOrWhiteSpace(Arrival)) t.Add("p. " + Arrival);
            if (!string.IsNullOrWhiteSpace(Departure)) t.Add((Stop ? "o. " : "") + Departure);
            if (!string.IsNullOrWhiteSpace(StopKinds)) t.Add(StopKinds);
            return string.Join("  ·  ", t);
        }
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(ListTitle));
    partial void OnUppercaseChanged(bool value) => OnPropertyChanged(nameof(ListTitle));
    partial void OnTypeSuffixChanged(string value) => OnPropertyChanged(nameof(ListTitle));
    partial void OnKmChanged(string value) => OnPropertyChanged(nameof(ListSubtitle));
    partial void OnArrivalChanged(string value) => OnPropertyChanged(nameof(ListSubtitle));
    partial void OnDepartureChanged(string value) => OnPropertyChanged(nameof(ListSubtitle));
    partial void OnStopChanged(bool value) => OnPropertyChanged(nameof(ListSubtitle));
    partial void OnStopKindsChanged(string value) => OnPropertyChanged(nameof(ListSubtitle));
}
