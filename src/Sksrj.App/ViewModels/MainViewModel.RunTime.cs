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
using Sksrj.Core.Engine;
using Sksrj.Core.Model;

namespace Sksrj.App.ViewModels;

public sealed record SegmentRow(string Route, string Km, string A, string B, string C, string Normal, string Short, string Avg, string Dwell, bool Stop);

public sealed record RestrictionRow(string Line, string Km, string Speed, string Kind, string Reason, string Loss);

public partial class MainViewModel
{
    [ObservableProperty] private RunTimeResult? _runResult;
    [ObservableProperty] private TractionPreset? _selectedPreset;

    public IReadOnlyList<TractionPreset> Presets => TractionPresets.All;
    public ObservableCollection<SegmentRow> SegmentRows { get; } = new();
    public ObservableCollection<RestrictionRow> RouteRestrictionRows { get; } = new();

    private static string F(double v, string fmt = "0.0") => v.ToString(fmt, CultureInfo.InvariantCulture);

    public string RunSummaryKm => RunResult?.Ok == true ? F(RunResult.TotalKm) + " km" : "–";
    public string RunSummaryA => RunResult?.Ok == true ? F(RunResult.TotalA) + " min" : "–";
    public string RunSummaryB => RunResult?.Ok == true ? F(RunResult.TotalB) + " min" : "–";
    public string RunSummaryC => RunResult?.Ok == true ? F(RunResult.TotalC) + " min" : "–";
    public string RunSummaryD => RunResult?.Ok == true ? F(RunResult.TotalD) + " min" : "–";
    public string RunSummaryTotal
    {
        get
        {
            if (RunResult?.Ok != true) return "–";
            var m = RunResult.TotalNormal + RunResult.TotalD;
            return $"{(int)(m / 60)} h {m % 60:0.0} min".Replace(',', '.');
        }
    }
    public string RunSummaryAvg => RunResult?.Ok == true && RunResult.TotalNormal + RunResult.TotalD > 0 ? F(RunResult.TotalKm / ((RunResult.TotalNormal + RunResult.TotalD) / 60), "0") + " km/h" : "–";
    public string ReserveRuleText => RunResult == null ? "" : $"B = {F(RunResult.ReserveRate, "0.0#")} min/100 km – {RunResult.ReserveRule}";
    public string DwellRuleText => RunResult == null ? "" : $"Minimalny postój: {F(RunResult.MinDwell, "0.0#")} min – {RunResult.DwellRule}";
    public string TrainParamsText => RunResult == null ? "" : $"Przyjęto: Vmax składu {F(RunResult.TrainVmax, "0")} km/h, długość pociągu {F(RunResult.TrainLength, "0")} m";

    partial void OnRunResultChanged(RunTimeResult? value)
    {
        SegmentRows.Clear();
        RouteRestrictionRows.Clear();
        if (value != null)
        {
            foreach (var s in value.Segments)
                SegmentRows.Add(new SegmentRow($"{s.From} → {s.To}", F(s.Km, "0.000"), F(s.A, "0.00"), F(s.B, "0.00"), F(s.C, "0.00"), F(s.Normal), F(s.Short), s.AvgSpeed > 0 ? F(s.AvgSpeed, "0") : "", s.Dwell > 0 ? F(s.Dwell) : "", s.StopAtEnd));
            foreach (var r in value.Restrictions)
                RouteRestrictionRows.Add(new RestrictionRow(r.Line, $"{r.R.KmFrom} – {r.R.KmTo}", r.R.Speed, r.R.KindShort, r.R.Reason, "+" + F(r.LossMin, "0.00") + " min"));
        }
        foreach (var n in new[] { nameof(RunSummaryKm), nameof(RunSummaryA), nameof(RunSummaryB), nameof(RunSummaryC), nameof(RunSummaryD), nameof(RunSummaryTotal), nameof(RunSummaryAvg), nameof(ReserveRuleText), nameof(DwellRuleText), nameof(TrainParamsText) })
            OnPropertyChanged(n);
    }

    partial void OnSelectedPresetChanged(TractionPreset? value)
    {
        var t = SelectedTimetable;
        if (value == null || t == null || t.RunCalc.Preset == value.Name && _syncingPreset) return;
        if (_syncingPreset) return;
        var s = t.RunCalc;
        s.Preset = value.Name;
        s.StartAcceleration = F(value.A0, "0.###");
        s.PowerPerTon = F(value.PowerPerTon, "0.###");
        s.Braking = F(value.Braking, "0.###");
        s.ResistanceA = F(value.ResA, "0.###");
        s.ResistanceC = F(value.ResC, "0.######");
    }

    private bool _syncingPreset;
    private void SyncPreset()
    {
        _syncingPreset = true;
        SelectedPreset = TractionPresets.Find(SelectedTimetable?.RunCalc.Preset);
        _syncingPreset = false;
    }

    private void ComputeRunTime()
    {
        var t = SelectedTimetable;
        if (t == null) { RunResult = null; return; }
        try { RunResult = RunTimeCalculator.Calculate(t, RestrictionTools.Effective(Project)); }
        catch (Exception ex) { RunResult = null; Warnings.Add("Błąd obliczeń trakcyjnych: " + ex.Message); }
    }

    [RelayCommand]
    private void CalculateRunTimes()
    {
        ComputeRunTime();
        if (RunResult?.Ok != true) { Toast(RunResult?.Warnings.FirstOrDefault() ?? "Brak danych do obliczeń"); return; }
        Toast($"Obliczono: {RunSummaryKm}, czas jazdy {RunSummaryTotal}");
    }

    [RelayCommand]
    private void ApplyRunTimes()
    {
        var t = SelectedTimetable; if (t == null) return;
        ComputeRunTime();
        if (RunResult?.Ok != true) { Toast(RunResult?.Warnings.FirstOrDefault() ?? "Brak danych do obliczeń"); return; }
        _applyingRoute = true;
        try { RunTimeCalculator.Apply(t, RunResult); }
        finally { _applyingRoute = false; }
        Toast("Wpisano czasy jazdy" + (t.RunCalc.UpdateClockTimes ? " i godziny" : "") + " do karty");
    }

    // ================================================================ ograniczenia
    [ObservableProperty] private int _genCount = 5;
    [ObservableProperty] private string _genLines = "";
    [ObservableProperty] private string _genSpeeds = "20, 30, 40, 50, 60, 70";
    [ObservableProperty] private string _genMinLength = "100";
    [ObservableProperty] private string _genMaxLength = "900";
    [ObservableProperty] private string _genValidFrom = "";
    [ObservableProperty] private string _genValidTo = "";
    [ObservableProperty] private string _genSeed = "";
    [ObservableProperty] private bool _genOnlyCurrentRoute = true;

    public string KnownLinesText
    {
        get
        {
            var r = RestrictionTools.LineRanges(Project);
            return r.Count == 0 ? "brak – dodaj odcinki na schemacie lub w kartach" :
                string.Join(", ", r.OrderBy(k => k.Key).Select(k => $"{k.Key} (km {F(k.Value.Min(x => x.A), "0.0")}–{F(k.Value.Max(x => x.B), "0.0")})"));
        }
    }

    [RelayCommand]
    private void AddRestriction(string kind)
    {
        var k = kind == "Permanent" ? RestrictionKind.Permanent : RestrictionKind.Temporary;
        var line = SelectedTimetable?.Sections.FirstOrDefault()?.LineNumber ?? Project.Scenery.Lines.FirstOrDefault()?.Number ?? "";
        Project.Restrictions.Add(new SpeedRestriction { Kind = k, Line = line, Speed = k == RestrictionKind.Permanent ? "80" : "40", Reason = k == RestrictionKind.Permanent ? "" : "zły stan nawierzchni" });
    }

    [RelayCommand] private void RemoveRestriction(SpeedRestriction r) => Project.Restrictions.Remove(r);

    [RelayCommand]
    private void GenerateRestrictions()
    {
        var o = new RestrictionGeneratorOptions
        {
            Count = Math.Clamp(GenCount, 1, 200), Lines = GenLines, Speeds = GenSpeeds,
            MinLengthM = double.TryParse(GenMinLength, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 100,
            MaxLengthM = double.TryParse(GenMaxLength, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 900,
            Seed = int.TryParse(GenSeed, out var s) ? s : null, ValidFrom = GenValidFrom, ValidTo = GenValidTo,
        };
        if (GenOnlyCurrentRoute && string.IsNullOrWhiteSpace(GenLines) && SelectedTimetable != null)
            o.Lines = string.Join(",", SelectedTimetable.Sections.Select(x => x.LineNumber).Distinct());
        var list = RestrictionTools.Generate(Project, o);
        if (GenOnlyCurrentRoute && SelectedTimetable != null)
        {
            // pozostaw tylko leżące na trasie bieżącego pociągu
            var g = RouteGeometry.Build(SelectedTimetable);
            list = list.Where(r => Sksrj.Core.Util.Num.TryParseKm(r.KmFrom, out var x) && Sksrj.Core.Util.Num.TryParseKm(r.KmTo, out var y) && g.MapRange(r.Line, x, y, RestrictionTrack.Both).Any()).ToList();
            var tries = 0;
            while (list.Count < o.Count && tries++ < 20)
            {
                var more = RestrictionTools.Generate(Project, new RestrictionGeneratorOptions { Count = o.Count, Lines = o.Lines, Speeds = o.Speeds, MinLengthM = o.MinLengthM, MaxLengthM = o.MaxLengthM, ValidFrom = o.ValidFrom, ValidTo = o.ValidTo, Seed = o.Seed.HasValue ? o.Seed + tries : null });
                list.AddRange(more.Where(r => Sksrj.Core.Util.Num.TryParseKm(r.KmFrom, out var x) && Sksrj.Core.Util.Num.TryParseKm(r.KmTo, out var y) && g.MapRange(r.Line, x, y, RestrictionTrack.Both).Any()));
            }
            list = list.Take(o.Count).ToList();
        }
        foreach (var r in list) Project.Restrictions.Add(r);
        Toast(list.Count == 0 ? "Brak linii z kilometrażem – nie wygenerowano ostrzeżeń" : $"Wygenerowano {list.Count} ostrzeżeń doraźnych");
        OnPropertyChanged(nameof(KnownLinesText));
    }

    [RelayCommand]
    private void RemoveGeneratedRestrictions()
    {
        var n = 0;
        foreach (var r in Project.Restrictions.Where(x => x.Generated).ToList()) { Project.Restrictions.Remove(r); n++; }
        Toast($"Usunięto {n} wygenerowanych ostrzeżeń");
    }

    [RelayCommand]
    private void SortRestrictions()
    {
        var sorted = Project.Restrictions.OrderBy(r => r.Line).ThenBy(r => Sksrj.Core.Util.Num.TryParseKm(r.KmFrom, out var k) ? k : 0).ToList();
        Project.Restrictions.Clear(); foreach (var r in sorted) Project.Restrictions.Add(r);
    }
}
