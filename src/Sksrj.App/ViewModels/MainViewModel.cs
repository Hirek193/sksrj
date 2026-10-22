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
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sksrj.App.Services;
using Sksrj.Core;
using Sksrj.Core.Dictionaries;
using Sksrj.Core.Engine;
using Sksrj.Core.Layout;
using Sksrj.Core.Model;
using Sksrj.Core.Pdf;

namespace Sksrj.App.ViewModels;

public sealed record NavItem(string Label, Material.Icons.MaterialIconKind Icon);

public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly PdfTextMeasurer _measurer = new();
    private readonly DispatcherTimer _debounce;
    private DeepChangeTracker? _tracker;

    public MainViewModel(IDialogService dialogs, TimetableProject? initialProject = null, string? initialPath = null)
    {
        _dialogs = dialogs;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Relayout(); };
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clock.Tick += (_, _) => { if (SelectedTimetable?.UseCurrentGenerationTime == true) Relayout(); };
        _clock.Start();
        LoadProject(initialProject ?? Samples.Project(), initialPath);
        IsDirty = false;
        _loadingSettings = true;
        IsDark = _settings.IsDark;
        SchemeDark = _settings.SchemeDark;
        _loadingSettings = false;
    }

    private readonly AppSettings _settings = AppSettings.Load();
    private bool _loadingSettings;

    /// <summary>Schemat scenerii w trybie ciemnym (tylko edytor – eksport PDF zawsze jasny).</summary>
    [ObservableProperty] private bool _schemeDark;

    partial void OnSchemeDarkChanged(bool value)
    {
        SchemeInvalidated?.Invoke();
        if (_loadingSettings) return;
        _settings.SchemeDark = value; _settings.Save();
    }

    [RelayCommand] private void ToggleSchemeTheme() => SchemeDark = !SchemeDark;

    public string AppName => AppInfo.Name;

    private readonly DispatcherTimer _clock;

    // ================================================================ słowniki dla UI
    public IReadOnlyList<NavItem> Navigation { get; } = new NavItem[]
    {
        new("Schemat", Material.Icons.MaterialIconKind.TransitConnectionVariant),
        new("Pociąg", Material.Icons.MaterialIconKind.Train),
        new("Trasa", Material.Icons.MaterialIconKind.MapMarkerPath),
        new("Posterunki", Material.Icons.MaterialIconKind.FormatListNumbered),
        new("Czasy jazdy", Material.Icons.MaterialIconKind.ChartBellCurveCumulative),
        new("Ostrzeżenia", Material.Icons.MaterialIconKind.AlertOctagonOutline),
        new("Uwagi", Material.Icons.MaterialIconKind.NoteTextOutline),
        new("Wydruk", Material.Icons.MaterialIconKind.PrinterOutline),
    };

    public IReadOnlyList<CodeEntry> CategoryCodes => Ir11.TrainCategoryCodes;
    public IReadOnlyList<CodeEntry> PointTypes => Ir11.PointTypes;
    public IReadOnlyList<CodeEntry> Abbreviations => Ir11.Abbreviations;
    public IReadOnlyList<CodeEntry> StopKindCodes => Ir11.StopKinds;
    public IReadOnlyList<CodeEntry> RunningDaySymbols => Ir11.RunningDaySymbols;
    public IReadOnlyList<CodeEntry> BrakeTypes => Ir11.BrakeTypes;
    public IReadOnlyList<string> TimetableKinds => Ir11.TimetableKindPresets;
    public IReadOnlyList<OrderSignatureType> OrderTypes => Ir11.OrderTypes;
    public IReadOnlyList<TrainKindRow> TrainKinds => Ir11.TrainKinds;

    // ================================================================ stan
    [ObservableProperty] private TimetableProject _project = new();
    [ObservableProperty] private Timetable? _selectedTimetable;
    [ObservableProperty] private RoutePoint? _selectedPoint;
    [ObservableProperty] private int _selectedPage;
    [ObservableProperty] private string? _currentPath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private IReadOnlyList<PageDrawing>? _previewPages;
    [ObservableProperty] private double _zoom = 0.9;
    [ObservableProperty] private bool _isDark;
    [ObservableProperty] private bool _previewVisible = true;
    [ObservableProperty] private string? _snackbar;
    [ObservableProperty] private OrderSignatureType? _orderType = Ir11.OrderTypes[0];
    [ObservableProperty] private string _orderYear = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
    [ObservableProperty] private string _orderSuffix = "";

    public ObservableCollection<string> Warnings { get; } = new();

    public const int PageScheme = 0, PageTrain = 1, PageRoute = 2, PagePoints = 3, PageRun = 4, PageRestrictions = 5, PageNotes = 6, PagePrint = 7;
    public bool IsSchemePage => SelectedPage == PageScheme;
    public bool IsTrainPage => SelectedPage == PageTrain;
    public bool IsRoutePage => SelectedPage == PageRoute;
    public bool IsPointsPage => SelectedPage == PagePoints;
    public bool IsRunPage => SelectedPage == PageRun;
    public bool IsRestrictionsPage => SelectedPage == PageRestrictions;
    public bool IsNotesPage => SelectedPage == PageNotes;
    public bool IsPrintPage => SelectedPage == PagePrint;
    public bool ShowPreviewPane => PreviewVisible && !IsSchemePage;
    public bool ShowNoTimetable => !HasTimetable && !IsSchemePage;
    public bool HasSelectedPoint => SelectedPoint != null;
    public bool HasTimetable => SelectedTimetable != null;
    public int WarningCount => Warnings.Count;
    public string PageCountText => PreviewPages == null ? "" : $"{PreviewPages.Count} {Plural(PreviewPages.Count, "strona", "strony", "stron")}";
    public string WindowTitle => $"{AppInfo.Name} – {(CurrentPath == null ? Project.ProjectName : Path.GetFileName(CurrentPath))}{(IsDirty ? " •" : "")}";
    public string ZoomText => $"{Math.Round(Zoom * 100)}%";
    public int SelectedPointNumber => SelectedTimetable == null || SelectedPoint == null ? 0 : SelectedTimetable.Points.IndexOf(SelectedPoint) + 1;

    public bool LayoutA5 { get => Project.Print.Layout == PageLayoutKind.A5; set { if (value) { Project.Print.Layout = PageLayoutKind.A5; OnPropertyChanged(); OnPropertyChanged(nameof(LayoutA4)); OnPropertyChanged(nameof(SheetLayout)); } } }
    public bool LayoutA4 { get => Project.Print.Layout == PageLayoutKind.A4TwoUp; set { if (value) { Project.Print.Layout = PageLayoutKind.A4TwoUp; OnPropertyChanged(); OnPropertyChanged(nameof(LayoutA5)); OnPropertyChanged(nameof(SheetLayout)); } } }
    public PageLayoutKind SheetLayout => Project.Print.Layout;

    private static string Plural(int n, string one, string few, string many) =>
        n == 1 ? one : (n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14) ? few : many;

    partial void OnSelectedPageChanged(int value)
    {
        OnPropertyChanged(nameof(IsTrainPage)); OnPropertyChanged(nameof(IsRoutePage)); OnPropertyChanged(nameof(IsPointsPage));
        OnPropertyChanged(nameof(IsNotesPage)); OnPropertyChanged(nameof(IsPrintPage)); OnPropertyChanged(nameof(IsSchemePage));
        OnPropertyChanged(nameof(IsRunPage)); OnPropertyChanged(nameof(IsRestrictionsPage)); OnPropertyChanged(nameof(ShowPreviewPane)); OnPropertyChanged(nameof(ShowNoTimetable));
        if (value == PageRestrictions) OnPropertyChanged(nameof(KnownLinesText));
    }

    partial void OnSelectedTimetableChanged(Timetable? value)
    {
        SelectedPoint = value?.Points.FirstOrDefault();
        OnPropertyChanged(nameof(HasTimetable)); OnPropertyChanged(nameof(ShowNoTimetable));
        SyncPreset();
        RecomputeRoute();
        Relayout();
    }

    partial void OnPreviewVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowPreviewPane));

    private void NotifyStructureCommands()
    {
        AddPointCommand.NotifyCanExecuteChanged(); DuplicatePointCommand.NotifyCanExecuteChanged(); DeletePointCommand.NotifyCanExecuteChanged();
        MovePointUpCommand.NotifyCanExecuteChanged(); MovePointDownCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPointChanged(RoutePoint? value)
    {
        OnPropertyChanged(nameof(HasSelectedPoint));
        OnPropertyChanged(nameof(SelectedPointNumber));
    }

    partial void OnPreviewPagesChanged(IReadOnlyList<PageDrawing>? value) => OnPropertyChanged(nameof(PageCountText));
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));
    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(WindowTitle));
    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomText));

    partial void OnIsDarkChanged(bool value)
    {
        if (Application.Current != null) Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        if (_loadingSettings) return;
        SchemeDark = value; // schemat podąża za motywem programu (można go potem przełączyć osobno)
        _settings.IsDark = value; _settings.Save();
    }

    // ================================================================ projekt
    private void LoadProject(TimetableProject p, string? path)
    {
        _tracker?.Dispose();
        Project = p;
        CurrentPath = path;
        _tracker = new DeepChangeTracker(p);
        _tracker.Changed += (_, _) => { IsDirty = true; _debounce.Stop(); _debounce.Start(); OnPropertyChanged(nameof(SelectedPointNumber)); };
        _tracker.ItemChanged += OnTrackedItemChanged;
        CurrentLine = p.Scenery.Lines.FirstOrDefault();
        ClearSelection();
        SelectedTimetable = p.Timetables.FirstOrDefault();
        FitSchemeRequested?.Invoke();
        OnPropertyChanged(nameof(LayoutA5)); OnPropertyChanged(nameof(LayoutA4)); OnPropertyChanged(nameof(SheetLayout));
        OnPropertyChanged(nameof(WindowTitle));
        IsDirty = false;
        Relayout();
    }

    public void OpenPath(string path)
    {
        try
        {
            LoadProject(ProjectSerializer.Load(path), path);
            Toast($"Otwarto {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Toast("Nie udało się otworzyć pliku: " + ex.Message); }
    }

    private void Relayout()
    {
        Warnings.Clear();
        if (SelectedTimetable == null) { PreviewPages = null; OnPropertyChanged(nameof(WarningCount)); return; }
        try
        {
            TimetableTools.UpdateHiddenFlags(SelectedTimetable);
            var r = TimetableLayout.Build(SelectedTimetable, Project.Print, _measurer, null, RestrictionTools.Effective(Project));
            PreviewPages = r.Pages;
            foreach (var w in r.Warnings.Distinct()) Warnings.Add(w);
            ComputeRunTime();
            if (RunResult != null) foreach (var w in RunResult.Warnings.Distinct()) if (!Warnings.Contains(w)) Warnings.Add(w);
        }
        catch (Exception ex)
        {
            Warnings.Add("Błąd składu: " + ex.Message);
        }
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(SheetLayout));
    }

    public void ShowMessage(string text) => Toast(text);

    private CancellationTokenSource? _toastCts;
    private async void Toast(string text)
    {
        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();
        Snackbar = text;
        try { await Task.Delay(3500, cts.Token); Snackbar = null; } catch (TaskCanceledException) { }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!IsDirty) return true;
        return await _dialogs.ConfirmAsync("Niezapisane zmiany", "Bieżący projekt ma niezapisane zmiany. Czy porzucić je?", "Porzuć", "Anuluj");
    }

    [RelayCommand]
    private async Task NewProject()
    {
        if (!await ConfirmDiscardAsync()) return;
        var p = new TimetableProject { ProjectName = "Nowy projekt" };
        p.Timetables.Add(Samples.NewEmpty());
        LoadProject(p, null);
    }

    [RelayCommand]
    private async Task LoadSamples()
    {
        if (!await ConfirmDiscardAsync()) return;
        LoadProject(Samples.Project(), null);
    }

    [RelayCommand]
    private async Task Open()
    {
        if (!await ConfirmDiscardAsync()) return;
        var path = await _dialogs.OpenProjectAsync();
        if (path != null) OpenPath(path);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (CurrentPath == null) { await SaveAs(); return; }
        try
        {
            ProjectSerializer.Save(Project, CurrentPath);
            IsDirty = false;
            Toast("Zapisano projekt");
        }
        catch (Exception ex) { Toast("Błąd zapisu: " + ex.Message); }
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        var path = await _dialogs.SaveProjectAsync(SafeName(Project.ProjectName) + ProjectSerializer.Extension);
        if (path == null) return;
        CurrentPath = path;
        await Save();
    }

    private static string SafeName(string s)
    {
        var bad = Path.GetInvalidFileNameChars();
        var r = new string((s ?? "rozklad").Select(c => bad.Contains(c) || c == '/' ? '_' : c).ToArray()).Trim();
        return r.Length == 0 ? "rozklad" : r;
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (SelectedTimetable == null) return;
        var t = SelectedTimetable;
        var name = SafeName($"Karta_{t.TrainCategory}_{t.TrainNumber}".Replace(' ', '_')) + ".pdf";
        var path = await _dialogs.SavePdfAsync(name);
        if (path == null) return;
        try
        {
            var layout = TimetableLayout.Build(t, Project.Print, _measurer, null, RestrictionTools.Effective(Project));
            await using var fs = File.Create(path);
            PdfExporter.Export(new[] { (t, layout) }, Project.Print, fs, $"Karta rozkładu jazdy pociągu {t.TrainCategory} {t.TrainNumber}".Trim());
            Toast($"Zapisano {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Toast("Błąd eksportu: " + ex.Message); }
    }

    [RelayCommand]
    private async Task ExportAllPdf()
    {
        if (Project.Timetables.Count == 0) return;
        var path = await _dialogs.SavePdfAsync(SafeName(Project.ProjectName) + ".pdf");
        if (path == null) return;
        try
        {
            var cards = Project.Timetables.Select(t => (t, TimetableLayout.Build(t, Project.Print, _measurer, null, RestrictionTools.Effective(Project)))).ToList();
            await using var fs = File.Create(path);
            PdfExporter.Export(cards, Project.Print, fs, Project.ProjectName);
            Toast($"Zapisano {cards.Count} kart do {Path.GetFileName(path)}");
        }
        catch (Exception ex) { Toast("Błąd eksportu: " + ex.Message); }
    }

    [RelayCommand] private void ToggleTheme() => IsDark = !IsDark;
    [RelayCommand] private Task ShowAbout() => _dialogs.ShowAboutAsync();
    [RelayCommand] private void TogglePreview() => PreviewVisible = !PreviewVisible;
    [RelayCommand] private void ZoomIn() => Zoom = Math.Min(3, Math.Round(Zoom + 0.1, 2));
    [RelayCommand] private void ZoomOut() => Zoom = Math.Max(0.3, Math.Round(Zoom - 0.1, 2));
    [RelayCommand] private void Refresh() => Relayout();

    // ================================================================ pociągi
    [RelayCommand]
    private void AddTimetable()
    {
        var t = Project.Scenery.Nodes.Count > 0 ? new Timetable { TrainCategory = "ROJ" } : Samples.NewEmpty();
        if (SelectedTimetable != null)
        {
            t.Issuer = SelectedTimetable.Issuer; t.Carrier = SelectedTimetable.Carrier; t.SystemName = SelectedTimetable.SystemName;
        }
        Project.Timetables.Add(t);
        SelectedTimetable = t;
        if (Project.Scenery.Nodes.Count > 0)
        {
            SelectedPage = PageScheme; Tool = SchemeTool.Route;
            Toast("Nowa karta – kliknij na schemacie początek, punkty pośrednie i koniec trasy");
        }
        else SelectedPage = PageTrain;
    }

    [RelayCommand]
    private void DuplicateTimetable()
    {
        if (SelectedTimetable == null) return;
        var t = ProjectSerializer.Clone(SelectedTimetable);
        t.TrainNumber = (t.TrainNumber + " (kopia)").Trim();
        Project.Timetables.Insert(Project.Timetables.IndexOf(SelectedTimetable) + 1, t);
        SelectedTimetable = t;
        Toast("Utworzono kopię karty");
    }

    [RelayCommand]
    private async Task DeleteTimetable()
    {
        if (SelectedTimetable == null) return;
        if (!await _dialogs.ConfirmAsync("Usuń kartę", $"Usunąć kartę rozkładu „{SelectedTimetable.DisplayName}”?", "Usuń")) return;
        var i = Project.Timetables.IndexOf(SelectedTimetable);
        Project.Timetables.Remove(SelectedTimetable);
        SelectedTimetable = Project.Timetables.Count == 0 ? null : Project.Timetables[Math.Clamp(i, 0, Project.Timetables.Count - 1)];
    }

    // ================================================================ strona tytułowa
    [RelayCommand] private void SetTimetableKind(string kind) { if (SelectedTimetable != null) SelectedTimetable.TimetableKind = kind; }

    [RelayCommand]
    private void BuildOrderNumber()
    {
        if (SelectedTimetable == null || OrderType == null) return;
        var id = string.IsNullOrWhiteSpace(SelectedTimetable.SystemNumber) ? SelectedTimetable.ApplicationNumber : SelectedTimetable.SystemNumber;
        SelectedTimetable.OrderNumber = OrderType.Build(OrderYear.Trim(), (id ?? "").Trim(), OrderSuffix);
    }

    [RelayCommand]
    private void FillRelation()
    {
        var t = SelectedTimetable;
        if (t == null || t.Points.Count == 0) return;
        t.RelationFrom = t.Points[0].Name;
        t.RelationTo = t.Points[^1].Name;
    }

    [RelayCommand] private void ResetDriverMessage() { if (SelectedTimetable != null) SelectedTimetable.DriverMessage = Timetable.DefaultDriverMessage; }

    // ================================================================ trasa
    [RelayCommand]
    private void AddSection()
    {
        var t = SelectedTimetable; if (t == null) return;
        var last = t.Sections.LastOrDefault();
        var s = new LineSection { LineNumber = last?.LineNumber ?? "", KmStart = last?.KmEnd ?? "0.000", KmEnd = last?.KmEnd ?? "0.000", Track = last?.Track ?? TrackSide.Right };
        var lastCh = last?.Changes.LastOrDefault();
        s.Changes.Add(new SpeedChange { Km = s.KmStart, Vp = lastCh?.Vp ?? "60", Vl = lastCh?.Vl ?? "60", SingleTrack = lastCh?.SingleTrack ?? false });
        t.Sections.Add(s);
    }

    [RelayCommand]
    private void RemoveSection(LineSection s) => SelectedTimetable?.Sections.Remove(s);

    [RelayCommand]
    private void MoveSectionUp(LineSection s) => Move(SelectedTimetable?.Sections, s, -1);

    [RelayCommand]
    private void MoveSectionDown(LineSection s) => Move(SelectedTimetable?.Sections, s, +1);

    [RelayCommand]
    private void AddChange(LineSection s)
    {
        var last = s.Changes.LastOrDefault();
        s.Changes.Add(new SpeedChange { Km = last?.Km ?? s.KmStart, Vp = last?.Vp ?? "", Vl = last?.Vl ?? "", SingleTrack = last?.SingleTrack ?? false });
    }

    [RelayCommand]
    private void RemoveChange(SpeedChange c)
    {
        var t = SelectedTimetable; if (t == null) return;
        foreach (var s in t.Sections) if (s.Changes.Remove(c)) return;
    }

    private static void Move<T>(ObservableCollection<T>? list, T item, int d)
    {
        if (list == null) return;
        var i = list.IndexOf(item); var j = i + d;
        if (i < 0 || j < 0 || j >= list.Count) return;
        list.Move(i, j);
    }

    // ================================================================ posterunki
    [RelayCommand(CanExecute = nameof(CanEditStructure))]
    private void AddPoint()
    {
        var t = SelectedTimetable; if (t == null) return;
        var p = new RoutePoint { Name = "Nowy posterunek", TracksToNext = SelectedPoint?.TracksToNext ?? TrackCount.Double };
        var i = SelectedPoint == null ? t.Points.Count : t.Points.IndexOf(SelectedPoint) + 1;
        t.Points.Insert(i, p);
        SelectedPoint = p;
    }

    [RelayCommand(CanExecute = nameof(CanEditStructure))]
    private void DuplicatePoint()
    {
        var t = SelectedTimetable; if (t == null || SelectedPoint == null) return;
        var p = TimetableTools.ClonePoint(SelectedPoint);
        t.Points.Insert(t.Points.IndexOf(SelectedPoint) + 1, p);
        SelectedPoint = p;
    }

    [RelayCommand(CanExecute = nameof(CanEditStructure))]
    private void DeletePoint()
    {
        var t = SelectedTimetable; if (t == null || SelectedPoint == null) return;
        var i = t.Points.IndexOf(SelectedPoint);
        t.Points.Remove(SelectedPoint);
        SelectedPoint = t.Points.Count == 0 ? null : t.Points[Math.Clamp(i, 0, t.Points.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(CanEditStructure))]
    private void MovePointUp() { if (SelectedPoint != null) { var p = SelectedPoint; Move(SelectedTimetable?.Points, p, -1); SelectedPoint = p; OnPropertyChanged(nameof(SelectedPointNumber)); } }

    [RelayCommand(CanExecute = nameof(CanEditStructure))]
    private void MovePointDown() { if (SelectedPoint != null) { var p = SelectedPoint; Move(SelectedTimetable?.Points, p, +1); SelectedPoint = p; OnPropertyChanged(nameof(SelectedPointNumber)); } }

    [RelayCommand]
    private void RecalcTimes()
    {
        if (SelectedTimetable == null) return;
        var n = TimetableTools.RecalculateTimes(SelectedTimetable);
        Toast(n == 0 ? "Brak zmian – sprawdź odjazd z pierwszego posterunku i czasy jazdy" : $"Przeliczono godziny ({n} posterunków)");
    }

    [RelayCommand]
    private void RecalcTravel()
    {
        if (SelectedTimetable == null) return;
        var n = TimetableTools.RecalculateTravelTimes(SelectedTimetable);
        Toast($"Uzupełniono czasy jazdy ({n} posterunków)");
    }

    private static string AppendList(string current, string code)
    {
        current = (current ?? "").TrimEnd();
        if (current.Length == 0) return code;
        return current.EndsWith(",") ? current + " " + code : current + ", " + code;
    }

    [RelayCommand]
    private void InsertAbbreviation(CodeEntry e) { if (SelectedPoint != null) SelectedPoint.Abbreviations = AppendList(SelectedPoint.Abbreviations, e.Code); }

    [RelayCommand]
    private void InsertStopKind(CodeEntry e)
    {
        if (SelectedPoint == null) return;
        var cur = (SelectedPoint.StopKinds ?? "").Trim();
        SelectedPoint.StopKinds = cur.Length == 0 ? e.Code : cur + "," + e.Code;
        if (e.Code is "ph" or "pt" or "pn" or "pm" or "zd") SelectedPoint.Stop = true;
    }

    [RelayCommand] private void SetPointType(CodeEntry e) { if (SelectedPoint != null) SelectedPoint.TypeSuffix = e.Code; }
    [RelayCommand] private void SetBrakeType(CodeEntry e) { if (SelectedPoint != null) SelectedPoint.BrakeType = e.Code; }

    [RelayCommand]
    private void MarkStation()
    {
        if (SelectedPoint == null) return;
        SelectedPoint.Bold = true; SelectedPoint.Uppercase = false;
    }

    // ================================================================ uwagi
    [RelayCommand]
    private void InsertRunningSymbol(CodeEntry e)
    {
        if (SelectedTimetable == null) return;
        var t = SelectedTimetable;
        var cur = (t.RunningDays ?? "").TrimEnd();
        t.RunningDays = cur.Length == 0 ? "- " + e.Code + " " : cur + (cur.EndsWith(";") ? "\n- " : " ") + e.Code + " ";
    }

    [RelayCommand]
    private void AddFootnote()
    {
        var t = SelectedTimetable; if (t == null) return;
        t.Footnotes.Add(new Footnote { Marker = $"*{t.Footnotes.Count + 1})", Text = "" });
    }

    [RelayCommand] private void RemoveFootnote(Footnote f) => SelectedTimetable?.Footnotes.Remove(f);
}
