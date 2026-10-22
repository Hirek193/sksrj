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

using Sksrj.Core.Util;

namespace Sksrj.Core.Model;

public static class TimetableTools
{
    /// <summary>
    /// Czy posterunek jest pomijany w karcie: przystanek osobowy („po”) bez postoju pociągu
    /// (pierwszy i ostatni posterunek trasy są zawsze drukowane).
    /// </summary>
    public static bool IsHiddenInCard(Timetable t, int i)
    {
        if (!t.HidePassedHalts || i <= 0 || i >= t.Points.Count - 1) return false;
        var p = t.Points[i];
        return !p.Stop && string.Equals((p.TypeSuffix ?? "").Trim().TrimEnd('.'), "po", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rodzaj posterunku dla domyślnych czasów postoju.</summary>
    public enum StopCategory { Halt, Station, Junction }

    public static StopCategory CategoryOf(RoutePoint p) =>
        p.Uppercase ? StopCategory.Junction
        : string.Equals((p.TypeSuffix ?? "").Trim().TrimEnd('.'), "po", StringComparison.OrdinalIgnoreCase) ? StopCategory.Halt
        : StopCategory.Station;

    public static string CategoryText(StopCategory c) => c switch
    {
        StopCategory.Halt => "przystanek osobowy",
        StopCategory.Junction => "stacja węzłowa",
        _ => "stacja",
    };

    /// <summary>Domyślny czas postoju karty dla rodzaju posterunku (puste = minimum wg § 22 ust. 11).</summary>
    public static string DefaultDwell(Timetable t, StopCategory c) => (c switch
    {
        StopCategory.Halt => t.DwellHalt,
        StopCategory.Junction => t.DwellJunction,
        _ => t.DwellStation,
    } ?? "").Trim();

    /// <summary>Aktualizuje znacznik <see cref="RoutePoint.HiddenInCard"/> wszystkich posterunków karty.</summary>
    public static void UpdateHiddenFlags(Timetable t)
    {
        for (var i = 0; i < t.Points.Count; i++)
        {
            var h = IsHiddenInCard(t, i);
            if (t.Points[i].HiddenInCard != h) t.Points[i].HiddenInCard = h;
        }
    }

    /// <summary>
    /// Przelicza godziny: od odjazdu z pierwszego posterunku, dodając czasy jazdy (normalne, a gdy brak – skrócone)
    /// oraz czasy postoju na posterunkach z postojem. Zwraca liczbę zmienionych posterunków.
    /// </summary>
    public static int RecalculateTimes(Timetable t, bool useShortTimes = false)
    {
        var pts = t.Points;
        if (pts.Count < 2) return 0;
        if (!ClockTime.TryParse(pts[0].Departure, out var cur)) return 0;
        var changed = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            var p = pts[i];
            var src = useShortTimes ? (string.IsNullOrWhiteSpace(p.TravelShort) ? p.TravelNormal : p.TravelShort)
                                    : (string.IsNullOrWhiteSpace(p.TravelNormal) ? p.TravelShort : p.TravelNormal);
            if (!Duration.TryParse(src, out var run)) continue;
            var arr = cur + run;
            var last = i == pts.Count - 1;
            string newArr, newDep;
            if (last) { newArr = arr.ToString(); newDep = ""; }
            else if (p.Stop)
            {
                Duration.TryParse(p.Dwell, out var dw);
                if (dw.Tenths == 0 && ClockTime.TryParse(p.Arrival, out var oa) && ClockTime.TryParse(p.Departure, out var od)) dw = od - oa;
                newArr = arr.ToString(); newDep = (arr + dw).ToString();
            }
            else { newArr = ""; newDep = arr.ToString(); }
            if (p.Arrival != newArr || p.Departure != newDep) changed++;
            p.Arrival = newArr; p.Departure = newDep;
            cur = ClockTime.TryParse(last ? newArr : newDep, out var c2) ? c2 : arr;
        }
        return changed;
    }

    /// <summary>Wyznacza czasy jazdy (normalne) z różnicy godzin. Zwraca liczbę uzupełnionych posterunków.</summary>
    public static int RecalculateTravelTimes(Timetable t)
    {
        var pts = t.Points;
        var changed = 0;
        var prevIdx = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            if (IsHiddenInCard(t, i)) continue; // czas jazdy liczony do następnego drukowanego posterunku
            var prev = pts[prevIdx];
            prevIdx = i;
            var prevStr = string.IsNullOrWhiteSpace(prev.Departure) ? prev.Arrival : prev.Departure;
            var p = pts[i];
            var curStr = (p.Stop || i == pts.Count - 1) && !string.IsNullOrWhiteSpace(p.Arrival) ? p.Arrival : (string.IsNullOrWhiteSpace(p.Departure) ? p.Arrival : p.Departure);
            if (!ClockTime.TryParse(prevStr, out var a) || !ClockTime.TryParse(curStr, out var b)) continue;
            var d = (b - a).ToString();
            if (p.TravelNormal != d) { p.TravelNormal = d; changed++; }
            if (p.Stop && ClockTime.TryParse(p.Arrival, out var pa) && ClockTime.TryParse(p.Departure, out var pd))
                p.Dwell = (pd - pa).ToString();
        }
        return changed;
    }

    /// <summary>
    /// Numer systemowy tak jak w skryptach Pythona MaSzyny:
    /// <c>str((int(hash(state["scenario"][:5])) % 299) + 1)</c> – hash napisu z Pythona 2 (32-bitowy long w Windows),
    /// napis w kodowaniu Windows-1250, wycinek pierwszych 5 bajtów. Wynik 1–299.
    /// </summary>
    public static int SystemNumberFromScenario(string? scenario)
    {
        System.Text.Encoding enc;
        try { System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); enc = System.Text.Encoding.GetEncoding(1250); }
        catch { enc = System.Text.Encoding.Latin1; }
        var bytes = enc.GetBytes(scenario ?? "").Take(5).ToArray();
        var h = Python2StringHash(bytes);
        var m = h % 299; if (m < 0) m += 299; // modulo jak w Pythonie (wynik nieujemny)
        return m + 1;
    }

    /// <summary>Funkcja skrótu napisu z CPythona 2.7 (bez losowania, long 32-bitowy).</summary>
    public static int Python2StringHash(byte[] s)
    {
        if (s.Length == 0) return 0;
        unchecked
        {
            var x = s[0] << 7;
            foreach (var b in s) x = (1000003 * x) ^ b;
            x ^= s.Length;
            if (x == -1) x = -2;
            return x;
        }
    }

    /// <summary>Zmienia numer linii na schemacie i we wszystkich miejscach, które się do niego odwołują.</summary>
    public static int RenameLine(TimetableProject p, SceneryLine line, string newNumber)
    {
        var old = (line.Number ?? "").Trim(); newNumber = (newNumber ?? "").Trim();
        if (newNumber.Length == 0 || old == newNumber) return 0;
        bool Eq(string? x) => string.Equals((x ?? "").Trim(), old, StringComparison.OrdinalIgnoreCase);
        var n = 0;
        foreach (var t in p.Timetables)
        {
            foreach (var sec in t.Sections) if (Eq(sec.LineNumber)) { sec.LineNumber = newNumber; n++; }
            foreach (var pt in t.Points) if (Eq(pt.Line)) { pt.Line = newNumber; n++; }
        }
        foreach (var r in p.Restrictions) if (Eq(r.Line)) { r.Line = newNumber; n++; }
        foreach (var node in p.Scenery.Nodes) foreach (var l in node.SpeedLimits) if (Eq(l.Line)) { l.Line = newNumber; n++; }
        line.Number = newNumber;
        return n;
    }

    /// <summary>Kopia posterunku (bez parametrów zmiany składu i numeru).</summary>
    public static RoutePoint ClonePoint(RoutePoint p) => ProjectSerializer.Clone(p);
}
