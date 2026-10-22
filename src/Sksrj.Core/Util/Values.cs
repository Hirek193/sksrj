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
using System.Text.RegularExpressions;

namespace Sksrj.Core.Util;

/// <summary>
/// Godzina rozkładowa z dokładnością do 0,1 minuty (tak jak w kartach SKRJ: 05:48⁵ = 5:48 i 30 s).
/// </summary>
public readonly record struct ClockTime(int Tenths)
{
    public int TotalMinutes => Tenths / 10;
    public int Fraction => ((Tenths % 10) + 10) % 10;

    /// <summary>Część główna „HH:MM” (doba modulo 24 h).</summary>
    public string MainText
    {
        get
        {
            var m = ((TotalMinutes % 1440) + 1440) % 1440;
            return $"{m / 60:00}:{m % 60:00}";
        }
    }

    /// <summary>Cyfra dziesiątych części minuty drukowana w indeksie górnym ("" gdy 0).</summary>
    public string SuperText => Fraction == 0 ? "" : Fraction.ToString(CultureInfo.InvariantCulture);

    public override string ToString() => Fraction == 0 ? MainText : $"{MainText}.{Fraction}";

    private static readonly Regex Rx = new(@"^\s*(\d{1,2})\s*[:.]\s*(\d{1,2})(?:\s*(?:([.,])\s*(\d)|:\s*(\d{1,2})))?\s*$", RegexOptions.Compiled);

    /// <summary>Akceptuje: 6:28, 06:28, 06:28.5, 06:28,5, 06:28:30.</summary>
    public static bool TryParse(string? text, out ClockTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = NormalizeSuperscripts(text);
        var m = Rx.Match(text);
        if (!m.Success) return false;
        var h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mi = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        if (mi > 59 || h > 47) return false;
        var t = 0;
        if (m.Groups[4].Success) t = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
        else if (m.Groups[5].Success)
        {
            var sec = int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture);
            if (sec > 59) return false;
            t = (int)Math.Round(sec / 6.0, MidpointRounding.AwayFromZero);
        }
        value = new ClockTime((h * 60 + mi) * 10 + t);
        return true;
    }

    public static ClockTime operator +(ClockTime a, Duration d) => new(a.Tenths + d.Tenths);
    public static Duration operator -(ClockTime a, ClockTime b)
    {
        var d = a.Tenths - b.Tenths;
        if (d < 0) d += 14400; // przejście przez północ
        return new Duration(d);
    }

    internal static string NormalizeSuperscripts(string s)
    {
        // pozwala wkleić „05:48⁵” wprost z karty
        const string sup = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        if (s.IndexOfAny(sup.ToCharArray()) < 0) return s;
        // wstaw kropkę przed cyfrą z indeksu górnego
        var res = new System.Text.StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var k = sup.IndexOf(s[i]);
            if (k >= 0) { res.Append('.'); res.Append((char)('0' + k)); }
            else res.Append(s[i]);
        }
        return res.ToString();
    }
}

/// <summary>Czas jazdy / postoju z dokładnością do 0,1 minuty (np. 3⁵ = 3,5 min).</summary>
public readonly record struct Duration(int Tenths)
{
    public string MainText => (Tenths / 10).ToString(CultureInfo.InvariantCulture);
    public string SuperText => Tenths % 10 == 0 ? "" : (Tenths % 10).ToString(CultureInfo.InvariantCulture);
    public override string ToString() => Tenths % 10 == 0 ? MainText : $"{MainText}.{Tenths % 10}";

    public static bool TryParse(string? text, out Duration value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = ClockTime.NormalizeSuperscripts(text.Trim()).Replace(',', '.');
        if (text.Contains(':'))
        {
            // m:ss
            var p = text.Split(':');
            if (p.Length == 2 && int.TryParse(p[0], out var mm) && int.TryParse(p[1], out var ss) && ss < 60)
            {
                value = new Duration(mm * 10 + (int)Math.Round(ss / 6.0, MidpointRounding.AwayFromZero));
                return true;
            }
            return false;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < 0) return false;
        value = new Duration((int)Math.Round(d * 10, MidpointRounding.AwayFromZero));
        return true;
    }
}

public static class Num
{
    private static readonly Regex FirstNumber = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    /// <summary>Parsuje kilometr („2.234”, „2,234”, „-0.146”).</summary>
    public static bool TryParseKm(string? text, out double km)
    {
        km = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out km);
    }

    /// <summary>Pierwsza liczba w tekście (np. z „8.317/1.558” → 8.317).</summary>
    public static bool TryParseFirstKm(string? text, out double km)
    {
        km = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var m = FirstNumber.Match(text);
        return m.Success && TryParseKm(m.Value, out km);
    }

    /// <summary>Wszystkie liczby w tekście (np. z „8.317/1.558” → 8.317, 1.558).</summary>
    public static IEnumerable<double> AllKms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (Match m in FirstNumber.Matches(text))
            if (TryParseKm(m.Value, out var v)) yield return v;
    }

    /// <summary>Format kilometrażu w kolumnie 2 (przecinek, 3 miejsca): „-0,030”.</summary>
    public static string KmComma(double km) => km.ToString("0.000", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Format kilometrażu w kolumnie 5 (kropka, 3 miejsca): „2.234”.</summary>
    public static string KmDot(double km) => km.ToString("0.000", CultureInfo.InvariantCulture);
}
