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

using System.Globalization;
using Sksrj.Core.Model;
using Sksrj.Core.Util;

namespace Sksrj.Core.Layout;

public sealed class LayoutResult
{
    public List<PageDrawing> Pages { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Składa kartę rozkładu jazdy pociągu wg wzoru z Załącznika 5 do Ir-11:
/// strona tytułowa + strony szczegółowego rozkładu w układzie 9 kolumn.
/// Wszystkie wymiary w punktach (1 pt = 1/72 cala), strona A5 pionowo.
/// </summary>
public sealed class TimetableLayout
{
    // ---------------------------------------------------------------- geometria strony A5
    public const double PageW = 419.53, PageH = 595.28;
    private const double ML = 28.25, MR = 26;

    // szerokości kolumn (proporcje zmierzone na wzorze karty)
    private const double WNr = 19.75, WKm = 36, WVp = 20, WVl = 20, WSt = 136.5, WGap = 3.0,
        WTime = 34.5, WTravel = 16, WLok = 29.5, WObc = 25.5, WVmax = 21.5;

    // strona szczegółowa: nagłówek na wysokości górnej krawędzi ramki strony tytułowej (jak w kartach SKRJ)
    private const double HeaderBaseline = 40;
    private const double TableTop = 45.5;
    private const double ColHeadH = 26;

    // ramka strony tytułowej
    private const double TitleX0 = 30, TitleX1 = 386.5, TitleY0 = 30.5, TitleY1 = 560.5;
    private const double BodyTop = TableTop + ColHeadH;

    // wiersze kolumny 5
    private const double LH = 12.5;
    private const double FirstBase = 11.0;   // od góry wiersza do linii bazowej 1. wiersza tekstu
    private const double LastBase = 3.2;     // od dołu wiersza do linii bazowej ostatniego wiersza
    private const double LeftMinCell = 12.0, LeftLabelH = 9.5;
    private const double NameKmGap = LH / 2;
    /// <summary>Granica oznaczenia szlaku (kol. 5|6) – tyle poniżej linii bazowej nazwy posterunku.</summary>
    private const double MarkerBelowName = 3.2;

    private const double Thin = 0.5, Frame = 0.75;
    private static readonly Rgb ShadeColor = new(217, 217, 217);

    // czcionki
    private static readonly FontSpec FName = new(10), FNameB = new(10, true), FItal = new(10, false, true);
    private static readonly FontSpec FTime = new(10), FTimeB = new(10, true), FTravel = new(8);
    private static readonly FontSpec FBox = new(8, true), FLeftKm = new(8), FLeftKmB = new(8, true);
    private static readonly FontSpec FSpeed = new(9), FSpeedB = new(9, true), FLineNr = new(8);
    private static readonly FontSpec FSmall = new(6.5), FLoad = new(9), FHead = new(9, true), FHeadNr = new(9.5, true);
    private static readonly FontSpec FNrHead = new(7);
    private static readonly FontSpec FCol = new(10), FColSmall = new(6), FFoot = new(9), FFootB = new(9.5, true);

    // x kolumn
    private readonly double xNr, xKm, xVp, xVl, xSt, xGap, xTime, xTravel, xLok, xObc, xVmax, xEnd;

    private readonly Timetable _t;
    private readonly PrintSettings _print;
    private readonly ITextMeasurer _m;
    private readonly LayoutResult _result = new();
    private readonly CultureInfo _pl = new("pl-PL");

    public TimetableLayout(Timetable t, PrintSettings print, ITextMeasurer measurer)
    {
        _t = t; _print = print; _m = measurer;
        xNr = ML; xKm = xNr + WNr; xVp = xKm + WKm; xVl = xVp + WVp; xSt = xVl + WVl; xGap = xSt + WSt;
        xTime = xGap + WGap; xTravel = xTime + WTime; xLok = xTravel + WTravel; xObc = xLok + WLok; xVmax = xObc + WObc; xEnd = xVmax + WVmax;
    }

    public static LayoutResult Build(Timetable t, PrintSettings print, ITextMeasurer m, DateTime? now = null, IEnumerable<SpeedRestriction>? restrictions = null)
    {
        var r = new TimetableLayout(t, print, m).Run(now ?? DateTime.Now);
        if (print.IncludeRestrictionList && restrictions != null && t.Points.Count >= 2)
        {
            var g = Engine.RouteGeometry.Build(t);
            var list = Engine.RunTimeCalculator.MatchRestrictions(t, g, restrictions);
            r.Pages.AddRange(Engine.RestrictionTools.ListPages(t, list, m, r.Pages.Count + 1, print));
        }
        return r;
    }

    // ================================================================= model pośredni
    private sealed class Sec
    {
        public int Index; public string Line = ""; public double KmStart, KmEnd, S0, S1; public TrackSide Side;
        public int EvStart, EvEnd;
        /// <summary>Grupa wyświetlania – kolejne odcinki tej samej linii z ciągłym kilometrażem drukowane są jako jeden.</summary>
        public int Group;
        /// <summary>Odcinek kontynuuje poprzedni (ta sama linia, ten sam kilometr) – bez granicy odcinków w kolumnach 1–2.</summary>
        public bool Continuation;
    }

    private sealed class Ev
    {
        public double S, SClamped, Y, Gap;
        public string? LabelAbove, LabelBelow; public bool AboveBold, BelowBold;
        public bool Boundary;
    }

    private sealed class Seg
    {
        public int Sec; public int Ev0, Ev1; public string Vp = "", Vl = ""; public bool Single; public BoldMode Bold;
    }

    private sealed class Row
    {
        public RoutePoint P = null!;
        public int Index;
        public double S, H, Top, Bottom;
        public List<(string text, FontSpec font)> TopLines = new();
        public List<(string left, string right, FontSpec lf)> BottomLines = new();
        public int Lines;
        public string? Box;
        public string Cat = "";
        public string[] Stock = new string[8];
        public bool StockChanged;
    }

    private readonly List<Sec> _secs = new();
    private readonly List<Ev> _evs = new();
    private readonly List<Seg> _segs = new();
    private readonly List<Row> _rows = new();
    private double _tableEnd;

    // ================================================================= przebieg główny
    private LayoutResult Run(DateTime now)
    {
        _result.Pages.Add(BuildTitlePage(now));

        BuildSections();
        BuildRows();
        if (_rows.Count == 0)
        {
            _result.Warnings.Add("Brak posterunków – dodaj przynajmniej dwa punkty trasy.");
            return _result;
        }
        SolveVertical();
        Paginate();
        return _result;
    }

    // ================================================================= strona tytułowa
    private PageDrawing BuildTitlePage(DateTime now)
    {
        var page = new PageDrawing { Width = PageW, Height = PageH, Number = 1 };
        var c = new Canvas(page, _m);
        // geometria zmierzona na rzeczywistej karcie SKRJ (A5): ramka 30–386,5 × 30,5–560,5 pt
        double x0 = TitleX0, x1 = TitleX1, y0 = TitleY0, y1 = TitleY1;
        double h = y1 - y0, w = x1 - x0, cx = (x0 + x1) / 2;
        double s1 = y0 + h * 0.161, s2 = y0 + h * 0.778;

        // ramka
        const double bw = 1.6;
        c.Fill(x0 - bw / 2, y0 - bw / 2, w + bw, bw); c.Fill(x0 - bw / 2, y1 - bw / 2, w + bw, bw);
        c.Fill(x0 - bw / 2, y0 - bw / 2, bw, h + bw); c.Fill(x1 - bw / 2, y0 - bw / 2, bw, h + bw);
        c.HLine(x0, x1, s1, 1.1); c.HLine(x0, x1, s2, 1.1);

        // nagłówek – wydawca (11 pt, pogrubiona)
        var fIss = new FontSpec(11, true);
        const double issLh = 15.9;
        var iss = Lines(_t.Issuer).ToList();
        var ib = (y0 + s1) / 2 - (iss.Count - 1) * issLh / 2 + 7.5;
        foreach (var l in iss) { c.Text(cx, ib, l, FitFont(c, l, fIss, w - 16), Align.Center); ib += issLh; }

        // część środkowa
        var mh = s2 - s1;
        double Y(double frac) => s1 + mh * frac;
        var fTitle = new FontSpec(13, true);
        var fMid = new FontSpec(11, true);
        var fGen = new FontSpec(12, true);
        const double titleLh = 20.4;
        var kind = (_t.TimetableKind ?? "").Trim();
        var titleLines = c.Wrap(kind.ToUpper(_pl), fTitle, w - 24);
        var ty = Y(0.1346);
        foreach (var l in titleLines) { c.Text(cx, ty, l, fTitle, Align.Center); ty += titleLh; }
        c.Text(cx, ty, $"POCIĄGU NR {_t.TrainNumber}".TrimEnd(), fTitle, Align.Center);

        if (!string.IsNullOrWhiteSpace(_t.Carrier))
            c.Text(cx, Y(0.388), _t.Carrier.Trim(), FitFont(c, _t.Carrier.Trim(), fMid, w - 16), Align.Center);
        if (!string.IsNullOrWhiteSpace(_t.ApplicationNumber) || !string.IsNullOrWhiteSpace(_t.ApplicationDate))
        {
            var s = "na wniosek nr " + _t.ApplicationNumber.Trim();
            if (!string.IsNullOrWhiteSpace(_t.ApplicationDate)) s += " z dnia " + _t.ApplicationDate.Trim();
            c.Text(cx, Y(0.500), s, FitFont(c, s, fMid, w - 16), Align.Center);
        }
        if (!string.IsNullOrWhiteSpace(_t.OrderNumber) || !string.IsNullOrWhiteSpace(_t.OrderDate))
        {
            var s = "zarządzenie " + _t.OrderNumber.Trim();
            if (!string.IsNullOrWhiteSpace(_t.OrderDate)) s += " z dnia " + _t.OrderDate.Trim();
            c.Text(cx, Y(0.594), s, FitFont(c, s, fMid, w - 16), Align.Center);
        }

        string gd, gt;
        if (_t.UseCurrentGenerationTime || string.IsNullOrWhiteSpace(_t.GenerationDate))
        { gd = now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); gt = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture); }
        else { gd = _t.GenerationDate.Trim(); gt = string.IsNullOrWhiteSpace(_t.GenerationTime) ? "" : _t.GenerationTime.Trim(); }
        c.Text(cx, Y(0.780), "wygenerowano w systemie", fGen, Align.Center);
        c.Text(cx, Y(0.841), string.IsNullOrWhiteSpace(_t.SystemName) ? "SKSRJ" : _t.SystemName.Trim(), fGen, Align.Center);
        var genLine = $"w dniu {gd} r." + (gt.Length > 0 ? $" o godz. {gt}" : "");
        c.Text(cx, Y(0.902), genLine, FitFont(c, genLine, fGen, w - 16), Align.Center);

        // stopka – informacja dla maszynisty (10 pt, pogrubiona)
        var fMsg = new FontSpec(10, true);
        var msg = new List<string>();
        foreach (var l in Lines(_t.DriverMessage)) msg.AddRange(c.Wrap(l, fMsg, w - 14));
        const double msgLh = 11.5;
        var my = (s2 + y1) / 2 - (msg.Count - 1) * msgLh / 2 + 2.7;
        for (var i = 0; i < msg.Count; i++)
        {
            c.Text(cx, my, msg[i], fMsg, Align.Center, underline: _t.UnderlineLastMessageLine && i == msg.Count - 1);
            my += msgLh;
        }
        SimulationNote(c);
        return page;
    }

    private static FontSpec FitFont(Canvas c, string s, FontSpec f, double width)
    {
        var size = f.Size;
        while (size > 5 && c.W(s, f with { Size = size }) > width) size -= 0.25;
        return f with { Size = size };
    }

    private static IEnumerable<string> Lines(string? s) =>
        (s ?? "").Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse();

    private void SimulationNote(Canvas c)
    {
        if (!_print.SimulationNote || string.IsNullOrWhiteSpace(_print.SimulationNoteText)) return;
        c.Text(ML, PageH - 9, _print.SimulationNoteText.Trim(), new FontSpec(6, false, true), Align.Left, Rgb.Grey);
    }

    // ================================================================= kolumny 1–4 (odcinki linii, prędkości)
    private static string EffTrain(Seg s, TrackSide side) => s.Single ? s.Vp : side == TrackSide.Right ? s.Vp : s.Vl;

    private static double ParseV(string? v) =>
        double.TryParse((v ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN;

    private static string Cap(string v, double vmax)
    {
        var d = ParseV(v);
        if (double.IsNaN(d) || double.IsNaN(vmax) || vmax <= 0 || d <= vmax) return v;
        return vmax.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>Prędkość maksymalna pociągu (kol. 9) obowiązująca od kolejnych posterunków – do ograniczania kolumn 3–4.</summary>
    private List<(double S, double V)> VmaxSteps()
    {
        var res = new List<(double, double)>();
        if (!_t.CapSpeedsToVmax || _t.Points.Count == 0) return res;
        var g = Engine.RouteGeometry.Build(_t);
        double cur = double.NaN;
        for (var i = 0; i < _t.Points.Count && i < g.PointS.Count; i++)
        {
            var v = ParseV(_t.Points[i].Vmax);
            if (double.IsNaN(v) || v <= 0 || v == cur) continue;
            res.Add((res.Count == 0 ? double.NegativeInfinity : g.PointS[i], v));
            cur = v;
        }
        return res;
    }

    private void BuildSections()
    {
        double s = 0;
        var vmax = VmaxSteps();
        double VmaxAt(double x)
        {
            var v = double.NaN;
            foreach (var (s0, vv) in vmax) { if (x >= s0 - 1e-9) v = vv; else break; }
            return v;
        }
        var group = -1;
        Seg? prevLast = null;
        foreach (var (ls, idx) in _t.Sections.Select((x, i) => (x, i)))
        {
            if (!Num.TryParseKm(ls.KmStart, out var k0)) { _result.Warnings.Add($"Odcinek linii {ls.LineNumber}: niepoprawny km początku „{ls.KmStart}”."); k0 = 0; }
            if (!Num.TryParseKm(ls.KmEnd, out var k1)) { _result.Warnings.Add($"Odcinek linii {ls.LineNumber}: niepoprawny km końca „{ls.KmEnd}”."); k1 = k0; }
            var sec = new Sec { Index = idx, Line = ls.LineNumber?.Trim() ?? "", KmStart = k0, KmEnd = k1, S0 = s, S1 = s + Math.Abs(k1 - k0), Side = ls.Track };
            s = sec.S1;
            var dir = Math.Sign(k1 - k0);

            var prev = _secs.Count > 0 ? _secs[^1] : null;
            var prevDir = prev == null ? 0 : Math.Sign(prev.KmEnd - prev.KmStart);
            sec.Continuation = prev != null && string.Equals(prev.Line, sec.Line, StringComparison.OrdinalIgnoreCase)
                               && Math.Abs(prev.KmEnd - k0) < 0.0015 && (dir == 0 || prevDir == 0 || dir == prevDir);
            if (!sec.Continuation) group++;
            sec.Group = group;

            // ---- zmiany prędkości odcinka (+ zmiany Vmax pociągu na posterunkach), ograniczone do Vmax
            var raw = new List<(double s, string vp, string vl, bool single, BoldMode bold, string? km)>();
            var changes = ls.Changes.ToList();
            if (changes.Count == 0) changes.Add(new SpeedChange { Km = ls.KmStart });
            double lastKm = k0;
            for (var i = 0; i < changes.Count; i++)
            {
                var ch = changes[i];
                var vp = (ch.Vp ?? "").Trim(); var vl = (ch.Vl ?? "").Trim();
                if (ch.SingleTrack && vp.Length == 0) vp = vl;
                if (i == 0) { raw.Add((sec.S0, vp, vl, ch.SingleTrack, ch.BoldKm, null)); continue; }
                if (!Num.TryParseKm(ch.Km, out var km)) { _result.Warnings.Add($"Linia {sec.Line}: niepoprawny km zmiany prędkości „{ch.Km}”."); continue; }
                var inRange = km >= Math.Min(k0, k1) - 1e-6 && km <= Math.Max(k0, k1) + 1e-6;
                if (!inRange) _result.Warnings.Add($"Linia {sec.Line}: km {Num.KmDot(km)} poza odcinkiem {Num.KmDot(k0)}–{Num.KmDot(k1)}.");
                if (dir != 0 && Math.Sign(km - lastKm) == -dir) _result.Warnings.Add($"Linia {sec.Line}: zmiany prędkości nie są uporządkowane w kierunku jazdy (km {Num.KmDot(km)}).");
                lastKm = km;
                raw.Add((Math.Clamp(sec.S0 + Math.Abs(km - k0), sec.S0, sec.S1), vp, vl, ch.SingleTrack, ch.BoldKm, Num.KmComma(km)));
            }
            foreach (var (vs, _) in vmax)
            {
                if (vs <= sec.S0 + 1e-6 || vs >= sec.S1 - 1e-6) continue;
                var baseC = raw.Last(r => r.s <= vs + 1e-9);
                raw.Add((vs, baseC.vp, baseC.vl, baseC.single, BoldMode.Auto, Num.KmComma(k0 + (dir == 0 ? 1 : dir) * (vs - sec.S0))));
            }
            raw = raw.OrderBy(r => r.s).ToList();
            var segsIn = raw.Select(r =>
            {
                var v = VmaxAt(r.s);
                return (r.s, seg: new Seg { Sec = idx, Vp = Cap(r.vp, v), Vl = Cap(r.vl, v), Single = r.single, Bold = r.bold }, r.km);
            }).ToList();

            // ---- zdarzenie początkowe
            var first = segsIn[0].seg;
            if (_evs.Count == 0)
                _evs.Add(new Ev { S = sec.S0, Boundary = true, LabelBelow = Num.KmComma(k0), BelowBold = false });
            else if (!sec.Continuation)
            {
                var e = _evs[^1];
                e.LabelBelow = Num.KmComma(k0); e.BelowBold = true; e.AboveBold = true; e.Boundary = true;
            }
            else
            {
                // styk odcinków tej samej linii (posterunek): granica tylko wtedy, gdy zmienia się prędkość lub tor jazdy
                var e = _evs[^1];
                e.Boundary = false; e.LabelAbove = null; e.AboveBold = false;
                var changed = prevLast == null || prevLast.Vp != first.Vp || prevLast.Vl != first.Vl || prevLast.Single != first.Single || prev!.Side != sec.Side;
                if (changed)
                {
                    // pogrubienie tylko wtedy, gdy zmienia się prędkość po torze, dla którego opracowano rozkład
                    var trainChanged = prevLast == null || EffTrain(prevLast, prev!.Side) != EffTrain(first, sec.Side);
                    e.LabelAbove = Num.KmComma(k0);
                    e.AboveBold = first.Bold switch { BoldMode.Yes => true, BoldMode.No => false, _ => trainChanged };
                }
            }
            sec.EvStart = _evs.Count - 1;

            Seg cur = first; cur.Ev0 = sec.EvStart;
            for (var i = 1; i < segsIn.Count; i++)
            {
                var (sKm, next, kmText) = segsIn[i];
                if (next.Vp == cur.Vp && next.Vl == cur.Vl && next.Single == cur.Single) continue; // po ograniczeniu do Vmax bez zmiany
                var trainChanged = EffTrain(cur, sec.Side) != EffTrain(next, sec.Side);
                var bold = next.Bold switch { BoldMode.Yes => true, BoldMode.No => false, _ => trainChanged };
                _evs.Add(new Ev { S = sKm, LabelAbove = kmText, AboveBold = bold });
                cur.Ev1 = _evs.Count - 1; _segs.Add(cur);
                next.Ev0 = _evs.Count - 1;
                cur = next;
            }
            // zdarzenie końcowe
            _evs.Add(new Ev { S = sec.S1, Boundary = true, LabelAbove = Num.KmComma(k1), AboveBold = false });
            sec.EvEnd = _evs.Count - 1;
            cur.Ev1 = sec.EvEnd; _segs.Add(cur);
            prevLast = cur;
            _secs.Add(sec);
        }
        // pogrubienie kilometrów granicznych: wewnętrzne granice odcinków (zmiana linii / kilometrażu) pogrubione
        for (var i = 0; i < _secs.Count; i++)
        {
            if (i > 0 && !_secs[i].Continuation) _evs[_secs[i].EvStart].BelowBold = true;
            if (i < _secs.Count - 1 && !_secs[i + 1].Continuation) _evs[_secs[i].EvEnd].AboveBold = true;
        }
        // minimalne odstępy
        for (var j = 1; j < _evs.Count; j++)
        {
            var below = _evs[j - 1].LabelBelow != null; var above = _evs[j].LabelAbove != null;
            var need = (below ? LeftLabelH : 0) + (above ? LeftLabelH : 0) + 0.5;
            var unlabeledJunction = (!_evs[j].Boundary && !above && _evs[j].LabelBelow == null) || (!_evs[j - 1].Boundary && _evs[j - 1].LabelAbove == null && !below);
            _evs[j].Gap = unlabeledJunction ? (above ? need : 0) : Math.Max(LeftMinCell, need);
        }
    }

    // ================================================================= kolumny 5–9 (posterunki)
    private void BuildRows()
    {
        var pts = _t.Points.ToList();
        string effCat = (_t.TrainCategory ?? "").Trim(), effNum = (_t.TrainNumber ?? "").Trim();
        string? prevBox = null;
        var stock = new string[8];
        int secPtr = 0; double prevS = 0;
        var pendingStock = false;

        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (TimetableTools.IsHiddenInCard(_t, i))
            {
                // przystanek osobowy bez postoju – nie jest drukowany; zmiany numeru pociągu i składu
                // przechodzą na następny drukowany posterunek
                if (!string.IsNullOrWhiteSpace(p.TrainCategory)) effCat = p.TrainCategory.Trim();
                if (!string.IsNullOrWhiteSpace(p.TrainNumber)) effNum = p.TrainNumber.Trim();
                var hv = new[] { p.Loco1, p.Loco2, p.Loco3, p.LocoLoad, p.TrainLength, p.Vmax, p.BrakePercent, p.BrakeType };
                for (var k = 0; k < 8; k++)
                {
                    var v = (hv[k] ?? "").Trim();
                    if (v.Length > 0 && v != (stock[k] ?? "")) { pendingStock = true; stock[k] = v; }
                }
                continue;
            }
            var r = new Row { P = p, Index = i };

            // --- położenie na trasie (s)
            r.S = LocatePoint(p, i, ref secPtr, prevS);
            if (i > 0 && r.S < prevS - 1e-9) r.S = prevS;
            prevS = r.S;

            // --- teksty kolumny 5
            var name = (p.Name ?? "").Trim();
            if (p.Uppercase) name = name.ToUpper(_pl);
            if (!string.IsNullOrWhiteSpace(p.TypeSuffix)) name += " " + p.TypeSuffix.Trim();
            if (!string.IsNullOrWhiteSpace(p.StopKinds)) name += " ; " + p.StopKinds.Trim();
            if (!string.IsNullOrWhiteSpace(p.NoteTop)) r.TopLines.Add((p.NoteTop.Trim(), FItal));
            r.TopLines.Add((name, p.Bold ? FNameB : FName));

            var abbr = Lines(p.Abbreviations).Select(l => l.Trim()).ToList();
            if (abbr.Count == 0) abbr.Add("");
            for (var k = 0; k < abbr.Count - 1; k++) r.BottomLines.Add(("", abbr[k], FName));
            r.BottomLines.Add(((p.Km ?? "").Trim(), abbr[^1], FName));
            if (!string.IsNullOrWhiteSpace(p.NoteBottom)) r.BottomLines.Add((p.NoteBottom.Trim(), "", FItal));

            // --- rodzaj i numer pociągu
            if (!string.IsNullOrWhiteSpace(p.TrainCategory)) effCat = p.TrainCategory.Trim();
            if (!string.IsNullOrWhiteSpace(p.TrainNumber)) effNum = p.TrainNumber.Trim();
            var box = $"{effCat} {effNum}".Trim();
            if (i == 0 || box != prevBox) r.Box = box.Length > 0 ? box : null;
            prevBox = box;
            r.Cat = effCat;

            // --- skład (kolumny 7–9)
            var vals = new[] { p.Loco1, p.Loco2, p.Loco3, p.LocoLoad, p.TrainLength, p.Vmax, p.BrakePercent, p.BrakeType };
            var changed = false;
            for (var k = 0; k < 8; k++)
            {
                var v = (vals[k] ?? "").Trim();
                if (v.Length > 0 && v != (stock[k] ?? "")) { changed = true; stock[k] = v; }
            }
            r.Stock = stock.Select(s => s ?? "").ToArray();
            r.StockChanged = changed || pendingStock || i == 0;
            pendingStock = false;

            r.Lines = Math.Max(2, r.TopLines.Count + r.BottomLines.Count);
            if (r.Box != null) r.Lines = Math.Max(3, r.Lines);
            // jak we wzorze karty: między nazwą posterunku a kilometrem pół wiersza odstępu
            r.H = FirstBase + (r.Lines - 1) * LH + NameKmGap + LastBase;
            _rows.Add(r);
        }

        if (_secs.Count == 0)
        {
            // trasa bez odcinków linii – położenie wg kolejności
            for (var i = 0; i < _rows.Count; i++) _rows[i].S = i;
        }
    }

    private double LocatePoint(RoutePoint p, int index, ref int secPtr, double prevS)
    {
        if (_secs.Count == 0) return index;
        var kms = Num.AllKms(p.Km).ToList();
        if (kms.Count == 0)
        {
            if (index > 0) return prevS;
            return _secs[0].S0;
        }
        var line = (p.Line ?? "").Trim();
        const double tol = 0.0015;
        foreach (var km in kms)
        {
            for (var k = secPtr; k < _secs.Count; k++)
            {
                var sec = _secs[k];
                if (line.Length > 0 && !string.Equals(sec.Line, line, StringComparison.OrdinalIgnoreCase)) continue;
                var lo = Math.Min(sec.KmStart, sec.KmEnd) - tol; var hi = Math.Max(sec.KmStart, sec.KmEnd) + tol;
                if (km < lo || km > hi) continue;
                var s = sec.S0 + Math.Abs(km - sec.KmStart);
                if (s < prevS - 0.01 && index > 0) continue;
                secPtr = k;
                return Math.Clamp(s, sec.S0, sec.S1);
            }
        }
        _result.Warnings.Add($"Posterunek „{p.Name}” (km {p.Km}) nie leży na żadnym z kolejnych odcinków linii – sprawdź kilometraż lub numer linii.");
        return prevS;
    }

    // ================================================================= rozmieszczenie pionowe
    private void SolveVertical()
    {
        // Wiersze posterunków układane są kolejno. Zmiany prędkości/linii leżące na szlaku i → posterunek i
        // trafiają do wiersza i proporcjonalnie do kilometrażu; jeśli się nie mieszczą, wiersz jest wydłużany
        // (tak jak w kartach SKRJ, gdzie np. „Wwa Praga WPT J” obejmuje wiele zmian prędkości).
        var n = _rows.Count;
        var m = _evs.Count;
        var sFirst = _rows[0].S; var sLast = _rows[^1].S;
        foreach (var e in _evs) e.SClamped = Math.Clamp(e.S, sFirst, sLast);

        // przypisanie zdarzeń do wierszy
        var byRow = new List<int>[n];
        for (var i = 0; i < n; i++) byRow[i] = new List<int>();
        for (var j = 1; j < m; j++)
        {
            var sv = _evs[j].SClamped; var i = 0;
            while (i < n - 1 && sv > _rows[i].S + 1e-9) i++;
            byRow[i].Add(j);
        }
        if (m > 0) _evs[0].Y = 0;
        var prevEv = 0.0; var hasPrev = m > 0;
        var yTop = 0.0;
        for (var i = 0; i < n; i++)
        {
            var evs = byRow[i];
            double sA = i == 0 ? (m > 0 ? Math.Min(_evs[0].S, _rows[0].S) : _rows[0].S) : _rows[i - 1].S, sB = _rows[i].S;
            double Frac(int j)
            {
                if (sB - sA < 1e-9) return 1;
                var sv = i == 0 ? Math.Min(_evs[j].S, sB) : _evs[j].SClamped;
                return Math.Clamp((sv - sA) / (sB - sA), 0, 1);
            }
            double[] Place(double H)
            {
                var res = new double[evs.Count];
                var last = prevEv;
                for (var k = 0; k < evs.Count; k++)
                {
                    var j = evs[k];
                    var y = yTop + Frac(j) * H;
                    if (hasPrev || k > 0) y = Math.Max(y, last + _evs[j].Gap);
                    res[k] = y; last = y;
                }
                return res;
            }
            var h = _rows[i].H;
            if (evs.Count > 0)
            {
                // wysokość nominalna: tyle, by zmieściły się wszystkie zmiany z tego szlaku
                var H = Math.Max(h, evs.Sum(j => _evs[j].Gap));
                var pos = Place(H);
                for (var k = 0; k < evs.Count; k++) _evs[evs[k]].Y = pos[k];
                h = Math.Max(H, pos[^1] - yTop);
                // zmiana leżąca dokładnie przy posterunku – wyrównaj do dolnej krawędzi wiersza
                for (var k = evs.Count - 1; k >= 0; k--)
                {
                    var j = evs[k];
                    if (Frac(j) < 0.999) break;
                    var target = yTop + h;
                    var nextY = k + 1 < evs.Count ? _evs[evs[k + 1]].Y - _evs[evs[k + 1]].Gap : double.MaxValue;
                    _evs[j].Y = Math.Min(Math.Max(_evs[j].Y, target), nextY);
                }
                prevEv = _evs[evs[^1]].Y; hasPrev = true;
            }
            _rows[i].Top = yTop; _rows[i].Bottom = yTop + h;
            yTop += h;
        }
        _tableEnd = _rows[^1].Bottom;
        if (m > 1)
        {
            _tableEnd = Math.Max(_tableEnd, _evs[^1].Y);
            _evs[^1].Y = _tableEnd;
            _rows[^1].Bottom = _tableEnd;
        }
    }

    // ================================================================= łamanie stron
    private sealed class FootItem { public string Marker = "", Text = ""; }

    private List<Footnote> FootnotesFor(int r0, int r1, bool last)
    {
        var res = new List<Footnote>();
        foreach (var f in _t.Footnotes)
        {
            var mk = (f.Marker ?? "").Trim();
            if (mk.Length == 0) continue;
            var used = false; var usedAnywhere = false;
            for (var i = 0; i < _rows.Count; i++)
            {
                var p = _rows[i].P;
                var hit = (p.Abbreviations ?? "").Contains(mk) || (p.NoteTop ?? "").Contains(mk) || (p.NoteBottom ?? "").Contains(mk) || (p.Name ?? "").Contains(mk);
                if (!hit) continue;
                usedAnywhere = true;
                if (i >= r0 && i <= r1) used = true;
            }
            if (used || (last && !usedAnywhere)) res.Add(f);
        }
        return res;
    }

    private double FootHeight(int r0, int r1, bool last, Canvas measureCanvas)
    {
        var h = 0.0;
        var fn = FootnotesFor(r0, r1, last);
        foreach (var f in fn) h += measureCanvas.Wrap($"{f.Marker} {f.Text}", FFoot, xEnd - ML).Count * 10.5;
        if (last)
        {
            var rd = Lines(_t.RunningDays).ToList();
            if (rd.Count > 0) h += 12 + rd.Sum(l => measureCanvas.Wrap(l, FFoot, xEnd - ML).Count) * 10.5 + 2;
            var rm = Lines(_t.Remarks).ToList();
            if (rm.Count > 0) h += 12 + rm.Sum(l => measureCanvas.Wrap(l, FFoot, xEnd - ML).Count) * 10.5 + 2;
        }
        if (h > 0) h += 4;
        return h;
    }

    private void Paginate()
    {
        var mc = new Canvas(new PageDrawing(), _m);
        // tabela razem z odnośnikami (i na ostatniej stronie – „Kursuje:” i uwagami) kończy się najniżej
        // na wysokości dolnej krawędzi ramki strony tytułowej; gdy są odnośniki, tabela jest odpowiednio wyżej
        var availTotal = TitleY1 - BodyTop;
        var availTable = TitleY1 - BodyTop;
        var n = _rows.Count;
        var ranges = new List<(int a, int b)>();
        var start = 0;
        while (start < n)
        {
            var endRow = start;
            for (var k = start; k < n; k++)
            {
                var h = _rows[k].Bottom - _rows[start].Top;
                var f = FootHeight(start, k, k == n - 1, mc);
                if (k == start || (h <= availTable + 0.01 && h + f <= availTotal)) endRow = k; else break;
            }
            ranges.Add((start, endRow));
            start = endRow + 1;
        }

        // strony poza ostatnią: tabela dociągnięta do dołu (wyrównana z ramką strony tytułowej) –
        // nadwyżka rozdzielana na wiersze, których wysokość nie wynika ze zmian prędkości
        for (var iter = 0; iter < 6; iter++)
        {
            var changed = false;
            foreach (var (a, b) in ranges)
            {
                if (b == n - 1) continue;
                var target = Math.Min(availTable, availTotal - FootHeight(a, b, false, mc));
                var extra = target - (_rows[b].Bottom - _rows[a].Top);
                if (extra < 0.05) continue;
                var free = Enumerable.Range(a, b - a + 1).Where(i => _rows[i].Bottom - _rows[i].Top <= _rows[i].H + 0.01).ToList();
                if (free.Count == 0) free = Enumerable.Range(a, b - a + 1).ToList();
                foreach (var i in free) _rows[i].H += extra / free.Count;
                changed = true;
            }
            if (!changed) break;
            SolveVertical();
        }

        var pageNo = 2;
        foreach (var (a, b) in ranges)
            _result.Pages.Add(DrawTimetablePage(a, b, pageNo++, b == n - 1));
    }

    // ================================================================= rysowanie strony rozkładu
    private string HeaderText() =>
        !string.IsNullOrWhiteSpace(_t.HeaderOverride) ? _t.HeaderOverride.Trim() : AutoHeader(_t);

    /// <summary>Nagłówek stron szczegółowego rozkładu składany automatycznie: rodzaje pociągu, numer, (nr systemowy) Relacja od - do.</summary>
    public static string AutoHeader(Timetable t)
    {
        var cats = new List<string>();
        var eff = (t.TrainCategory ?? "").Trim();
        for (var i = 0; i < t.Points.Count; i++)
        {
            var p = t.Points[i];
            if (!string.IsNullOrWhiteSpace(p.TrainCategory)) eff = p.TrainCategory.Trim();
            if (TimetableTools.IsHiddenInCard(t, i)) continue;
            if (eff.Length > 0 && !cats.Contains(eff)) cats.Add(eff);
        }
        if (cats.Count == 0 && !string.IsNullOrWhiteSpace(t.TrainCategory)) cats.Add(t.TrainCategory.Trim());
        var s = string.Join(" ", cats);
        if (!string.IsNullOrWhiteSpace(t.TrainNumber)) s += " " + t.TrainNumber.Trim();
        if (!string.IsNullOrWhiteSpace(t.SystemNumber)) s += $" ({t.SystemNumber.Trim()})";
        var from = string.IsNullOrWhiteSpace(t.RelationFrom) ? t.Points.FirstOrDefault()?.Name?.Trim() ?? "" : t.RelationFrom.Trim();
        var to = string.IsNullOrWhiteSpace(t.RelationTo) ? t.Points.LastOrDefault()?.Name?.Trim() ?? "" : t.RelationTo.Trim();
        s += $" Relacja {from} - {to}";
        return s.Trim();
    }

    private PageDrawing DrawTimetablePage(int r0, int r1, int pageNo, bool lastPage)
    {
        var page = new PageDrawing { Width = PageW, Height = PageH, Number = pageNo };
        var c = new Canvas(page, _m);
        double Y0 = _rows[r0].Top, Y1 = _rows[r1].Bottom;
        double Py(double y) => BodyTop + (y - Y0);
        var bodyBottom = Py(Y1);

        // ---- nagłówek strony
        var head = HeaderText();
        var numW = c.W(pageNo.ToString(CultureInfo.InvariantCulture), FHeadNr);
        c.Text(ML, HeaderBaseline, head, FitFont(c, head, FHead, xEnd - ML - numW - 8));
        c.Text(xEnd, HeaderBaseline, pageNo.ToString(CultureInfo.InvariantCulture), FHeadNr, Align.Right);

        DrawColumnHeader(c);

        // ---- kolumny 1–4
        DrawLeftColumns(c, Y0, Y1, Py);

        // ---- wiersze posterunków
        for (var i = r0; i <= r1; i++) DrawRow(c, _rows[i], Py(_rows[i].Top), Py(_rows[i].Bottom), i == r0, i == 0, i == _rows.Count - 1);

        // ---- oznaczenie szlaku między kolumną 5 i 6
        DrawLineMarker(c, r0, r1, Py);

        // ---- linie pionowe i ramka
        c.VLine(xNr, TableTop, bodyBottom, Frame);
        c.VLine(xKm, TableTop, bodyBottom, Thin);
        c.VLine(xVp, TableTop, bodyBottom, Thin);
        c.VLine(xSt, TableTop, bodyBottom, Thin);
        c.VLine(xGap, BodyTop, bodyBottom, Thin);
        c.VLine(xTime, TableTop, bodyBottom, Thin);
        c.VLine(xLok, TableTop, bodyBottom, Thin);
        c.VLine(xEnd, TableTop, bodyBottom, Frame);
        c.HLine(xNr, xEnd, TableTop, Frame);
        c.HLine(xNr, xEnd, BodyTop, Thin);
        c.HLine(xNr, xEnd, bodyBottom, Frame);

        // ---- strzałka kierunku jazdy (kolumna 2|3 lub 4|5)
        DrawArrow(c, Y0, Y1, Py);

        // ---- pod tabelą: odnośniki, Kursuje, uwagi
        var fy = bodyBottom + 10.5;
        foreach (var f in FootnotesFor(r0, r1, lastPage))
            foreach (var l in c.Wrap($"{f.Marker} {f.Text}".Trim(), FFoot, xEnd - ML)) { c.Text(ML, fy, l, FFoot); fy += 10.5; }
        if (lastPage)
        {
            void Block(string title, string body)
            {
                var ls = Lines(body).ToList();
                if (ls.Count == 0) return;
                fy += 1.5;
                c.Text(ML, fy, title, FFootB, underline: true); fy += 11.5;
                foreach (var l in ls) foreach (var w in c.Wrap(l, FFoot, xEnd - ML)) { c.Text(ML, fy, w, FFoot); fy += 10.5; }
            }
            Block("Kursuje:", _t.RunningDays);
            Block("Uwagi do rozkładu:", _t.Remarks);
        }
        SimulationNote(c);
        return page;
    }

    private void DrawColumnHeader(Canvas c)
    {
        double t = TableTop, b = BodyTop, mid = (t + b) / 2;
        c.Text((xNr + xKm) / 2, t + 10.8, "Nr", FNrHead, Align.Center);
        c.Text((xNr + xKm) / 2, t + 18.6, "linii", FNrHead, Align.Center);
        c.Text((xKm + xVp) / 2, mid + 3.2, "Km", FCol, Align.Center);
        DrawVHeader(c, (xVp + xVl) / 2, mid + 3.2, "P");
        DrawVHeader(c, (xVl + xSt) / 2, mid + 3.2, "L");
        c.VLine(xVl, t, b, Thin);
        c.Text((xSt + xTime) / 2, mid + 3.2, "Stacja", FCol, Align.Center);
        c.Text((xTime + xLok) / 2, mid + 3.2, "Godzina", FCol, Align.Center);
        var third = (b - t) / 3;
        string[] lok = { "Lok I", "Lok II", "Lok III" };
        for (var k = 0; k < 3; k++) c.Text((xLok + xObc) / 2, t + third * k + third / 2 + 2.1, lok[k], FColSmall, Align.Center);
        c.HLine(xLok, xObc, t + third, Thin); c.HLine(xLok, xObc, t + 2 * third, Thin);
        c.VLine(xObc, t, b, Thin);
        var halfH = (b - t) / 2;
        c.Text((xObc + xVmax) / 2, t + halfH / 2 + 2.1, "Obc.lok", FColSmall, Align.Center);
        c.HLine(xObc, xVmax, t + halfH, Thin);
        c.Text((xObc + xVmax) / 2, t + halfH + halfH / 2 + 2.1, "Dł.Poc.", FColSmall, Align.Center);
        c.VLine(xVmax, t, b, Thin);
        string[] vm = { "Vmax", "%", "Rodz.h" };
        for (var k = 0; k < 3; k++) c.Text((xVmax + xEnd) / 2, t + third * k + third / 2 + 2.1, vm[k], FColSmall, Align.Center);
        c.HLine(xVmax, xEnd, t + third, Thin); c.HLine(xVmax, xEnd, t + 2 * third, Thin);
    }

    private static void DrawVHeader(Canvas c, double cx, double baseline, string sub)
    {
        var fs = new FontSpec(6);
        var wv = c.W("V", FCol); var ws = c.W(sub, fs);
        var x = cx - (wv + ws) / 2;
        c.Text(x, baseline, "V", FCol);
        c.Text(x + wv, baseline + 2.2, sub, fs);
    }

    private void DrawLeftColumns(Canvas c, double Y0, double Y1, Func<double, double> Py)
    {
        if (_secs.Count == 0)
        {
            c.VLine(xVl, BodyTop, Py(Y1), Thin);
            return;
        }
        bool Inside(double y) => y > Y0 + 0.01 && y < Y1 - 0.01;

        // granice odcinków + numery linii + km graniczne (kolejne odcinki tej samej linii – jedna grupa)
        foreach (var g in _secs.GroupBy(x => x.Group))
        {
            var f = g.First(); var l = g.Last();
            double a = _evs[f.EvStart].Y, b = _evs[l.EvEnd].Y;
            if (b < Y0 - 0.01 || a > Y1 + 0.01) continue;
            if (Inside(a)) c.HLine(xNr, xSt, Py(a), Thin);
            var top = Math.Max(a, Y0);
            if (Math.Min(b, Y1) - top >= 8) c.Text((xNr + xKm) / 2, Py(top) + 8.3, f.Line, FLineNr, Align.Center);
        }
        // etykiety km
        foreach (var e in _evs)
        {
            if (e.LabelBelow != null && e.Y >= Y0 - 0.01 && e.Y < Y1 - 8)
                c.Text(xKm + 2.2, Py(e.Y) + 8.3, e.LabelBelow, e.BelowBold ? FLeftKmB : FLeftKm);
            if (e.LabelAbove != null && e.Y > Y0 + 8 && e.Y <= Y1 + 0.01)
            {
                var isEnd = e.Boundary;
                if (isEnd) c.Text(xKm + 2.2, Py(e.Y) - 2.2, e.LabelAbove, e.AboveBold ? FLeftKmB : FLeftKm);
                else c.Text(xVp - 2.2, Py(e.Y) - 2.2, e.LabelAbove, e.AboveBold ? FLeftKmB : FLeftKm, Align.Right);
            }
        }
        // komórki prędkości – osobno kolumna Vp i VL, połączone dla odcinków jednotorowych
        DrawSpeedColumn(c, Y0, Y1, Py, isVp: true);
        DrawSpeedColumn(c, Y0, Y1, Py, isVp: false);

        // linia między Vp i VL tam, gdzie odcinek nie jest jednotorowy
        foreach (var s in _segs)
        {
            if (s.Single) continue;
            double a = Math.Max(_evs[s.Ev0].Y, Y0), b = Math.Min(_evs[s.Ev1].Y, Y1);
            if (b > a) c.VLine(xVl, Py(a), Py(b), Thin);
        }
    }

    private void DrawSpeedColumn(Canvas c, double Y0, double Y1, Func<double, double> Py, bool isVp)
    {
        // łączenie kolejnych segmentów o tej samej wartości w jedną komórkę
        var cells = new List<(int sec, double a, double b, string text, bool single, bool bold)>();
        foreach (var s in _segs)
        {
            var key = s.Single ? s.Vp : isVp ? s.Vp : s.Vl;
            double a = _evs[s.Ev0].Y, b = _evs[s.Ev1].Y;
            var side = _secs[s.Sec].Side;
            var bold = s.Single || (isVp ? side == TrackSide.Right : side == TrackSide.Left);
            if (cells.Count > 0 && _secs[cells[^1].sec].Group == _secs[s.Sec].Group && cells[^1].text == key && cells[^1].single == s.Single && cells[^1].bold == bold && Math.Abs(cells[^1].b - a) < 1e-6)
                cells[^1] = (cells[^1].sec, cells[^1].a, b, key, s.Single, bold);
            else cells.Add((s.Sec, a, b, key, s.Single, bold));
        }
        double x0 = isVp ? xVp : xVl, x1 = isVp ? xVl : xSt;
        foreach (var cell in cells)
        {
            if (cell.b < Y0 || cell.a > Y1) continue;
            if (cell.single && !isVp) continue; // komórka połączona rysowana raz (przy Vp)
            var xr = cell.single ? xSt : x1;
            // górna krawędź komórki (jeśli nie pokrywa się z granicą odcinka – ta rysowana osobno)
            if (cell.a > Y0 + 0.01 && cell.a < Y1 - 0.01) c.HLine(x0, xr, Py(cell.a), Thin);
            var top = Math.Max(cell.a, Y0); var bot = Math.Min(cell.b, Y1);
            if (bot - top < 9 || string.IsNullOrEmpty(cell.text)) continue;
            c.Text((x0 + xr) / 2, Py(top) + 9.6, cell.text, cell.bold ? FSpeedB : FSpeed, Align.Center);
        }
    }

    private void DrawArrow(Canvas c, double Y0, double Y1, Func<double, double> Py)
    {
        var runs = new List<(double x, double a, double b)>();
        if (_secs.Count == 0) runs.Add((xVp, Y0, Y1));
        else
            foreach (var sec in _secs)
            {
                double a = Math.Max(_evs[sec.EvStart].Y, Y0), b = Math.Min(_evs[sec.EvEnd].Y, Y1);
                if (b <= a) continue;
                var x = sec.Side == TrackSide.Right ? xVp : xSt;
                if (runs.Count > 0 && Math.Abs(runs[^1].x - x) < 0.01 && Math.Abs(runs[^1].b - a) < 0.01) runs[^1] = (x, runs[^1].a, b);
                else runs.Add((x, a, b));
            }
        foreach (var (x, a, b) in runs)
        {
            double ya = Py(a), yb = Py(b);
            const double aw = 2.4, headH = 7.5, headW = 2.9;
            c.Fill(x - aw / 2, ya, aw, Math.Max(0, yb - ya - headH + 1));
            c.Polygon(Rgb.Black, (x - headW, yb - headH), (x + headW, yb - headH), (x, yb));
        }
    }

    private void DrawLineMarker(Canvas c, int r0, int r1, Func<double, double> Py)
    {
        // obszar szlaku i → i+1: od pierwszego wiersza tekstu (nazwy) posterunku i do pierwszego wiersza
        // posterunku i+1 – tak jak w kartach SKRJ gruba linia jednotoru kończy się / zaczyna pod nazwą posterunku
        var n = _rows.Count;
        double Anchor(int i) => Math.Min(_rows[i].Top + FirstBase + MarkerBelowName, _rows[i].Bottom);
        double Y0 = _rows[r0].Top, Y1 = _rows[r1].Bottom;
        var last = Math.Max(0, n - 2);
        for (var i = 0; i <= last; i++)
        {
            double a = i == 0 ? 0 : Anchor(i), b = i == last ? _tableEnd : Anchor(i + 1);
            a = Math.Max(a, Y0); b = Math.Min(b, Y1);
            if (b <= a) continue;
            if (SingleStretch(i)) c.Fill(xGap - 0.25, Py(a), WGap + 0.5, Py(b) - Py(a));
        }
    }

    /// <summary>
    /// Szlak od wiersza i do następnego jest jednotorowy: tak oznaczony posterunek (także pominięty przystanek
    /// na tym szlaku) albo odcinek, na którym kolumny V<sub>P</sub>/V<sub>L</sub> są połączone (linia jednotorowa).
    /// </summary>
    /// <summary>Postój handlowy – rodzaj postoju zawiera „ph”.</summary>
    private static bool IsCommercialStop(RoutePoint p) =>
        (p.StopKinds ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Any(x => x.Trim().Equals("ph", StringComparison.OrdinalIgnoreCase));

    private bool SingleStretch(int i)
    {
        var r = _rows[i];
        if (r.P.TracksToNext == TrackCount.Single) return true;
        if (i + 1 < _rows.Count)
        {
            for (var k = r.Index + 1; k < _rows[i + 1].Index; k++)
                if (_t.Points[k].TracksToNext == TrackCount.Single) return true;
            var s0 = r.S; var s1 = _rows[i + 1].S;
            if (s1 - s0 > 1e-6 && SegSingleAt((s0 + s1) / 2)) return true;
        }
        return false;
    }

    private bool SegSingleAt(double s)
    {
        foreach (var g in _segs)
        {
            double a = _evs[g.Ev0].S, b = _evs[g.Ev1].S;
            if (s >= Math.Min(a, b) - 1e-9 && s <= Math.Max(a, b) + 1e-9) return g.Single;
        }
        return false;
    }

    private void DrawRow(Canvas c, Row r, double top, double bottom, bool firstOnPage, bool firstRow, bool lastRow)
    {
        var p = r.P;
        // szare tło godzin na posterunku z postojem rozkładowym (tylko rubryka godzin, bez czasów jazdy)
        if (_print.ShadeStopTimes && p.Stop && !firstRow && !lastRow && IsCommercialStop(p))
            c.Fill(xTime + Thin / 2, top + Thin / 2, WTime - Thin, bottom - top - Thin, ShadeColor);
        // linia dolna wiersza (kolumny 5–9)
        c.HLine(xSt, xEnd, bottom, Thin);

        // ---- kolumna 5
        double TopBase(int k) => top + FirstBase + k * LH;
        double BotBase(int k) => bottom - LastBase - k * LH; // k = 0 – ostatni wiersz
        var stW = WSt - 6;
        for (var k = 0; k < r.TopLines.Count; k++)
        {
            var (text, f) = r.TopLines[k];
            c.Text(xSt + 3, TopBase(k), text, FitFont(c, text, f, stW));
        }
        var nb = r.BottomLines.Count;
        for (var k = 0; k < nb; k++)
        {
            var (left, right, lf) = r.BottomLines[k];
            var y = BotBase(nb - 1 - k);
            var isNote = lf.Italic;
            var wl = 0.0;
            if (left.Length > 0) wl = c.Text(xSt + 3, y, left, isNote ? FitFont(c, left, FItal, stW) : FName);
            if (right.Length > 0)
            {
                var avail = stW - (wl > 0 ? wl + 8 : 0);
                c.Text(xGap - 3, y, right, FitFont(c, right, FName, avail), Align.Right);
            }
        }

        // ---- kolumna 6 – godziny i czasy jazdy
        var yTop = TopBase(0); var yBot = BotBase(0);
        var tx = xTime + 3.5;
        var single = _rows.Count == 1;

        void Dash(double y) => c.HLine(xTime + 4, xTravel - 4, y - 3.4, 0.8);
        void Bar(double y) => c.VLine((xTime + xTravel) / 2, y - 8.2, y - 0.3, 0.8); // przejazd – kreska na środku rubryki godzin
        void Time(string s, double y, bool bold)
        {
            if (ClockTime.TryParse(s, out var t))
                c.TextSup(tx, y, t.MainText, t.SuperText, bold ? FTimeB : FTime, 6.5, 4.0);
            else if (!string.IsNullOrWhiteSpace(s))
                c.Text(tx, y, s.Trim(), bold ? FTimeB : FTime);
        }

        string arr = p.Arrival ?? "", dep = p.Departure ?? "";
        if (firstRow)
        {
            Dash(yTop);
            if (!single) Time(dep.Length > 0 ? dep : arr, yBot, true);
            else Time(dep, yBot, true);
        }
        else if (lastRow)
        {
            Time(arr.Trim().Length > 0 ? arr : dep, yTop, false);
            Dash(yBot);
        }
        else if (p.Stop)
        {
            if (arr.Trim().Length > 0) Time(arr, yTop, true); else Bar(yTop);
            Time(dep.Trim().Length > 0 ? dep : arr, yBot, true);
        }
        else
        {
            Bar(yTop);
            Time(dep.Trim().Length > 0 ? dep : arr, yBot, false);
        }

        // czasy jazdy: licznik (normalny) u góry, mianownik (skrócony) u dołu
        var hasN = Duration.TryParse(p.TravelNormal, out var dn);
        var hasS = Duration.TryParse(p.TravelShort, out var ds);
        void Dur(Duration d, double y) => c.TextSup(xTravel + 2.4, y, d.MainText, d.SuperText, FTravel, 5.5, 3.6);
        if (hasN && hasS)
        {
            Dur(dn, yTop); Dur(ds, yBot);
            if (r.Box == null) c.HLine(xTravel, xLok, (top + bottom) / 2, Thin);
        }
        else if (hasN) Dur(dn, yBot);
        else if (hasS) Dur(ds, yBot);

        // wewnętrzna linia kolumny 6 i ramka z numerem pociągu
        var mid = (top + bottom) / 2;
        if (r.Box != null)
        {
            var bw = Math.Min(WTime + WTravel - 6, c.W(r.Box, FBox) + 6);
            var cx = (xTime + xLok) / 2;
            double lt = mid - 6.4, lb = mid + 4.6;
            c.HLine(cx - bw / 2, cx + bw / 2, lt, 0.7);
            c.HLine(cx - bw / 2, cx + bw / 2, lb, 0.7);
            c.Text(cx, mid + 2.6, r.Box, FitFont(c, r.Box, FBox, WTime + WTravel - 4), Align.Center);
            c.VLine(xTravel, top, lt, Thin);
            c.VLine(xTravel, lb, bottom, Thin);
        }
        else c.VLine(xTravel, top, bottom, Thin);

        // ---- kolumny 7–9 – skład
        var col = (r.StockChanged || firstOnPage) ? Rgb.Black : Rgb.Grey;
        var h = bottom - top; var third = h / 3; var half = h / 2;
        var s = r.Stock;
        for (var k = 0; k < 3; k++)
        {
            var cy = top + third * k + third / 2;
            if (s[k].Length > 0) c.Text((xLok + xObc) / 2, cy + 2.3, s[k], FitFont(c, s[k], FSmall, WLok - 2), Align.Center, col);
            if (s[5 + k].Length > 0) c.Text((xVmax + xEnd) / 2, cy + 2.3, s[5 + k], FitFont(c, s[5 + k], FSmall, WVmax - 2), Align.Center, col);
        }
        if (s[3].Length > 0) c.Text((xObc + xVmax) / 2, top + half / 2 + 3.2, s[3], FitFont(c, s[3], FLoad, WObc - 2), Align.Center, col);
        if (s[4].Length > 0) c.Text((xObc + xVmax) / 2, top + half + half / 2 + 3.2, s[4], FitFont(c, s[4], FLoad, WObc - 2), Align.Center, col);
        c.HLine(xLok, xObc, top + third, Thin, col); c.HLine(xLok, xObc, top + 2 * third, Thin, col);
        c.HLine(xObc, xVmax, top + half, Thin, col);
        c.HLine(xVmax, xEnd, top + third, Thin, col); c.HLine(xVmax, xEnd, top + 2 * third, Thin, col);
        c.VLine(xObc, top, bottom, Thin, col);
        c.VLine(xVmax, top, bottom, Thin, col);
    }
}
