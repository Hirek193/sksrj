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

namespace Sksrj.Core.Engine;

public sealed record SegmentResult(int Index, string From, string To, double Km, double A, double B, double C, double Normal, double Short, double AvgSpeed, bool StopAtEnd, double Dwell)
{
    /// <summary>Indeks posterunku początkowego (poprzedni drukowany posterunek).</summary>
    public int FromIndex { get; init; } = Index - 1;
}

public sealed record MatchedRestriction(SpeedRestriction R, double S0, double S1, string Line, double Speed, double LossMin);

public sealed record ProfileMark(double S, string Name, bool Stop);

public sealed class RunTimeResult
{
    public List<SegmentResult> Segments { get; } = new();
    public List<MatchedRestriction> Restrictions { get; } = new();
    public List<ProfileMark> Marks { get; } = new();
    /// <summary>Czas techniczny [min] od początku trasy do każdego posterunku – do godzin przejazdu przez pomijane przystanki.</summary>
    public double[] PointTime { get; set; } = Array.Empty<double>();
    public List<string> Warnings { get; } = new();
    /// <summary>Próbki profilu: s [km], prędkość jazdy [km/h], prędkość dopuszczalna bez i z ostrzeżeniami doraźnymi [km/h].</summary>
    public double[] S { get; set; } = Array.Empty<double>();
    public double[] V { get; set; } = Array.Empty<double>();
    public double[] LimitBase { get; set; } = Array.Empty<double>();
    public double[] LimitAll { get; set; } = Array.Empty<double>();
    public double ReserveRate { get; set; }
    public string ReserveRule { get; set; } = "";
    public double MinDwell { get; set; }
    public string DwellRule { get; set; } = "";
    public double TrainVmax { get; set; }
    public double TrainLength { get; set; }
    public double TotalKm => Segments.Sum(x => x.Km);
    public double TotalA => Segments.Sum(x => x.A);
    public double TotalB => Segments.Sum(x => x.B);
    public double TotalC => Segments.Sum(x => x.C);
    public double TotalD => Segments.Sum(x => x.Dwell);
    public double TotalNormal => Segments.Sum(x => x.Normal);
    public double TotalShort => Segments.Sum(x => x.Short);
    public bool Ok => Segments.Count > 0;
}

/// <summary>
/// Obliczenia trakcyjne czasu jazdy wg Ir-11 § 21:
/// A – techniczny czas jazdy (z rozruchem i hamowaniem, ograniczeniami prędkości i długością pociągu),
/// B – rezerwa eksploatacyjna wg rodzaju pociągu [min/100 km], C – strata na ostrzeżeniach doraźnych, D – postoje.
/// </summary>
public static class RunTimeCalculator
{
    private const double Ds = 0.01; // krok obliczeń 10 m
    private const double G = 9.81;

    private static double P(string? s, double def) =>
        double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : def;

    // ---------------------------------------------------------------- normy Ir-11
    /// <summary>Rezerwa eksploatacyjna B wg § 21 ust. 6 pkt 2.</summary>
    public static (double rate, string rule) ReserveFor(string? category)
    {
        var c = (category ?? "").Trim().ToUpperInvariant();
        if (c.Length == 0) return (5, "pozostałe pociągi pasażerskie – 5 min/100 km");
        if (c == "EIJ") return (2.5, "pociągi EIJ – 2,5 min/100 km (§ 21 ust. 6 pkt 2a)");
        var p2 = c.Length >= 2 ? c[..2] : c;
        if (p2 is "EC" or "EN" or "EI" or "MH" or "MM" or "MP" or "RP") return (3, "EC, EN, EI, MH, MM, MP, RP – 3 min/100 km (pkt 2b)");
        if (p2 is "TC" or "TD") return (3, "towarowe intermodalne TC, TD – 3 min/100 km (pkt 2c)");
        if (c[0] == 'P') return (5, "pociągi niehandlowe – 5 min/100 km (pkt 2f)");
        if (c[0] == 'L') return (3, "lokomotywy luzem – 3 min/100 km (pkt 2g)");
        if (c[0] is 'T' or 'Z') return (7, "towarowe i utrzymaniowo-naprawcze – 7 min/100 km (pkt 2e)");
        return (5, "pozostałe pociągi pasażerskie – 5 min/100 km (pkt 2d)");
    }

    /// <summary>Minimalny czas postoju handlowego wg § 22 ust. 11.</summary>
    public static (double min, string rule) MinDwellFor(string? category)
    {
        var c = (category ?? "").Trim().ToUpperInvariant();
        if (c.Length >= 1 && c[0] is 'T' or 'L' or 'Z') return (1.0, "postój techniczny pociągu towarowego / luzem – przyjęto 1 min");
        if (c.StartsWith("MH")) return (2.0, "pociągi z wagonami sypialnymi lub z kuszetkami – 2 min (§ 22 ust. 11 pkt 3)");
        if (c.Length >= 3 && c[2] is 'J' or 'M') return (0.3, "zespoły trakcyjne i autobusy szynowe – 0,3 min (§ 22 ust. 11 pkt 1)");
        if (c.StartsWith("MP") || c.StartsWith("MM")) return (1.0, "pociągi międzywojewódzkie pośpieszne z wagonów – 1 min (pkt 2)");
        return (0.5, "pociągi pasażerskie – 0,5 min (§ 22 ust. 11 pkt 1)");
    }

    public static bool IsPassenger(string? category)
    {
        var c = (category ?? "").Trim().ToUpperInvariant();
        return c.Length > 0 && c[0] is 'E' or 'M' or 'R' or 'P';
    }

    public static bool IsFreight(string? category)
    {
        var c = (category ?? "").Trim().ToUpperInvariant();
        return c.Length > 0 && c[0] == 'T';
    }

    // ---------------------------------------------------------------- ograniczenia na trasie
    public static List<MatchedRestriction> MatchRestrictions(Timetable t, RouteGeometry g, IEnumerable<SpeedRestriction> all)
    {
        var res = new List<MatchedRestriction>();
        foreach (var r in all)
        {
            if (!r.Active) continue;
            if (r.AppliesTo == TrainFilter.Passenger && !IsPassenger(t.TrainCategory)) continue;
            if (r.AppliesTo == TrainFilter.Freight && !IsFreight(t.TrainCategory)) continue;
            if (!Num.TryParseKm(r.KmFrom, out var a)) continue;
            if (!Num.TryParseKm(r.KmTo, out var b)) b = a;
            var v = P(r.Speed, 0);
            if (v <= 0) continue;
            foreach (var (s0, s1, sec) in g.MapRange(r.Line, a, b, r.Track))
                res.Add(new MatchedRestriction(r, s0, s1, sec.Line, v, 0));
        }
        return res.OrderBy(x => x.S0).ToList();
    }

    // ---------------------------------------------------------------- obliczenie
    public static RunTimeResult Calculate(Timetable t, IEnumerable<SpeedRestriction> restrictions)
    {
        var r = new RunTimeResult();
        var g = RouteGeometry.Build(t);
        r.Warnings.AddRange(g.Warnings);
        if (g.Sections.Count == 0 || t.Points.Count < 2)
        {
            r.Warnings.Add("Do obliczeń potrzebne są odcinki linii (zakładka Trasa) i co najmniej dwa posterunki.");
            return r;
        }
        var set = t.RunCalc;
        var preset = TractionPresets.Find(set.Preset);
        var a0 = P(set.StartAcceleration, preset?.A0 ?? 0.5);
        var pw = P(set.PowerPerTon, preset?.PowerPerTon ?? 5);
        var br = Math.Max(0.1, P(set.Braking, preset?.Braking ?? 0.5));
        var ra = P(set.ResistanceA, 2); var rc = P(set.ResistanceC, 0.0003);
        var first = t.Points[0];
        var vmax = P(set.TrainVmax, P(first.Vmax, preset?.Vmax ?? 120));
        var len = P(set.TrainLength, P(first.TrainLength, preset?.Length ?? 100));
        r.TrainVmax = vmax; r.TrainLength = len;
        (r.ReserveRate, r.ReserveRule) = ReserveFor(t.TrainCategory);
        if (!string.IsNullOrWhiteSpace(set.ReservePer100)) { r.ReserveRate = P(set.ReservePer100, r.ReserveRate); r.ReserveRule = "wartość podana ręcznie"; }
        (r.MinDwell, r.DwellRule) = MinDwellFor(t.TrainCategory);
        if (!string.IsNullOrWhiteSpace(set.DefaultDwell)) { r.MinDwell = P(set.DefaultDwell, r.MinDwell); r.DwellRule = "wartość podana ręcznie"; }

        var total = g.Length;
        var n = Math.Max(2, (int)Math.Ceiling(total / Ds) + 1);
        var sArr = new double[n];
        for (var i = 0; i < n; i++) sArr[i] = Math.Min(total, i * Ds);

        var matched = MatchRestrictions(t, g, restrictions);
        double[] Limits(Func<MatchedRestriction, bool> include)
        {
            var lim = new double[n];
            for (var i = 0; i < n; i++) lim[i] = Math.Min(vmax, g.SpeedAt(sArr[i]));
            foreach (var m in matched.Where(include))
            {
                var i0 = Math.Max(0, (int)Math.Floor(m.S0 / Ds)); var i1 = Math.Min(n - 1, (int)Math.Ceiling(m.S1 / Ds));
                for (var i = i0; i <= i1; i++) lim[i] = Math.Min(lim[i], m.Speed);
            }
            // pociąg o długości L może przyspieszyć dopiero, gdy cały minie ograniczenie
            var w = (int)Math.Ceiling(len / 1000.0 / Ds);
            if (w <= 0) return lim;
            var eff = new double[n];
            var dq = new LinkedList<int>();
            for (var i = 0; i < n; i++)
            {
                while (dq.Count > 0 && lim[dq.Last!.Value] >= lim[i]) dq.RemoveLast();
                dq.AddLast(i);
                while (dq.First!.Value < i - w) dq.RemoveFirst();
                eff[i] = lim[dq.First.Value];
            }
            return eff;
        }

        // posterunki z zatrzymaniem
        var idx = new int[t.Points.Count];
        var stop = new bool[t.Points.Count];
        for (var k = 0; k < t.Points.Count; k++)
        {
            idx[k] = Math.Clamp((int)Math.Round(g.PointS[k] / Ds), 0, n - 1);
            stop[k] = k == 0 || k == t.Points.Count - 1 || t.Points[k].Stop;
        }
        var stopIdx = new HashSet<int>();
        for (var k = 0; k < stop.Length; k++) if (stop[k]) stopIdx.Add(idx[k]);

        double Acc(double v)
        {
            var tr = v < 0.5 ? a0 : Math.Min(a0, pw / v);
            var kmh = v * 3.6;
            var res = (ra + rc * kmh * kmh) * G / 1000.0;
            return tr - res;
        }

        (double[] v, double[] T) Run(double[] lim)
        {
            var dsM = Ds * 1000;
            var v = new double[n];
            for (var i = 0; i < n; i++) v[i] = lim[i] / 3.6;
            // jazda do przodu – ograniczenie przyspieszenia
            var cur = 0.0;
            for (var i = 0; i < n; i++)
            {
                if (i == 0 || stopIdx.Contains(i)) { v[i] = 0; cur = 0; continue; }
                var a = Acc(cur);
                var nv = a > 0 ? Math.Sqrt(cur * cur + 2 * a * dsM) : cur;
                v[i] = Math.Min(v[i], nv);
                cur = v[i];
            }
            v[n - 1] = 0;
            // hamowanie – przebieg wstecz
            for (var i = n - 2; i >= 0; i--)
            {
                if (stopIdx.Contains(i)) { v[i] = 0; continue; }
                v[i] = Math.Min(v[i], Math.Sqrt(v[i + 1] * v[i + 1] + 2 * br * dsM));
            }
            var T = new double[n];
            for (var i = 1; i < n; i++)
            {
                var d = (sArr[i] - sArr[i - 1]) * 1000;
                var sum = v[i] + v[i - 1];
                T[i] = T[i - 1] + (d <= 0 ? 0 : sum > 1e-6 ? 2 * d / sum : Math.Sqrt(2 * d / Math.Max(0.05, a0)));
            }
            return (v, T);
        }

        var limBase = Limits(m => m.R.Kind == RestrictionKind.Permanent);
        var limAll = Limits(m => m.R.Kind == RestrictionKind.Permanent || set.IncludeTemporary);
        var (vBase, tBase) = Run(limBase);
        var (vAll, tAll) = Run(limAll);

        // strata czasu na poszczególnych ograniczeniach (względem jazdy bez ostrzeżeń)
        var limNone = Limits(_ => false);
        var (_, tNone) = Run(limNone);
        for (var i = 0; i < matched.Count; i++)
        {
            var m = matched[i];
            var (_, tOne) = Run(Limits(x => ReferenceEquals(x, m)));
            matched[i] = m with { LossMin = Math.Max(0, (tOne[n - 1] - tNone[n - 1]) / 60.0) };
        }
        r.Restrictions.AddRange(matched);

        // wyniki na odcinkach między drukowanymi posterunkami (przystanki pomijane w karcie są wliczane do szlaku)
        r.PointTime = t.Points.Select((_, k) => tAll[idx[k]] / 60.0).ToArray();
        var kp = 0;
        for (var k = 1; k < t.Points.Count; k++)
        {
            if (Model.TimetableTools.IsHiddenInCard(t, k)) continue;
            var k0 = kp; kp = k;
            var km = Math.Max(0, g.PointS[k] - g.PointS[k0]);
            var a = Math.Max(0, (tBase[idx[k]] - tBase[idx[k0]]) / 60.0);
            var aAll = Math.Max(0, (tAll[idx[k]] - tAll[idx[k0]]) / 60.0);
            var c = set.IncludeTemporary ? Math.Max(0, aAll - a) : 0;
            var b = r.ReserveRate * km / 100.0;
            var normal = Ceil1(a + b + c);
            var shrt = Ceil1(a + c);
            var avg = a > 0 ? km / (a / 60.0) : 0;
            double dwell = 0;
            if (stop[k] && k < t.Points.Count - 1)
                dwell = Duration.TryParse(t.Points[k].Dwell, out var dw) && dw.Tenths > 0 ? dw.Tenths / 10.0 : r.MinDwell;
            r.Segments.Add(new SegmentResult(k, Name(t.Points[k0]), Name(t.Points[k]), km, a, b, c, normal, shrt, avg, stop[k], dwell) { FromIndex = k0 });
        }
        for (var k = 0; k < t.Points.Count; k++) r.Marks.Add(new ProfileMark(g.PointS[k], Name(t.Points[k]), stop[k]));

        // próbki do wykresu
        var step = Math.Max(1, n / 1600);
        var cnt = (n + step - 1) / step;
        r.S = new double[cnt]; r.V = new double[cnt]; r.LimitBase = new double[cnt]; r.LimitAll = new double[cnt];
        for (int i = 0, j = 0; i < n && j < cnt; i += step, j++)
        {
            // w obrębie kroku – minimum prędkości (by nie gubić zatrzymań)
            var vmin = double.MaxValue; var lb = double.MaxValue; var la = double.MaxValue;
            for (var q = i; q < Math.Min(n, i + step); q++) { vmin = Math.Min(vmin, set.IncludeTemporary ? vAll[q] : vBase[q]); lb = Math.Min(lb, g.SpeedAt(sArr[q])); la = Math.Min(la, limAll[q]); }
            r.S[j] = sArr[i]; r.V[j] = vmin * 3.6; r.LimitBase[j] = Math.Min(lb, vmax); r.LimitAll[j] = la;
        }
        return r;
    }

    private static string Name(RoutePoint p) => (p.Uppercase ? (p.Name ?? "").ToUpperInvariant() : p.Name ?? "").Trim();

    private static double Ceil1(double x) => Math.Max(0.1, Math.Ceiling(x * 10 - 1e-6) / 10.0);

    private static string F1(double x) => new Duration((int)Math.Round(x * 10)).ToString();

    /// <summary>Wpisuje wyniki do karty: czasy jazdy normalne i skrócone oraz (opcjonalnie) godziny z zaokrągleniami wg § 21 ust. 9.</summary>
    public static int Apply(Timetable t, RunTimeResult r)
    {
        if (!r.Ok) return 0;
        var set = t.RunCalc;
        foreach (var seg in r.Segments)
        {
            var p = t.Points[seg.Index];
            for (var h = seg.FromIndex + 1; h < seg.Index; h++) { t.Points[h].TravelNormal = ""; t.Points[h].TravelShort = ""; }
            if (seg.Km < 0.0005) { p.TravelNormal = ""; p.TravelShort = ""; continue; }
            p.TravelNormal = F1(seg.Normal);
            p.TravelShort = F1(seg.Short);
        }
        if (!set.UpdateClockTimes) return r.Segments.Count;

        var pts = t.Points;
        if (!ClockTime.TryParse(pts[0].Departure, out var cur)) cur = new ClockTime(8 * 600);
        if (set.RoundIr11) cur = CeilTo(cur, 10);
        pts[0].Departure = cur.ToString(); pts[0].Arrival = "";
        foreach (var seg in r.Segments)
        {
            var p = pts[seg.Index];
            var last = seg.Index == pts.Count - 1;
            var arr = cur + new Duration((int)Math.Round(seg.Normal * 10));
            // godziny przejazdu przez przystanki pomijane w karcie – proporcjonalnie do czasu technicznego
            if (seg.Index - seg.FromIndex > 1 && r.PointTime.Length == pts.Count)
            {
                var t0 = r.PointTime[seg.FromIndex]; var span = r.PointTime[seg.Index] - t0;
                for (var h = seg.FromIndex + 1; h < seg.Index; h++)
                {
                    var f = span > 1e-9 ? Math.Clamp((r.PointTime[h] - t0) / span, 0, 1) : 0;
                    pts[h].Arrival = "";
                    pts[h].Departure = (cur + new Duration((int)Math.Round(seg.Normal * f * 10))).ToString();
                }
            }
            if (last)
            {
                if (set.RoundIr11) arr = CeilTo(arr, 10);
                p.Arrival = arr.ToString(); p.Departure = "";
                break;
            }
            if (p.Stop)
            {
                if (set.RoundIr11 && p.Uppercase) arr = CeilTo(arr, 5);
                var dw = new Duration((int)Math.Round(seg.Dwell * 10));
                if (string.IsNullOrWhiteSpace(p.Dwell)) p.Dwell = dw.ToString();
                var dep = arr + dw;
                if (set.RoundIr11 && p.Uppercase) dep = CeilTo(dep, 5);
                p.Arrival = arr.ToString(); p.Departure = dep.ToString();
                cur = dep;
            }
            else
            {
                p.Arrival = ""; p.Departure = arr.ToString();
                cur = arr;
            }
        }
        return r.Segments.Count;
    }

    private static ClockTime CeilTo(ClockTime t, int tenths) => new((t.Tenths + tenths - 1) / tenths * tenths);
}
