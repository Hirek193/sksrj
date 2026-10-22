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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sksrj.Core.Engine;
using Sksrj.Core.Layout;
using Sksrj.Core.Model;
using Sksrj.Core.Util;

namespace Sksrj.App.ViewModels;

public enum SchemeTool { Select, AddNode, Connect, Route, Delete }

public sealed record WaypointItem(int Number, string Id, string Name);

public sealed record LineColor(string Hex, string Name);

public partial class MainViewModel
{
    public event Action? SchemeInvalidated;
    public event Action? FitSchemeRequested;

    public ITextMeasurer Measurer => _measurer;

    [ObservableProperty] private SchemeTool _tool = SchemeTool.Select;
    [ObservableProperty] private SceneryNode? _selectedNode;
    [ObservableProperty] private SceneryEdge? _selectedEdge;
    [ObservableProperty] private SceneryLine? _currentLine;
    [ObservableProperty] private SceneryRouter.RouteResult? _currentRoute;

    public ObservableCollection<WaypointItem> WaypointItems { get; } = new();

    public IReadOnlyList<LineColor> LineColors => Palette;

    public static IReadOnlyList<LineColor> Palette { get; } = new LineColor[]
    {
        new("#E30613", "czerwony"), new("#00692F", "zielony"), new("#1037FF", "niebieski"), new("#D0661A", "pomarańczowy"),
        new("#8A8A8A", "szary"), new("#A6A6A6", "jasnoszary"), new("#7B1FA2", "fioletowy"), new("#F9A825", "żółty"),
        new("#00838F", "turkusowy"), new("#5D4037", "brązowy"), new("#C2185B", "różowy"), new("#000000", "czarny"),
    };

    public bool IsToolSelect { get => Tool == SchemeTool.Select; set { if (value) Tool = SchemeTool.Select; } }
    public bool IsToolAddNode { get => Tool == SchemeTool.AddNode; set { if (value) Tool = SchemeTool.AddNode; } }
    public bool IsToolConnect { get => Tool == SchemeTool.Connect; set { if (value) Tool = SchemeTool.Connect; } }
    public bool IsToolRoute { get => Tool == SchemeTool.Route; set { if (value) Tool = SchemeTool.Route; } }
    public bool IsToolDelete { get => Tool == SchemeTool.Delete; set { if (value) Tool = SchemeTool.Delete; } }

    public string ToolHint => Tool switch
    {
        SchemeTool.Select => "Zaznacz posterunek lub odcinek (inspektor po prawej). Przeciągnij posterunek, by go przesunąć (Shift – bez przyciągania). Dwuklik na odcinku dodaje załamanie, dwuklik na załamaniu je usuwa. Kółko myszy – powiększenie, prawy przycisk – przesuwanie widoku.",
        SchemeTool.AddNode => "Kliknij w pustym miejscu, aby dodać posterunek.",
        SchemeTool.Connect => $"Klikaj kolejne posterunki (lub puste miejsca – powstaną nowe), aby rysować linię {CurrentLine?.Number}. Prawy przycisk / Esc kończy rysowanie.",
        SchemeTool.Route => "Kliknij początek, punkty pośrednie i koniec trasy pociągu – droga wyznaczy się sama (Shift/Ctrl+klik usuwa punkt). Następnie „Ułóż trasę w karcie”.",
        _ => "Kliknij posterunek lub odcinek, aby go usunąć.",
    };

    public bool HasSelectedNode => SelectedNode != null;
    public bool HasSelectedEdge => SelectedEdge != null;
    public bool NothingSelected => SelectedNode == null && SelectedEdge == null;
    public bool IsRouteLinked => SelectedTimetable?.Route.Linked == true;
    public bool CanEditStructure => !IsRouteLinked;
    public string RouteSummary
    {
        get
        {
            var r = CurrentRoute;
            if (SelectedTimetable == null || SelectedTimetable.Route.Waypoints.Count == 0) return "Brak trasy – wybierz narzędzie „Trasa” i kliknij posterunki.";
            if (r == null || !r.Ok) return r?.Warnings.FirstOrDefault() ?? "Wskaż co najmniej dwa posterunki.";
            return $"{r.Nodes.Count} posterunków · {r.Km:0.0} km · {r.Hops.Select(h => Project.Scenery.Line(h.Edge.LineId)?.Number).Distinct().Count()} linie";
        }
    }

    public SceneryLine? SelectedEdgeLine
    {
        get => SelectedEdge == null ? null : Project.Scenery.Line(SelectedEdge.LineId);
        set { if (SelectedEdge != null && value != null) { SelectedEdge.LineId = value.Id; OnPropertyChanged(); } }
    }

    partial void OnToolChanged(SchemeTool value)
    {
        OnPropertyChanged(nameof(IsToolSelect)); OnPropertyChanged(nameof(IsToolAddNode)); OnPropertyChanged(nameof(IsToolConnect));
        OnPropertyChanged(nameof(IsToolRoute)); OnPropertyChanged(nameof(IsToolDelete)); OnPropertyChanged(nameof(ToolHint));
        SchemeInvalidated?.Invoke();
    }

    partial void OnCurrentLineChanged(SceneryLine? value) => OnPropertyChanged(nameof(ToolHint));

    partial void OnSelectedNodeChanged(SceneryNode? value)
    {
        OnPropertyChanged(nameof(HasSelectedNode)); OnPropertyChanged(nameof(NothingSelected)); OnPropertyChanged(nameof(SelectedNodeLines));
        SchemeInvalidated?.Invoke();
    }

    partial void OnSelectedEdgeChanged(SceneryEdge? value)
    {
        OnPropertyChanged(nameof(HasSelectedEdge)); OnPropertyChanged(nameof(NothingSelected)); OnPropertyChanged(nameof(SelectedEdgeLine));
        SchemeInvalidated?.Invoke();
    }

    partial void OnCurrentRouteChanged(SceneryRouter.RouteResult? value) => OnPropertyChanged(nameof(RouteSummary));

    private void OnSchemeObjectChanged() => SchemeInvalidated?.Invoke();

    public void RecomputeRoute()
    {
        WaypointItems.Clear();
        var t = SelectedTimetable;
        if (t == null) { CurrentRoute = null; return; }
        var i = 1;
        foreach (var id in t.Route.Waypoints)
        {
            var n = Project.Scenery.Node(id);
            WaypointItems.Add(new WaypointItem(i++, id, n == null ? "(usunięty)" : n.DisplayLabel.Replace("\n", " ")));
        }
        CurrentRoute = t.Route.Waypoints.Count >= 2 ? SceneryRouter.FindRoute(Project.Scenery, t.Route.Waypoints.ToList()) : null;
        OnPropertyChanged(nameof(RouteSummary));
        OnPropertyChanged(nameof(IsRouteLinked)); OnPropertyChanged(nameof(CanEditStructure));
        NotifyStructureCommands();
        SchemeInvalidated?.Invoke();
    }

    // ---------------------------------------------------------------- operacje z płótna
    public void SelectNode(SceneryNode n) { SelectedEdge = null; SelectedNode = n; }
    public void SelectEdge(SceneryEdge e) { SelectedNode = null; SelectedEdge = e; }
    public void ClearSelection() { SelectedNode = null; SelectedEdge = null; }

    public SceneryNode AddNodeAt(double x, double y)
    {
        var n = new SceneryNode { Name = $"Posterunek {Project.Scenery.Nodes.Count + 1}", X = x, Y = y, Kind = NodeKind.Station, Passenger = true, Bold = true };
        Project.Scenery.Nodes.Add(n);
        SelectNode(n);
        return n;
    }

    public void Connect(SceneryNode a, SceneryNode b)
    {
        var sc = Project.Scenery;
        if (sc.Edges.Any(e => (e.NodeA == a.Id && e.NodeB == b.Id) || (e.NodeA == b.Id && e.NodeB == a.Id))) return;
        var line = CurrentLine ?? sc.Lines.FirstOrDefault();
        if (line == null) { AddLine(); line = CurrentLine!; }
        var e = new SceneryEdge { LineId = line.Id, NodeA = a.Id, NodeB = b.Id, Tracks = line.DefaultTracks };
        // kilometraż: kontynuacja z sąsiedniego odcinka tej samej linii
        var prev = sc.Edges.FirstOrDefault(x => x.LineId == line.Id && (x.NodeA == a.Id || x.NodeB == a.Id));
        double startKm = 0;
        if (prev != null && Num.TryParseKm(prev.NodeA == a.Id ? prev.KmA : prev.KmB, out var k)) startKm = k;
        var len = SchematicGeometry.PolyLength(SchematicGeometry.EdgePath(sc, e)) * sc.KmPerUnit;
        if (len <= 0) len = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * sc.KmPerUnit;
        e.KmA = Num.KmDot(startKm); e.KmB = Num.KmDot(startKm + len);
        sc.Edges.Add(e);
    }

    public void AddVia(SceneryEdge e, double x, double y)
    {
        var sc = Project.Scenery;
        var a = sc.Node(e.NodeA); if (a == null) return;
        // wstaw w kolejności wzdłuż odcinka
        var pts = new List<Pt> { new(a.X, a.Y) };
        pts.AddRange(e.Via.Select(v => new Pt(v.X, v.Y)));
        var b = sc.Node(e.NodeB); if (b != null) pts.Add(new Pt(b.X, b.Y));
        var best = 0; var bd = double.MaxValue;
        for (var i = 1; i < pts.Count; i++)
        {
            var d = SchematicGeometry.DistanceToPolyline(new Pt(x, y), new[] { pts[i - 1], pts[i] });
            if (d < bd) { bd = d; best = i - 1; }
        }
        e.Via.Insert(Math.Min(best, e.Via.Count), new ViaPoint { X = x, Y = y });
        SelectEdge(e);
    }

    public void AddWaypoint(SceneryNode n)
    {
        var t = SelectedTimetable; if (t == null) return;
        if (t.Route.Waypoints.Count > 0 && t.Route.Waypoints[^1] == n.Id) return;
        t.Route.Waypoints.Add(n.Id);
        RecomputeRoute();
    }

    public void RemoveWaypointNode(SceneryNode n)
    {
        var t = SelectedTimetable; if (t == null) return;
        var i = t.Route.Waypoints.ToList().LastIndexOf(n.Id);
        if (i >= 0) t.Route.Waypoints.RemoveAt(i);
        RecomputeRoute();
    }

    public void DeleteNode(SceneryNode n)
    {
        var sc = Project.Scenery;
        foreach (var e in sc.Edges.Where(e => e.NodeA == n.Id || e.NodeB == n.Id).ToList()) sc.Edges.Remove(e);
        sc.Nodes.Remove(n);
        foreach (var t in Project.Timetables) while (t.Route.Waypoints.Remove(n.Id)) { }
        if (SelectedNode == n) SelectedNode = null;
        RecomputeRoute();
    }

    public void DeleteEdge(SceneryEdge e)
    {
        Project.Scenery.Edges.Remove(e);
        if (SelectedEdge == e) SelectedEdge = null;
        RecomputeRoute();
    }

    // ---------------------------------------------------------------- polecenia
    [RelayCommand] private void SetTool(string tool) { if (Enum.TryParse<SchemeTool>(tool, out var t)) Tool = t; }
    [RelayCommand] private void FitScheme() => FitSchemeRequested?.Invoke();

    [RelayCommand]
    private void DeleteSelection()
    {
        if (SelectedNode != null) DeleteNode(SelectedNode);
        else if (SelectedEdge != null) DeleteEdge(SelectedEdge);
    }

    [RelayCommand]
    private void AddLine()
    {
        var sc = Project.Scenery;
        var nums = sc.Lines.Select(l => int.TryParse(l.Number, out var v) ? v : 0).DefaultIfEmpty(0).Max();
        var col = LineColors[sc.Lines.Count % LineColors.Count].Hex;
        var l = new SceneryLine { Number = (Math.Max(nums, 0) + 1).ToString(), Color = col };
        sc.Lines.Add(l);
        CurrentLine = l;
    }

    [RelayCommand]
    private async Task DeleteLine(SceneryLine l)
    {
        var sc = Project.Scenery;
        var n = sc.Edges.Count(e => e.LineId == l.Id);
        if (n > 0 && !await _dialogs.ConfirmAsync("Usuń linię", $"Linia {l.Number} ma {n} odcinków na schemacie. Usunąć linię razem z odcinkami?", "Usuń")) return;
        foreach (var e in sc.Edges.Where(e => e.LineId == l.Id).ToList()) sc.Edges.Remove(e);
        sc.Lines.Remove(l);
        if (CurrentLine == l) CurrentLine = sc.Lines.FirstOrDefault();
        RecomputeRoute();
    }

    [RelayCommand]
    private void SetLineColor(string hex)
    {
        var l = SelectedEdgeLine ?? CurrentLine;
        if (l != null) l.Color = hex;
    }

    [RelayCommand]
    private void AutoKm(SceneryLine? l)
    {
        l ??= SelectedEdgeLine ?? CurrentLine;
        if (l == null) return;
        var n = SceneryRouter.AutoKilometrage(Project.Scenery, l, SelectedNode?.Id);
        Toast($"Linia {l.Number}: nadano kilometraż {n} odcinkom" + (SelectedNode != null ? $" (km 0 – {SelectedNode.Name})" : ""));
    }

    [RelayCommand]
    private void AddEdgeVia()
    {
        var e = SelectedEdge; var sc = Project.Scenery;
        if (e == null) return;
        var path = SchematicGeometry.EdgePath(sc, e);
        if (path.Count < 2) return;
        var a = path[0]; var b = path[^1];
        AddVia(e, Math.Round((a.X + b.X) / 2 / sc.GridSize) * sc.GridSize, Math.Round((a.Y + b.Y) / 2 / sc.GridSize) * sc.GridSize);
    }

    [RelayCommand] private void ClearEdgeVia() => SelectedEdge?.Via.Clear();

    [RelayCommand]
    private void ReverseEdge()
    {
        var e = SelectedEdge; if (e == null) return;
        (e.NodeA, e.NodeB) = (e.NodeB, e.NodeA);
        (e.KmA, e.KmB) = (e.KmB, e.KmA);
        var via = e.Via.Reverse().ToList(); e.Via.Clear(); foreach (var v in via) e.Via.Add(v);
        var ch = e.Changes.ToList();
        if (ch.Count > 0)
        {
            // odwrócenie zmian prędkości: segmenty w odwrotnej kolejności, zamiana VP/VL
            Num.TryParseKm(e.KmA, out var kA);
            var rev = new List<SpeedChange>();
            for (var i = ch.Count - 1; i >= 0; i--)
            {
                var start = i == ch.Count - 1 ? Num.KmDot(kA) : ch[i + 1].Km;
                rev.Add(new SpeedChange { Km = start, Vp = ch[i].SingleTrack ? ch[i].Vp : ch[i].Vl, Vl = ch[i].SingleTrack ? "" : ch[i].Vp, SingleTrack = ch[i].SingleTrack });
            }
            e.Changes.Clear(); foreach (var c in rev) e.Changes.Add(c);
        }
    }

    [RelayCommand]
    private void AddEdgeChange()
    {
        var e = SelectedEdge; if (e == null) return;
        var l = Project.Scenery.Line(e.LineId);
        var last = e.Changes.LastOrDefault();
        e.Changes.Add(new SpeedChange { Km = last == null ? e.KmA : last.Km, Vp = last?.Vp ?? l?.DefaultVp ?? "60", Vl = last?.Vl ?? l?.DefaultVl ?? "60", SingleTrack = e.Tracks == TrackCount.Single });
    }

    [RelayCommand] private void RemoveEdgeChange(SpeedChange c) => SelectedEdge?.Changes.Remove(c);

    [RelayCommand]
    private void SelectNodeById(string id) { var n = Project.Scenery.Node(id); if (n != null) SelectNode(n); }

    [RelayCommand]
    private void RemoveWaypointAt(WaypointItem w)
    {
        var t = SelectedTimetable; if (t == null) return;
        if (w.Number - 1 < t.Route.Waypoints.Count) t.Route.Waypoints.RemoveAt(w.Number - 1);
        RecomputeRoute();
    }

    [RelayCommand]
    private void ClearRoute()
    {
        SelectedTimetable?.Route.Waypoints.Clear();
        RecomputeRoute();
    }

    [RelayCommand]
    private void ReverseRoute()
    {
        var t = SelectedTimetable; if (t == null) return;
        var w = t.Route.Waypoints.Reverse().ToList();
        t.Route.Waypoints.Clear(); foreach (var x in w) t.Route.Waypoints.Add(x);
        RecomputeRoute();
    }

    private bool _applyingRoute;

    [RelayCommand]
    private async Task ApplyRoute()
    {
        var t = SelectedTimetable; if (t == null) return;
        RecomputeRoute();
        var r = CurrentRoute;
        if (r == null || !r.Ok) { Toast(RouteSummary); return; }
        if (t.ManualSections && !await _dialogs.ConfirmAsync("Ręczne zmiany w zakładce Trasa",
                "Odcinki linii tej karty były zmieniane ręcznie. Ponowne ułożenie trasy zastąpi je danymi ze schematu (ręczne zmiany posterunków oznaczone jako „ręczne” zostaną zachowane).", "Ułóż trasę"))
            return;
        if (!t.Route.Linked && t.Points.Count > 2 && t.Points.All(p => string.IsNullOrEmpty(p.NodeId)) &&
            !await _dialogs.ConfirmAsync("Zastąpić posterunki?", "Karta ma posterunki wpisane ręcznie. Ułożenie trasy ze schematu zastąpi listę posterunków i odcinków linii.", "Zastąp"))
            return;
        _applyingRoute = true;
        try
        {
            var dep = t.Points.FirstOrDefault()?.Departure;
            var warn = SceneryRouter.ApplyToTimetable(Project.Scenery, t, r);
            if (t.Points.Count > 0 && string.IsNullOrWhiteSpace(t.Points[0].Departure)) t.Points[0].Departure = string.IsNullOrWhiteSpace(dep) ? "08:00" : dep;
            var res = RunTimeCalculator.Calculate(t, RestrictionTools.Effective(Project));
            RunTimeCalculator.Apply(t, res);
            Toast($"Ułożono trasę: {t.Points.Count} posterunków, {t.Sections.Count} odcinków linii, czasy jazdy obliczone" + (warn.Count > 0 ? $" ({warn.Count} uwag)" : ""));
        }
        finally { _applyingRoute = false; }
        SelectedPoint = t.Points.FirstOrDefault();
        RecomputeRoute();
        Relayout();
    }

    [RelayCommand]
    private async Task UnlinkRoute()
    {
        var t = SelectedTimetable; if (t == null) return;
        if (!await _dialogs.ConfirmAsync("Odłącz od schematu", "Karta przestanie być powiązana z trasą na schemacie – posterunki będzie można dowolnie dodawać, usuwać i przestawiać.", "Odłącz")) return;
        t.Route.Linked = false;
        RecomputeRoute();
    }

    [RelayCommand]
    private void ResetManualInfra()
    {
        if (SelectedPoint != null) SelectedPoint.ManualInfra = false;
    }

    [RelayCommand]
    private async Task BatchDwell()
    {
        var t = SelectedTimetable; if (t == null) return;
        var vm = new DwellBatchViewModel(t);
        if (!await _dialogs.ShowDwellBatchAsync(vm)) return;
        int n;
        _applyingRoute = true; // zbiorcze ustawienie nie jest „ręczną zmianą postoju” – zasada postojów trasy dalej działa
        try
        {
            n = vm.Apply();
            var res = RunTimeCalculator.Calculate(t, RestrictionTools.Effective(Project));
            if (res.Ok) RunTimeCalculator.Apply(t, res);
        }
        finally { _applyingRoute = false; }
        Relayout();
        Toast(n == 0 ? "Nie zaznaczono żadnego postoju" : $"Ustawiono czas postoju na {n} posterunkach i przeliczono czasy jazdy");
    }

    [RelayCommand]
    private void SetAllTracks(string side)
    {
        var t = SelectedTimetable; if (t == null) return;
        var v = side == "Left" ? TrackSide.Left : TrackSide.Right;
        foreach (var s in t.Sections) s.Track = v;
    }

    [RelayCommand]
    private void ResetManualStop()
    {
        var t = SelectedTimetable; var p = SelectedPoint;
        if (t == null || p == null) return;
        p.ManualStop = false;
        if (t.Route.Linked) QueueLinkedUpdate(stops: true);
    }

    // ---------------------------------------------------------------- ograniczenia prędkości posterunku
    [RelayCommand]
    private void AddNodeLimit()
    {
        var n = SelectedNode; if (n == null) return;
        var sc = Project.Scenery;
        var e = sc.Edges.FirstOrDefault(x => x.NodeA == n.Id || x.NodeB == n.Id);
        var line = sc.Line(e?.LineId)?.Number ?? "";
        double km = double.NaN;
        if (e != null && Num.TryParseKm(e.NodeA == n.Id ? e.KmA : e.KmB, out var k)) km = k;
        else if (Num.TryParseKm(n.Km, out var k2)) km = k2;
        var l = new NodeSpeedLimit { Line = line };
        if (!double.IsNaN(km)) { l.KmFrom = Num.KmDot(km - 0.3); l.KmTo = Num.KmDot(km + 0.3); }
        n.SpeedLimits.Add(l);
    }

    [RelayCommand]
    private void RemoveNodeLimit(NodeSpeedLimit l) => SelectedNode?.SpeedLimits.Remove(l);

    /// <summary>Numery linii przechodzących przez zaznaczony posterunek (podpowiedź w inspektorze).</summary>
    public string SelectedNodeLines
    {
        get
        {
            var n = SelectedNode; if (n == null) return "";
            var sc = Project.Scenery;
            var parts = sc.Edges.Where(e => e.NodeA == n.Id || e.NodeB == n.Id)
                .Select(e => (no: sc.Line(e.LineId)?.Number ?? "?", km: e.NodeA == n.Id ? e.KmA : e.KmB))
                .Distinct().Select(x => string.IsNullOrWhiteSpace(x.km) ? $"linia {x.no}" : $"linia {x.no} km {x.km}");
            var s = string.Join(", ", parts);
            return s.Length == 0 ? "Posterunek nie jest połączony z żadną linią." : "Przez posterunek: " + s + ".";
        }
    }

    [RelayCommand]
    private void GoToScheme() => SelectedPage = PageScheme;

    [RelayCommand]
    private async Task ExportSchemePdf()
    {
        var path = await _dialogs.SavePdfAsync(SafeName("Schemat_" + Project.Scenery.Name) + ".pdf");
        if (path == null) return;
        try
        {
            await using var fs = File.Create(path);
            Sksrj.Core.Pdf.PdfExporter.ExportScheme(Project.Scenery, fs);
            Toast($"Zapisano {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Toast("Błąd eksportu: " + ex.Message); }
    }

    [RelayCommand]
    private async Task ExportScenery()
    {
        var path = await _dialogs.SaveSceneryAsync(SafeName(Project.Scenery.Name) + ProjectSerializer.SceneryExtension);
        if (path == null) return;
        try { ProjectSerializer.SaveScenery(Project.Scenery, path); Toast($"Zapisano schemat {Path.GetFileName(path)}"); }
        catch (Exception ex) { Toast("Błąd zapisu schematu: " + ex.Message); }
    }

    [RelayCommand]
    private async Task ImportScenery()
    {
        var path = await _dialogs.OpenSceneryAsync();
        if (path == null) return;
        Scenery sc;
        try { sc = ProjectSerializer.LoadScenery(path); }
        catch (Exception ex) { Toast("Nie można wczytać schematu: " + ex.Message); return; }
        if (Project.Scenery.Nodes.Count > 0 && !await _dialogs.ConfirmAsync("Import schematu",
                $"Zastąpić bieżący schemat schematem „{sc.Name}” ({sc.Nodes.Count} posterunków, {sc.Lines.Count} linii)? Karty pociągów zostaną zachowane; trasy, których posterunków nie ma w nowym schemacie, zostaną odłączone.", "Importuj"))
            return;
        Project.Scenery = sc;
        var unlinked = 0;
        foreach (var t in Project.Timetables)
        {
            if (t.Route.Waypoints.Count > 0 && t.Route.Waypoints.Any(id => sc.Node(id) == null))
            {
                t.Route.Waypoints.Clear();
                if (t.Route.Linked) { t.Route.Linked = false; unlinked++; }
            }
        }
        CurrentLine = sc.Lines.FirstOrDefault();
        ClearSelection();
        RecomputeRoute();
        FitSchemeRequested?.Invoke();
        Toast($"Zaimportowano schemat „{sc.Name}”" + (unlinked > 0 ? $" – odłączono {unlinked} tras" : ""));
    }

    [RelayCommand]
    private async Task NewScenery()
    {
        if (!await _dialogs.ConfirmAsync("Nowy schemat", "Wyczyścić schemat scenerii (linie, posterunki i odcinki)? Karty pociągów pozostaną bez zmian.", "Wyczyść")) return;
        Project.Scenery.Edges.Clear(); Project.Scenery.Nodes.Clear(); Project.Scenery.Lines.Clear();
        foreach (var t in Project.Timetables) { t.Route.Waypoints.Clear(); t.Route.Linked = false; }
        ClearSelection();
        AddLine();
        RecomputeRoute();
    }

    [RelayCommand]
    private async Task LoadSampleScenery()
    {
        if (Project.Scenery.Nodes.Count > 0 && !await _dialogs.ConfirmAsync("Schemat przykładowy", "Zastąpić bieżący schemat schematem przykładowym?", "Zastąp")) return;
        Project.Scenery = SampleScenery.Build();
        foreach (var t in Project.Timetables) { t.Route.Waypoints.Clear(); t.Route.Linked = false; }
        CurrentLine = Project.Scenery.Lines.FirstOrDefault();
        ClearSelection();
        RecomputeRoute();
        FitSchemeRequested?.Invoke();
    }

    // ---------------------------------------------------------------- wykrywanie ręcznych zmian
    private static readonly HashSet<string> InfraProps = new()
    {
        nameof(RoutePoint.Name), nameof(RoutePoint.Bold), nameof(RoutePoint.Uppercase), nameof(RoutePoint.TypeSuffix),
        nameof(RoutePoint.Km), nameof(RoutePoint.Line), nameof(RoutePoint.Abbreviations), nameof(RoutePoint.NoteTop), nameof(RoutePoint.NoteBottom),
    };

    private void OnTrackedItemChanged(object sender, string? prop)
    {
        switch (sender)
        {
            case SceneryNode or SceneryEdge or SceneryLine or ViaPoint or Scenery:
                OnSchemeObjectChanged();
                if (sender is SceneryNode && (prop == nameof(SceneryNode.X) || prop == nameof(SceneryNode.Y))) return;
                if (CurrentRoute != null && sender is SceneryEdge) RecomputeRouteQuiet();
                return;
            case SpeedChange sc when Project.Scenery.Edges.Any(e => e.Changes.Contains(sc)):
                OnSchemeObjectChanged();
                return;
        }
        if (_applyingRoute) return;
        var t = SelectedTimetable;
        if (sender is RoutePoint sp && prop == nameof(RoutePoint.Stop) && !sp.Stop)
        {
            // bez postoju nie ma rodzaju postoju (ph, pt…) ani czasu postoju
            if (sp.StopKinds.Length > 0) sp.StopKinds = "";
            if (sp.Dwell.Length > 0) sp.Dwell = "";
        }
        else if (sender is RoutePoint sp2 && prop == nameof(RoutePoint.Stop) && sp2.Stop && string.IsNullOrWhiteSpace(sp2.StopKinds) && t != null &&
                 Project.Scenery.Node(sp2.NodeId) is { Passenger: true } sn && RunTimeCalculator.IsPassenger(t.TrainCategory))
            sp2.StopKinds = sn.DefaultStopKinds;
        if (t == null || !t.Route.Linked) return;
        if (sender is SceneryRoute sr && ReferenceEquals(sr, t.Route) && prop == nameof(SceneryRoute.StopPolicy))
        {
            QueueLinkedUpdate(stops: true);
            return;
        }
        if (sender is RoutePoint rp && !string.IsNullOrEmpty(rp.NodeId) && prop != null)
        {
            if (InfraProps.Contains(prop) && !rp.ManualInfra) rp.ManualInfra = true;
            if (StopProps.Contains(prop) && !rp.ManualStop) rp.ManualStop = true;
        }
        // postój na przystanku lub pomijanie przystanków zmienia szlaki w karcie – przelicz czasy jazdy
        if ((sender is RoutePoint && prop == nameof(RoutePoint.Stop)) || (ReferenceEquals(sender, t) && prop == nameof(Timetable.HidePassedHalts)))
            QueueLinkedUpdate(stops: false);
        if (sender is LineSection ts && t.Sections.Contains(ts) && prop == nameof(LineSection.Track))
        {
            // wybór toru nie jest „ręczną zmianą odcinków” – zachowuje się przy ponownym ułożeniu trasy
            QueueLinkedUpdate(stops: false);
            return;
        }
        if ((sender is LineSection ls && t.Sections.Contains(ls)) || ReferenceEquals(sender, t.Sections) ||
            (sender is SpeedChange ch && t.Sections.Any(s => s.Changes.Contains(ch))) ||
            (sender is System.Collections.IList list && t.Sections.Any(s => ReferenceEquals(s.Changes, list))))
            if (!t.ManualSections) t.ManualSections = true;
    }

    private static readonly HashSet<string> StopProps = new() { nameof(RoutePoint.Stop), nameof(RoutePoint.StopKinds), nameof(RoutePoint.Dwell) };

    private bool _linkedQueued, _linkedStops;

    /// <summary>Po zmianie zasady postojów lub toru jazdy – ponownie stosuje postoje i przelicza czasy jazdy karty powiązanej ze schematem.</summary>
    private void QueueLinkedUpdate(bool stops)
    {
        _linkedStops |= stops;
        if (_linkedQueued) return;
        _linkedQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _linkedQueued = false;
            var doStops = _linkedStops; _linkedStops = false;
            var t = SelectedTimetable;
            if (t == null || !t.Route.Linked) return;
            _applyingRoute = true;
            try
            {
                var changed = doStops ? SceneryRouter.ApplyStopPolicy(Project.Scenery, t) : 0;
                var res = RunTimeCalculator.Calculate(t, RestrictionTools.Effective(Project));
                RunTimeCalculator.Apply(t, res);
                if (doStops) Toast(changed == 0 ? "Zasada postojów bez zmian w karcie" : $"Zmieniono postoje na {changed} posterunkach i przeliczono czasy jazdy");
                else Toast("Przeliczono czasy jazdy");
            }
            finally { _applyingRoute = false; }
            Relayout();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private bool _routeQueued;
    private void RecomputeRouteQuiet()
    {
        if (_routeQueued) return;
        _routeQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { _routeQueued = false; RecomputeRoute(); }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
