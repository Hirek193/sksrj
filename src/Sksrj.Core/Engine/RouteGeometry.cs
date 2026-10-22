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

using Sksrj.Core.Model;
using Sksrj.Core.Util;

namespace Sksrj.Core.Engine;

/// <summary>
/// Geometria trasy pociągu wzdłuż drogi s [km] liczonej od początku pierwszego odcinka linii:
/// odcinki linii, prędkości dopuszczalne po torze pociągu i położenie posterunków.
/// </summary>
public sealed class RouteGeometry
{
    public sealed record Section(int Index, string Line, double KmStart, double KmEnd, double S0, double S1, TrackSide Side)
    {
        public int Dir => Math.Sign(KmEnd - KmStart) == 0 ? 1 : Math.Sign(KmEnd - KmStart);
        public double KmAt(double s) => KmStart + Dir * (s - S0);
    }

    public sealed record SpeedSegment(int Section, double S0, double S1, double TrainSpeed, bool Single);

    public List<Section> Sections { get; } = new();
    public List<SpeedSegment> Segments { get; } = new();
    public List<double> PointS { get; } = new();
    public List<string> Warnings { get; } = new();
    public double Length => Sections.Count == 0 ? 0 : Sections[^1].S1;

    public static RouteGeometry Build(Timetable t)
    {
        var g = new RouteGeometry();
        double s = 0;
        for (var i = 0; i < t.Sections.Count; i++)
        {
            var ls = t.Sections[i];
            Num.TryParseKm(ls.KmStart, out var k0);
            if (!Num.TryParseKm(ls.KmEnd, out var k1)) k1 = k0;
            var sec = new Section(i, (ls.LineNumber ?? "").Trim(), k0, k1, s, s + Math.Abs(k1 - k0), ls.Track);
            g.Sections.Add(sec);
            var changes = ls.Changes.ToList();
            if (changes.Count == 0) changes.Add(new SpeedChange { Km = ls.KmStart, Vp = "60", Vl = "60" });
            var pts = new List<(double s, double v, bool single)>();
            for (var c = 0; c < changes.Count; c++)
            {
                var ch = changes[c];
                double sc = sec.S0;
                if (c > 0 && Num.TryParseKm(ch.Km, out var km)) sc = Math.Clamp(sec.S0 + Math.Abs(km - k0), sec.S0, sec.S1);
                var vp = ParseV(ch.Vp); var vl = ParseV(ch.SingleTrack ? ch.Vp : ch.Vl);
                if (ch.SingleTrack && vp <= 0) vp = ParseV(ch.Vl);
                var v = ch.SingleTrack ? vp : sec.Side == TrackSide.Right ? vp : vl;
                if (v <= 0) v = vp > 0 ? vp : vl;
                pts.Add((sc, v, ch.SingleTrack));
            }
            for (var c = 0; c < pts.Count; c++)
            {
                var a = pts[c].s; var b = c + 1 < pts.Count ? pts[c + 1].s : sec.S1;
                if (b - a > 1e-9) g.Segments.Add(new SpeedSegment(i, a, b, pts[c].v, pts[c].single));
            }
            s = sec.S1;
        }

        // położenie posterunków (jak w składzie karty)
        int ptr = 0; double prev = 0;
        for (var i = 0; i < t.Points.Count; i++)
        {
            var p = t.Points[i];
            var ps = g.Locate(p, i, ref ptr, prev);
            if (i > 0 && ps < prev) ps = prev;
            g.PointS.Add(ps); prev = ps;
        }
        return g;
    }

    private static double ParseV(string? v) =>
        double.TryParse((v ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;

    private double Locate(RoutePoint p, int index, ref int ptr, double prevS)
    {
        if (Sections.Count == 0) return index;
        var kms = Num.AllKms(p.Km).ToList();
        if (kms.Count == 0) return index > 0 ? prevS : Sections[0].S0;
        var line = (p.Line ?? "").Trim();
        foreach (var km in kms)
            for (var k = ptr; k < Sections.Count; k++)
            {
                var sec = Sections[k];
                if (line.Length > 0 && !string.Equals(sec.Line, line, StringComparison.OrdinalIgnoreCase)) continue;
                var lo = Math.Min(sec.KmStart, sec.KmEnd) - 0.0015; var hi = Math.Max(sec.KmStart, sec.KmEnd) + 0.0015;
                if (km < lo || km > hi) continue;
                var s = Math.Clamp(sec.S0 + Math.Abs(km - sec.KmStart), sec.S0, sec.S1);
                if (s < prevS - 0.01 && index > 0) continue;
                ptr = k;
                return s;
            }
        Warnings.Add($"Posterunek „{p.Name}” (km {p.Km}) nie leży na żadnym z kolejnych odcinków linii.");
        return prevS;
    }

    /// <summary>Prędkość dopuszczalna po torze pociągu w punkcie s.</summary>
    public double SpeedAt(double s)
    {
        foreach (var seg in Segments)
            if (s >= seg.S0 - 1e-9 && s < seg.S1 - 1e-9) return seg.TrainSpeed;
        return Segments.Count > 0 ? Segments[^1].TrainSpeed : 60;
    }

    public bool SingleAt(double s)
    {
        foreach (var seg in Segments)
            if (s >= seg.S0 - 1e-9 && s < seg.S1 - 1e-9) return seg.Single;
        return false;
    }

    /// <summary>Zakresy s, w których linia <paramref name="line"/> obejmuje kilometry [kmA, kmB] na torze pociągu.</summary>
    public IEnumerable<(double S0, double S1, Section Sec)> MapRange(string line, double kmA, double kmB, RestrictionTrack track)
    {
        var lo = Math.Min(kmA, kmB); var hi = Math.Max(kmA, kmB);
        foreach (var sec in Sections)
        {
            if (!string.Equals(sec.Line, (line ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            var slo = Math.Min(sec.KmStart, sec.KmEnd); var shi = Math.Max(sec.KmStart, sec.KmEnd);
            var a = Math.Max(lo, slo); var b = Math.Min(hi, shi);
            if (b < a - 1e-9) continue;
            var sa = sec.S0 + Math.Abs(a - sec.KmStart); var sb = sec.S0 + Math.Abs(b - sec.KmStart);
            var s0 = Math.Min(sa, sb); var s1 = Math.Max(sa, sb);
            if (track != RestrictionTrack.Both)
            {
                // tor nr 1 = prawy w kierunku rosnącego km
                var trainTrack = (sec.Dir > 0) == (sec.Side == TrackSide.Right) ? RestrictionTrack.Track1 : RestrictionTrack.Track2;
                var single = SingleAt((s0 + s1) / 2);
                if (!single && trainTrack != track) continue;
            }
            yield return (s0, s1, sec);
        }
    }
}
