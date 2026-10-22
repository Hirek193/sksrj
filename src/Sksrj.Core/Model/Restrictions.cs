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

using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sksrj.Core.Model;

public enum RestrictionKind
{
    /// <summary>Ostrzeżenie stałe (wykaz ostrzeżeń stałych – Dodatek 2 do WRJ, Ir-18). Wliczane do technicznego czasu jazdy A.</summary>
    Permanent,
    /// <summary>Ostrzeżenie doraźne – ograniczenie wynikające z usterek, robót itp. Strata czasu wliczana do rezerwy C.</summary>
    Temporary,
}

public enum RestrictionTrack
{
    Both,
    /// <summary>Tor nr 1 – w tym programie: tor prawy dla jazdy w kierunku rosnącego kilometrażu.</summary>
    Track1,
    /// <summary>Tor nr 2 – tor prawy dla jazdy w kierunku malejącego kilometrażu.</summary>
    Track2,
}

public enum TrainFilter { All, Passenger, Freight }

/// <summary>Ograniczenie prędkości (ostrzeżenie) na odcinku linii.</summary>
public partial class SpeedRestriction : ObservableObject
{
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private RestrictionKind _kind = RestrictionKind.Temporary;
    [ObservableProperty] private string _line = "";
    [ObservableProperty] private RestrictionTrack _track = RestrictionTrack.Both;
    [ObservableProperty] private string _kmFrom = "";
    [ObservableProperty] private string _kmTo = "";
    [ObservableProperty] private string _speed = "40";
    [ObservableProperty] private TrainFilter _appliesTo = TrainFilter.All;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _validFrom = "";
    [ObservableProperty] private string _validTo = "";
    [ObservableProperty] private string _notes = "";
    /// <summary>Utworzone przez generator ograniczeń doraźnych.</summary>
    [ObservableProperty] private bool _generated;

    [JsonIgnore] public string KindShort => Kind == RestrictionKind.Permanent ? "stałe" : "doraźne";
    partial void OnKindChanged(RestrictionKind value) => OnPropertyChanged(nameof(KindShort));
}

/// <summary>Parametry obliczeń trakcyjnych i rezerw (Ir-11 § 21).</summary>
public partial class RunCalcSettings : ObservableObject
{
    [ObservableProperty] private string _preset = "EZT (np. EN57)";
    /// <summary>Przyspieszenie przy ruszaniu [m/s²].</summary>
    [ObservableProperty] private string _startAcceleration = "0.6";
    /// <summary>Moc jednostkowa [kW/t] – ogranicza przyspieszenie przy większych prędkościach.</summary>
    [ObservableProperty] private string _powerPerTon = "4.6";
    /// <summary>Opóźnienie hamowania służbowego [m/s²].</summary>
    [ObservableProperty] private string _braking = "0.6";
    /// <summary>Opór ruchu: stała [N/kN].</summary>
    [ObservableProperty] private string _resistanceA = "2.0";
    /// <summary>Opór ruchu: współczynnik przy V² [N/kN /(km/h)²].</summary>
    [ObservableProperty] private string _resistanceC = "0.00035";
    /// <summary>Prędkość maksymalna składu [km/h] (puste = z kolumny 9 pierwszego posterunku).</summary>
    [ObservableProperty] private string _trainVmax = "";
    /// <summary>Długość pociągu [m] (puste = z kolumny 8 pierwszego posterunku).</summary>
    [ObservableProperty] private string _trainLength = "";
    /// <summary>Rezerwa eksploatacyjna B [min/100 km] (puste = wg rodzaju pociągu, § 21 ust. 6 pkt 2).</summary>
    [ObservableProperty] private string _reservePer100 = "";
    /// <summary>Wliczaj ostrzeżenia doraźne (rezerwa C) do czasów jazdy.</summary>
    [ObservableProperty] private bool _includeTemporary = true;
    /// <summary>Zaokrąglaj godziny wg § 21 ust. 9 (stacje początkowe/końcowe – pełne minuty, węzłowe – 0,5 min).</summary>
    [ObservableProperty] private bool _roundIr11 = true;
    /// <summary>Po obliczeniu przelicz godziny przyjazdów i odjazdów.</summary>
    [ObservableProperty] private bool _updateClockTimes = true;
    /// <summary>Minimalny postój handlowy [min] (puste = wg § 22 ust. 11).</summary>
    [ObservableProperty] private string _defaultDwell = "";
}

public sealed record TractionPreset(string Name, double A0, double PowerPerTon, double Braking, double ResA, double ResC, double Vmax, double Length, string Loco);

public static class TractionPresets
{
    public static readonly IReadOnlyList<TractionPreset> All = new TractionPreset[]
    {
        new("EZT (np. EN57)", 0.60, 4.6, 0.60, 2.0, 0.00035, 110, 65, "EN57"),
        new("Nowoczesny EZT (np. Elf, Impuls)", 1.00, 13.0, 0.80, 1.8, 0.00025, 160, 75, "EN76"),
        new("Autobus szynowy / SZT spalinowy", 0.80, 7.5, 0.80, 2.0, 0.00035, 120, 25, "SA135"),
        new("Pociąg zespołowy dużych prędkości (np. ED250)", 0.50, 13.7, 0.70, 1.6, 0.00018, 200, 187, "ED250"),
        new("Lokomotywa + wagony pasażerskie (np. EU07 + 8 wag.)", 0.35, 4.5, 0.55, 1.8, 0.00028, 120, 210, "EU07"),
        new("Lokomotywa + wagony (np. EP09 + 10 wag.)", 0.40, 6.2, 0.60, 1.7, 0.00025, 160, 270, "EP09"),
        new("Pociąg towarowy ciężki (np. ET22 + 2000 t)", 0.12, 1.4, 0.30, 1.5, 0.00030, 70, 600, "ET22"),
        new("Pociąg towarowy intermodalny (np. E6ACT + 1200 t)", 0.20, 4.0, 0.35, 1.5, 0.00028, 100, 600, "E6ACT"),
        new("Lokomotywa luzem (np. EU07)", 0.90, 25.0, 0.70, 2.5, 0.00050, 125, 16, "EU07"),
        new("Lokomotywa spalinowa luzem (np. SM42)", 0.80, 8.0, 0.60, 2.5, 0.00060, 90, 14, "SM42"),
    };

    public static TractionPreset? Find(string? name) => All.FirstOrDefault(p => p.Name == name);
}
