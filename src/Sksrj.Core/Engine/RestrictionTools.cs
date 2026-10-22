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
using Sksrj.Core.Layout;
using Sksrj.Core.Model;
using Sksrj.Core.Util;

namespace Sksrj.Core.Engine;

public sealed class RestrictionGeneratorOptions
{
    public int Count { get; set; } = 5;
    /// <summary>Numery linii rozdzielone przecinkami (puste = wszystkie znane linie).</summary>
    public string Lines { get; set; } = "";
    public string Speeds { get; set; } = "20, 30, 40, 50, 60, 70";
    public double MinLengthM { get; set; } = 100;
    public double MaxLengthM { get; set; } = 900;
    public int? Seed { get; set; }
    public string ValidFrom { get; set; } = "";
    public string ValidTo { get; set; } = "";
}

public static class RestrictionTools
{
    public static readonly string[] Reasons =
    {
        "zły stan nawierzchni", "usterka rozjazdu", "roboty torowe", "osłabienie podtorza",
        "usterka urządzeń zabezpieczenia przejazdu", "pęknięcie szyny", "wichrowatość toru",
        "uszkodzenie sieci trakcyjnej", "roboty na obiekcie inżynieryjnym", "usterka urządzeń srk",
    };

    /// <summary>
    /// Wszystkie ograniczenia obowiązujące w projekcie: wykaz ostrzeżeń oraz ograniczenia posterunków schematu
    /// oznaczone jako pozycje wykazu ostrzeżeń stałych.
    /// </summary>
    public static List<SpeedRestriction> Effective(TimetableProject p)
    {
        var list = p.Restrictions.ToList();
        foreach (var n in p.Scenery.Nodes)
            foreach (var l in n.SpeedLimits)
                if (l.Active && l.Mode == NodeLimitMode.PermanentWarning)
                    list.Add(new SpeedRestriction
                    {
                        Kind = RestrictionKind.Permanent, Line = l.Line, KmFrom = l.KmFrom, KmTo = l.KmTo, Speed = l.Speed,
                        Track = l.Track, Reason = l.Reason, Notes = "posterunek " + n.Name,
                    });
        return list;
    }

    /// <summary>Znane zakresy kilometrażu linii – ze schematu i z kart w projekcie.</summary>
    public static Dictionary<string, List<(double A, double B, bool Single)>> LineRanges(TimetableProject p)
    {
        var res = new Dictionary<string, List<(double, double, bool)>>(StringComparer.OrdinalIgnoreCase);
        void Add(string line, double a, double b, bool single)
        {
            line = (line ?? "").Trim();
            if (line.Length == 0 || Math.Abs(b - a) < 0.05) return;
            if (!res.TryGetValue(line, out var l)) res[line] = l = new();
            l.Add((Math.Min(a, b), Math.Max(a, b), single));
        }
        foreach (var e in p.Scenery.Edges)
            if (Num.TryParseKm(e.KmA, out var a) && Num.TryParseKm(e.KmB, out var b))
                Add(p.Scenery.Line(e.LineId)?.Number ?? "", a, b, e.Tracks == TrackCount.Single);
        if (res.Count == 0)
            foreach (var t in p.Timetables)
                foreach (var s in t.Sections)
                    if (Num.TryParseKm(s.KmStart, out var a) && Num.TryParseKm(s.KmEnd, out var b))
                        Add(s.LineNumber, a, b, s.Changes.All(c => c.SingleTrack));
        return res;
    }

    /// <summary>Losuje ostrzeżenia doraźne na znanych liniach (do urozmaicenia jazdy w symulatorze).</summary>
    public static List<SpeedRestriction> Generate(TimetableProject p, RestrictionGeneratorOptions o)
    {
        var rnd = o.Seed.HasValue ? new Random(o.Seed.Value) : new Random();
        var ranges = LineRanges(p);
        var wanted = o.Lines.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pool = ranges.Where(kv => wanted.Count == 0 || wanted.Contains(kv.Key)).SelectMany(kv => kv.Value.Select(v => (line: kv.Key, v.A, v.B, v.Single))).ToList();
        var speeds = o.Speeds.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).ToList();
        if (speeds.Count == 0) speeds.Add(40);
        var res = new List<SpeedRestriction>();
        if (pool.Count == 0) return res;
        var total = pool.Sum(x => x.B - x.A);
        for (var i = 0; i < o.Count; i++)
        {
            // losowanie odcinka proporcjonalnie do długości
            var pick = rnd.NextDouble() * total; var seg = pool[0];
            foreach (var x in pool) { if (pick <= x.B - x.A) { seg = x; break; } pick -= x.B - x.A; }
            var len = (o.MinLengthM + rnd.NextDouble() * Math.Max(0, o.MaxLengthM - o.MinLengthM)) / 1000.0;
            len = Math.Min(len, seg.B - seg.A);
            var a = seg.A + rnd.NextDouble() * Math.Max(0, seg.B - seg.A - len);
            a = Math.Round(a * 1000 / 50) * 50 / 1000.0; // pełne 50 m
            var b = Math.Round((a + len) * 1000 / 50) * 50 / 1000.0;
            if (b <= a) b = a + 0.1;
            res.Add(new SpeedRestriction
            {
                Kind = RestrictionKind.Temporary,
                Line = seg.line,
                KmFrom = Num.KmDot(a), KmTo = Num.KmDot(b),
                Track = seg.Single ? RestrictionTrack.Both : (RestrictionTrack)rnd.Next(0, 3),
                Speed = speeds[rnd.Next(speeds.Count)].ToString(CultureInfo.InvariantCulture),
                Reason = Reasons[rnd.Next(Reasons.Length)],
                ValidFrom = o.ValidFrom, ValidTo = o.ValidTo,
                Generated = true,
            });
        }
        return res;
    }

    public static string TrackText(RestrictionTrack t) => t switch { RestrictionTrack.Track1 => "1", RestrictionTrack.Track2 => "2", _ => "1, 2" };

    /// <summary>Strony „Wykaz ostrzeżeń na trasie pociągu” w formacie karty (A5).</summary>
    public static List<PageDrawing> ListPages(Timetable t, IReadOnlyList<MatchedRestriction> list, ITextMeasurer m, int firstPageNo, PrintSettings print)
    {
        var pages = new List<PageDrawing>();
        if (list.Count == 0) return pages;
        const double W = TimetableLayout.PageW, H = TimetableLayout.PageH, ML = 30, MR = 26, MT = 26, MB = 26;
        var xEnd = W - MR;
        // kolumny: Lp, linia, tor, od km, do km, V, rodzaj, przyczyna
        double[] cw = { 18, 30, 26, 44, 44, 26, 44, 0 };
        cw[7] = xEnd - ML - cw.Take(7).Sum();
        var xs = new double[cw.Length + 1]; xs[0] = ML;
        for (var i = 0; i < cw.Length; i++) xs[i + 1] = xs[i] + cw[i];
        var fH = new FontSpec(9, true); var fC = new FontSpec(8.5); var fCb = new FontSpec(8.5, true); var fHead = new FontSpec(7.5, true);
        string[] heads = { "Lp.", "Nr linii", "Tor", "od km", "do km", "V", "Rodzaj", "Przyczyna / uwagi" };
        var rowIdx = 0; var pageNo = firstPageNo;
        var title = $"{t.TrainCategory} {t.TrainNumber}".Trim();
        while (rowIdx < list.Count)
        {
            var page = new PageDrawing { Width = W, Height = H, Number = pageNo };
            var c = new Canvas(page, m);
            c.Text(ML, MT + 8, $"{title} – wykaz ostrzeżeń na trasie pociągu", fH);
            c.Text(xEnd, MT + 8, pageNo.ToString(CultureInfo.InvariantCulture), new FontSpec(9.5, true), Align.Right);
            var y = MT + 14;
            if (pageNo == firstPageNo)
            {
                c.Text(ML, y + 10, $"Relacja {t.RelationFrom} - {t.RelationTo}. Ostrzeżenia stałe wliczono do czasu jazdy, doraźne – do rezerwy C.", new FontSpec(7.5, false, true));
                y += 16;
            }
            const double hh = 18;
            c.HLine(ML, xEnd, y, 0.75); c.HLine(ML, xEnd, y + hh, 0.5);
            for (var i = 0; i < heads.Length; i++) c.Text((xs[i] + xs[i + 1]) / 2, y + 11.5, heads[i], fHead, Align.Center);
            var top = y; y += hh;
            while (rowIdx < list.Count)
            {
                var r = list[rowIdx];
                var reason = string.Join("; ", new[] { r.R.Reason, r.R.Notes, ValidText(r.R) }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var lines = c.Wrap(reason, fC, cw[7] - 6);
                var rh = Math.Max(14, 4 + lines.Count * 10.5);
                if (y + rh > H - MB - (print.SimulationNote ? 8 : 0)) break;
                var b = y + 10.5;
                c.Text((xs[0] + xs[1]) / 2, b, (rowIdx + 1).ToString(CultureInfo.InvariantCulture), fC, Align.Center);
                c.Text((xs[1] + xs[2]) / 2, b, r.Line, fC, Align.Center);
                c.Text((xs[2] + xs[3]) / 2, b, TrackText(r.R.Track), fC, Align.Center);
                c.Text(xs[4] - 3, b, Num.TryParseKm(r.R.KmFrom, out var a) ? Num.KmComma(a) : r.R.KmFrom, fC, Align.Right);
                c.Text(xs[5] - 3, b, Num.TryParseKm(r.R.KmTo, out var bb) ? Num.KmComma(bb) : r.R.KmTo, fC, Align.Right);
                c.Text((xs[5] + xs[6]) / 2, b, r.R.Speed, fCb, Align.Center);
                c.Text((xs[6] + xs[7]) / 2, b, r.R.KindShort, fC, Align.Center);
                for (var i = 0; i < lines.Count; i++) c.Text(xs[7] + 3, b + i * 10.5, lines[i], fC);
                y += rh;
                c.HLine(ML, xEnd, y, 0.5);
                rowIdx++;
            }
            for (var i = 0; i < xs.Length; i++) c.VLine(xs[i], top, y, i == 0 || i == xs.Length - 1 ? 0.75 : 0.5);
            c.HLine(ML, xEnd, y, 0.75);
            if (print.SimulationNote && !string.IsNullOrWhiteSpace(print.SimulationNoteText))
                c.Text(ML, H - 9, print.SimulationNoteText.Trim(), new FontSpec(6, false, true), Align.Left, Rgb.Grey);
            pages.Add(page);
            pageNo++;
        }
        return pages;
    }

    private static string ValidText(SpeedRestriction r)
    {
        var a = (r.ValidFrom ?? "").Trim(); var b = (r.ValidTo ?? "").Trim();
        if (a.Length == 0 && b.Length == 0) return "";
        return a.Length > 0 && b.Length > 0 ? $"ważne {a} – {b}" : a.Length > 0 ? $"ważne od {a}" : $"ważne do {b}";
    }
}
