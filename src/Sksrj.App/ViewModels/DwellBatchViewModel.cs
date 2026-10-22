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
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sksrj.Core.Model;
using Sksrj.Core.Util;

namespace Sksrj.App.ViewModels;

public partial class DwellRow : ObservableObject
{
    public RoutePoint Point { get; }
    public int Number { get; }
    public TimetableTools.StopCategory Category { get; }
    public string Name => Point.ListTitle;
    public string CategoryText => TimetableTools.CategoryText(Category);
    public string StopKinds => string.IsNullOrWhiteSpace(Point.StopKinds) ? "" : Point.StopKinds.Trim();
    public string CurrentDwell => string.IsNullOrWhiteSpace(Point.Dwell) ? "min. § 22" : Point.Dwell.Trim() + " min";
    [ObservableProperty] private bool _selected;

    public DwellRow(RoutePoint p, int number, bool selected)
    {
        Point = p; Number = number; Category = TimetableTools.CategoryOf(p); _selected = selected;
    }

    public void Refresh() => OnPropertyChanged(nameof(CurrentDwell));
}

/// <summary>Zbiorcze ustawianie czasów postojów handlowych wg rodzaju posterunku.</summary>
public partial class DwellBatchViewModel : ObservableObject
{
    private readonly Timetable _t;
    [ObservableProperty] private string _halt;
    [ObservableProperty] private string _station;
    [ObservableProperty] private string _junction;
    [ObservableProperty] private string _error = "";

    public ObservableCollection<DwellRow> Rows { get; } = new();
    public string TrainText => _t.DisplayName;
    public bool HasRows => Rows.Count > 0;
    public string SelectionText => $"Zaznaczono {Rows.Count(r => r.Selected)} z {Rows.Count}";

    public DwellBatchViewModel(Timetable t)
    {
        _t = t;
        _halt = t.DwellHalt; _station = t.DwellStation; _junction = t.DwellJunction;
        for (var i = 1; i < t.Points.Count - 1; i++)
        {
            var p = t.Points[i];
            if (!p.Stop) continue;
            var commercial = (p.StopKinds ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(x => x.Equals("ph", StringComparison.OrdinalIgnoreCase));
            var row = new DwellRow(p, i + 1, commercial);
            row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DwellRow.Selected)) OnPropertyChanged(nameof(SelectionText)); };
            Rows.Add(row);
        }
    }

    [RelayCommand] private void SelectAll() { foreach (var r in Rows) r.Selected = true; }
    [RelayCommand] private void SelectNone() { foreach (var r in Rows) r.Selected = false; }

    [RelayCommand]
    private void SelectCategory(string cat)
    {
        if (!Enum.TryParse<TimetableTools.StopCategory>(cat, out var c)) return;
        foreach (var r in Rows) r.Selected = r.Category == c;
    }

    private static bool Valid(string v) => string.IsNullOrWhiteSpace(v) || (Duration.TryParse(v, out var d) && d.Tenths >= 0);

    /// <summary>Sprawdza wartości i zapamiętuje je w karcie; zwraca false przy błędzie.</summary>
    public bool Validate()
    {
        foreach (var (v, n) in new[] { (Halt, "przystanków osobowych"), (Station, "stacji"), (Junction, "stacji węzłowych") })
            if (!Valid(v)) { Error = $"Niepoprawny czas postoju dla {n}: „{v}” (np. 0.5, 1, 2.5)."; return false; }
        Error = "";
        return true;
    }

    /// <summary>Wpisuje czasy postoju do zaznaczonych posterunków. Zwraca liczbę zmienionych.</summary>
    public int Apply()
    {
        _t.DwellHalt = (Halt ?? "").Trim(); _t.DwellStation = (Station ?? "").Trim(); _t.DwellJunction = (Junction ?? "").Trim();
        var n = 0;
        foreach (var r in Rows.Where(x => x.Selected))
        {
            var v = TimetableTools.DefaultDwell(_t, r.Category);
            if (Duration.TryParse(v, out var d)) v = d.ToString();
            r.Point.Dwell = v;
            n++;
        }
        return n;
    }
}
