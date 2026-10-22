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

/// <summary>Rodzaj symbolu posterunku na schemacie.</summary>
public enum NodeKind
{
    /// <summary>Stacja / posterunek z torami dodatkowymi – kółko.</summary>
    Station,
    /// <summary>Przystanek osobowy, posterunek odstępowy, podg itp. – kreska na linii.</summary>
    Stop,
    /// <summary>Posterunek zamknięty (Z) – przekreślone szare kółko.</summary>
    Closed,
    /// <summary>Koniec linii / koniec scenerii / punkt załamania – zakończenie linii.</summary>
    End,
}

public enum LabelSide { Top, Bottom, Left, Right }

/// <summary>Sposób prowadzenia odcinka na schemacie (styl mapy metra).</summary>
public enum EdgeStyle
{
    /// <summary>Najpierw ukos 45°, potem prosta.</summary>
    DiagonalFirst,
    /// <summary>Najpierw prosta, potem ukos 45°.</summary>
    StraightFirst,
    /// <summary>Linia prosta (dowolny kąt).</summary>
    Direct,
    /// <summary>Kąt prosty – najpierw poziomo.</summary>
    HorizontalFirst,
    /// <summary>Kąt prosty – najpierw pionowo.</summary>
    VerticalFirst,
}

/// <summary>Plan schematyczny scenerii: linie, posterunki i łączące je odcinki.</summary>
public partial class Scenery : ObservableObject
{
    [ObservableProperty] private string _name = "Sceneria";
    /// <summary>Kilometry na jednostkę rysunku – używane przy automatycznym kilometrażu.</summary>
    [ObservableProperty] private double _kmPerUnit = 0.03;
    [ObservableProperty] private double _gridSize = 13;

    public ObservableCollection<SceneryLine> Lines { get; set; } = new();
    public ObservableCollection<SceneryNode> Nodes { get; set; } = new();
    public ObservableCollection<SceneryEdge> Edges { get; set; } = new();

    public SceneryNode? Node(string? id) => id == null ? null : Nodes.FirstOrDefault(n => n.Id == id);
    public SceneryLine? Line(string? id) => id == null ? null : Lines.FirstOrDefault(l => l.Id == id);
    public SceneryEdge? Edge(string? id) => id == null ? null : Edges.FirstOrDefault(e => e.Id == id);

    public static string NewId() => Guid.NewGuid().ToString("N")[..10];
}

public partial class SceneryLine : ObservableObject
{
    [ObservableProperty] private string _id = Scenery.NewId();
    [ObservableProperty] private string _number = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _color = "#E30613";
    [ObservableProperty] private bool _dashed;
    [ObservableProperty] private double _width = 5;
    [ObservableProperty] private string _defaultVp = "100";
    [ObservableProperty] private string _defaultVl = "100";
    [ObservableProperty] private TrackCount _defaultTracks = TrackCount.Double;

    [JsonIgnore] public string Display => string.IsNullOrWhiteSpace(Name) ? $"Linia {Number}" : $"{Number} – {Name}";
    partial void OnNumberChanged(string value) => OnPropertyChanged(nameof(Display));
    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(Display));
}

/// <summary>Posterunek na schemacie wraz z metadanymi przenoszonymi do karty rozkładu.</summary>
public partial class SceneryNode : ObservableObject
{
    [ObservableProperty] private string _id = Scenery.NewId();
    [ObservableProperty] private string _name = "";
    /// <summary>Opis na schemacie (puste = nazwa + rodzaj; Enter dzieli na wiersze).</summary>
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private NodeKind _kind = NodeKind.Stop;
    [ObservableProperty] private LabelSide _labelSide = LabelSide.Top;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;

    /// <summary>Obsługa podróżnych (ikona pociągu na schemacie, domyślny postój pociągów osobowych).</summary>
    [ObservableProperty] private bool _passenger;
    /// <summary>Posterunek techniczny (znak ⌘ na schemacie).</summary>
    [ObservableProperty] private bool _technical;
    [ObservableProperty] private bool _airport;

    // dane do karty rozkładu
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _uppercase;
    [ObservableProperty] private string _typeSuffix = "";
    [ObservableProperty] private string _abbreviations = "";
    [ObservableProperty] private string _defaultStopKinds = "ph";
    [ObservableProperty] private string _defaultDwell = "";
    [ObservableProperty] private string _openingHours = "";
    [ObservableProperty] private string _km = "";

    // informacje dodatkowe (nie drukowane)
    [ObservableProperty] private string _platforms = "";
    [ObservableProperty] private string _stationTracks = "";
    [ObservableProperty] private string _notes = "";

    /// <summary>Ograniczenia prędkości w obrębie posterunku (np. geometria głowicy, rozjazdy) – dla danej linii i kilometrażu.</summary>
    public ObservableCollection<NodeSpeedLimit> SpeedLimits { get; set; } = new();

    /// <summary>Rodzaj posterunku do karty – przystanek osobowy bez podanego rodzaju otrzymuje „po”.</summary>
    [JsonIgnore]
    public string EffectiveTypeSuffix => !string.IsNullOrWhiteSpace(TypeSuffix) ? TypeSuffix.Trim()
        : Kind == NodeKind.Stop && Passenger ? "po" : "";

    [JsonIgnore]
    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label)) return Label.Replace("\r", "");
            var n = (Uppercase ? Name.ToUpperInvariant() : Name).Trim();
            if (!string.IsNullOrWhiteSpace(TypeSuffix)) n += " " + TypeSuffix.Trim() + ".";
            return n;
        }
    }

    [JsonIgnore] public string KindText => Kind switch
    {
        NodeKind.Station => "stacja",
        NodeKind.Stop => "przystanek / posterunek",
        NodeKind.Closed => "zamknięty",
        _ => "koniec / załamanie",
    };

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnLabelChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnTypeSuffixChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnUppercaseChanged(bool value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnKindChanged(NodeKind value) { OnPropertyChanged(nameof(KindText)); OnPropertyChanged(nameof(EffectiveTypeSuffix)); }
    partial void OnPassengerChanged(bool value) => OnPropertyChanged(nameof(EffectiveTypeSuffix));
}

/// <summary>Sposób uwzględnienia ograniczenia prędkości posterunku w karcie.</summary>
public enum NodeLimitMode
{
    /// <summary>Zmiana prędkości dopuszczalnej w kolumnach 1–4 karty.</summary>
    Profile,
    /// <summary>Pozycja w wykazie ostrzeżeń stałych.</summary>
    PermanentWarning,
}

/// <summary>Ograniczenie prędkości w obrębie posterunku – na wskazanej linii, w zakresie km i torze.</summary>
public partial class NodeSpeedLimit : ObservableObject
{
    [ObservableProperty] private bool _active = true;
    /// <summary>Numer linii kolejowej.</summary>
    [ObservableProperty] private string _line = "";
    [ObservableProperty] private string _kmFrom = "";
    [ObservableProperty] private string _kmTo = "";
    [ObservableProperty] private string _speed = "40";
    [ObservableProperty] private RestrictionTrack _track = RestrictionTrack.Both;
    [ObservableProperty] private string _reason = "geometria głowicy";
    [ObservableProperty] private NodeLimitMode _mode = NodeLimitMode.Profile;
}

public partial class ViaPoint : ObservableObject
{
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
}

/// <summary>Odcinek linii między dwoma posterunkami schematu.</summary>
public partial class SceneryEdge : ObservableObject
{
    [ObservableProperty] private string _id = Scenery.NewId();
    [ObservableProperty] private string _lineId = "";
    [ObservableProperty] private string _nodeA = "";
    [ObservableProperty] private string _nodeB = "";
    /// <summary>Kilometraż linii przy posterunku A i B.</summary>
    [ObservableProperty] private string _kmA = "";
    [ObservableProperty] private string _kmB = "";
    [ObservableProperty] private TrackCount _tracks = TrackCount.Double;
    [ObservableProperty] private bool _electrified = true;
    /// <summary>Rodzaj blokady / urządzeń srk na szlaku (S, SS, PP, SP, PS, L1, L2…) – dopisywany do skrótów posterunku początkowego.</summary>
    [ObservableProperty] private string _blocking = "";
    [ObservableProperty] private EdgeStyle _style = EdgeStyle.DiagonalFirst;
    /// <summary>Odcinek bez ruchu pasażerskiego / bocznica – nie jest wybierany przez wyszukiwanie trasy, chyba że jako jedyny.</summary>
    [ObservableProperty] private bool _avoidInRouting;

    public ObservableCollection<ViaPoint> Via { get; set; } = new();
    /// <summary>Zmiany prędkości w kierunku A→B (puste = prędkości domyślne linii).</summary>
    public ObservableCollection<SpeedChange> Changes { get; set; } = new();
}

/// <summary>Trasa pociągu ułożona na schemacie.</summary>
public partial class SceneryRoute : ObservableObject
{
    /// <summary>Kliknięte punkty trasy (początek, punkty pośrednie, koniec).</summary>
    public ObservableCollection<string> Waypoints { get; set; } = new();
    [ObservableProperty] private StopPolicy _stopPolicy = StopPolicy.AllPassenger;
    /// <summary>Karta jest powiązana ze schematem (posterunki i odcinki pochodzą z trasy).</summary>
    [ObservableProperty] private bool _linked;
}

public enum StopPolicy
{
    /// <summary>Postój na każdym posterunku z obsługą podróżnych (pociąg osobowy).</summary>
    AllPassenger,
    /// <summary>Postój tylko na stacjach (kółko) z obsługą podróżnych (pociąg przyspieszony/pospieszny).</summary>
    StationsOnly,
    /// <summary>Postój tylko na punktach trasy wskazanych na schemacie.</summary>
    Waypoints,
    /// <summary>Bez postojów pośrednich (pociąg towarowy, przejazd luzem).</summary>
    None,
}
