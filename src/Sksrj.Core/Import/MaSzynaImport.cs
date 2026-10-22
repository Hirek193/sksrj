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
using System.Text;
using System.Text.RegularExpressions;
using Sksrj.Core.Dictionaries;
using Sksrj.Core.Engine;
using Sksrj.Core.Model;
using Sksrj.Core.Util;

namespace Sksrj.Core.Import;

/// <summary>Posterunek z tekstowego rozkładu jazdy symulatora MaSzyna.</summary>
public sealed class MaSzynaStation
{
    public string Name = "";
    public string Suffix = "";
    public double Km;
    public string KmText = "";
    public string? Speed;
    public string Abbreviations = "";
    public bool SingleTrack;
    public string Arrival = "";
    public string Departure = "";
    public string TravelTime = "";
    public bool Stop => Arrival.Length > 0 && Departure.Length > 0;
}

/// <summary>Rozkład jazdy w formacie tekstowym MaSzyny (plik *.txt z katalogu rozkładów scenerii).</summary>
public sealed class MaSzynaTimetable
{
    public string TrainId = "";
    public string Category = "";
    public string Number = "";
    public string From = "";
    public string To = "";
    public string BrakePercent = "";
    public string Loco = "";
    public string LocoLoad = "";
    public List<MaSzynaStation> Stations { get; } = new();
    public List<string> Warnings { get; } = new();
    public double LengthKm => Stations.Count < 2 ? 0 : Math.Abs(Stations[^1].Km - Stations[0].Km);
}

public sealed class MaSzynaImportOptions
{
    public string LineNumber = "";
    public string LineName = "";
    /// <summary>Użyj istniejących posterunków schematu o tej samej nazwie (łączenie kilku rozkładów na tej samej linii).</summary>
    public bool MergeByName = true;
    /// <summary>Utwórz też kartę pociągu z godzinami, czasami jazdy i składem z pliku.</summary>
    public bool CreateTimetable = true;
}

/// <summary>Import rozkładu jazdy MaSzyny do schematu scenerii (i opcjonalnie do karty pociągu).</summary>
public static class MaSzynaImport
{
    private static readonly Regex Row = new(@"^\[(?<km>[^|]*)\|(?<spd>[^|]*)\|(?<mid>.*)\|(?<tt>[^|\]]*)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex Mid = new(@"^(?<text>.*?)\s*(?<tr>\d)\s+(?<time>\d{1,2}[.:]\d{2}|\|)?\s*$", RegexOptions.Compiled);
    private static readonly Regex Head = new(@"^\[(?<l>[^|]*)\|(?<v>[^\]]*)\]\s*$", RegexOptions.Compiled);

    public static string ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1250).GetString(bytes);
        }
    }

    public static MaSzynaTimetable Parse(string text)
    {
        var res = new MaSzynaTimetable();
        var header = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<string>();
        MaSzynaStation? cur = null;
        var lineNo = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            lineNo++;
            var line = raw.TrimEnd();
            if (!line.StartsWith('[')) continue;
            var m = Row.Match(line);
            if (m.Success && (Regex.IsMatch(m.Groups["km"].Value, @"^\s*-?\d+([.,]\d+)?\s*$") || (cur != null && m.Groups["km"].Value.Trim().Length == 0 && !line.Contains("___") && !m.Groups["mid"].Value.Contains("---"))))
            {
                var mid = Mid.Match(m.Groups["mid"].Value);
                if (!mid.Success) { if (!m.Groups["mid"].Value.Contains("---")) res.Warnings.Add($"Wiersz {lineNo}: nie rozpoznano zawartości."); continue; }
                var kmText = m.Groups["km"].Value.Trim();
                var spd = m.Groups["spd"].Value.Trim().Trim('_').Trim();
                var txt = mid.Groups["text"].Value.Trim();
                var time = mid.Groups["time"].Value.Trim();
                if (kmText.Length > 0)
                {
                    cur = new MaSzynaStation { KmText = kmText, Km = double.Parse(kmText.Replace(',', '.'), CultureInfo.InvariantCulture) };
                    (cur.Name, cur.Suffix) = SplitName(txt);
                    cur.SingleTrack = mid.Groups["tr"].Value == "1";
                    if (time.Length > 0 && time != "|") cur.Arrival = Clock(time);
                    cur.TravelTime = m.Groups["tt"].Value.Trim();
                    res.Stations.Add(cur);
                }
                else if (cur != null)
                {
                    if (txt.Length > 0) cur.Abbreviations = string.Join(", ", txt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    if (time.Length > 0 && time != "|") cur.Departure = Clock(time);
                }
                if (spd.Length > 0 && int.TryParse(spd, out _) && cur != null) cur.Speed = spd;
                continue;
            }
            if (res.Stations.Count > 0) continue;
            var h = Head.Match(line);
            if (!h.Success) continue;
            var label = h.Groups["l"].Value.Trim().Trim('_').Trim();
            var value = h.Groups["v"].Value.Trim().Trim('_').Trim();
            if (label.Length == 0) { if (value.Length > 0) pending.Add(value); continue; }
            var vals = pending.ToList(); if (value.Length > 0) vals.Add(value);
            pending.Clear();
            header[label] = vals;
        }

        string H(string key) => header.FirstOrDefault(kv => kv.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase)).Value is { Count: > 0 } v ? string.Join(" ", v) : "";
        List<string> HL(string key) => header.FirstOrDefault(kv => kv.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase)).Value ?? new();

        res.TrainId = H("Rodzaj i numer");
        var cm = Regex.Match(res.TrainId, @"^(?<c>[A-Za-z]+)\s*(?<n>.*)$");
        res.Category = cm.Success ? cm.Groups["c"].Value.ToUpperInvariant() : "";
        res.Number = cm.Success ? cm.Groups["n"].Value.Trim() : res.TrainId;
        var rel = HL("Relacja");
        res.From = rel.Count > 0 ? Name(rel[0]) : res.Stations.FirstOrDefault()?.Name ?? "";
        res.To = rel.Count > 1 ? Name(rel[^1]) : res.Stations.LastOrDefault()?.Name ?? "";
        res.BrakePercent = Regex.Match(H("Wymagany %"), @"\d+").Value;
        var loco = H("Seria i obci").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        res.Loco = loco.Length > 0 ? loco[0] : "";
        res.LocoLoad = loco.Length > 1 ? loco[1] : "";

        // pierwszy posterunek – tylko odjazd, ostatni – tylko przyjazd
        if (res.Stations.Count > 0)
        {
            var f = res.Stations[0];
            if (f.Departure.Length == 0) f.Departure = f.Arrival;
            f.Arrival = "";
            var l = res.Stations[^1];
            if (l.Arrival.Length == 0) l.Arrival = l.Departure;
            l.Departure = "";
        }
        if (res.Stations.Count < 2) res.Warnings.Add("W pliku nie znaleziono co najmniej dwóch posterunków.");
        return res;
    }

    private static string Clock(string t) => t.Replace('.', ':');

    private static string Name(string s) => s.Replace('_', ' ').Trim();

    /// <summary>„Zerniki_Male_po” → („Zerniki Male”, „po”) – rodzaj posterunku z ostatniego członu nazwy.</summary>
    private static (string name, string suffix) SplitName(string raw)
    {
        var parts = raw.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 1 && Ir11.PointTypes.Any(p => p.Code.Equals(parts[^1], StringComparison.OrdinalIgnoreCase)))
        {
            var suf = parts[^1].ToLowerInvariant();
            parts.RemoveAt(parts.Count - 1);
            return (string.Join(" ", parts), suf);
        }
        return (string.Join(" ", parts), "");
    }

    /// <summary>
    /// Dodaje posterunki i odcinki rozkładu do schematu. Zwraca identyfikatory posterunków w kolejności jazdy.
    /// </summary>
    public static List<string> AddToScenery(Scenery sc, MaSzynaTimetable tt, MaSzynaImportOptions o, out SceneryLine line, out int createdNodes)
    {
        createdNodes = 0;
        var number = string.IsNullOrWhiteSpace(o.LineNumber) ? "?" : o.LineNumber.Trim();
        var ln = sc.Lines.FirstOrDefault(l => string.Equals(l.Number?.Trim(), number, StringComparison.OrdinalIgnoreCase));
        if (ln == null)
        {
            var used = sc.Lines.Select(l => l.Color).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] palette = { "#E30613", "#00692F", "#1037FF", "#D0661A", "#7B1FA2", "#00838F", "#C2185B", "#5D4037", "#F9A825", "#8A8A8A" };
            ln = new SceneryLine
            {
                Number = number, Name = string.IsNullOrWhiteSpace(o.LineName) ? $"{tt.From} – {tt.To}" : o.LineName.Trim(),
                Color = palette.FirstOrDefault(c => !used.Contains(c)) ?? palette[sc.Lines.Count % palette.Length],
                DefaultVp = tt.Stations.FirstOrDefault(s => s.Speed != null)?.Speed ?? "100",
            };
            ln.DefaultVl = ln.DefaultVp;
            sc.Lines.Add(ln);
        }
        line = ln;

        // położenie: poziomo, pod istniejącym rysunkiem; odległości wg kilometrażu
        double x0 = 60, y = 80;
        if (sc.Nodes.Count > 0) { y = sc.Nodes.Max(n => n.Y) + sc.GridSize * 8; x0 = sc.Nodes.Min(n => n.X); }
        var g = sc.GridSize > 1 ? sc.GridSize : 13;
        double Snap(double v) => Math.Round(v / g) * g;
        var unitsPerKm = 1.0 / (sc.KmPerUnit > 1e-6 ? sc.KmPerUnit : 0.03);
        var ids = new List<string>();
        SceneryNode? prev = null; MaSzynaStation? prevSt = null;
        var x = x0;
        string? speed = null;
        foreach (var st in tt.Stations)
        {
            var display = st.Name;
            SceneryNode? node = o.MergeByName
                ? sc.Nodes.FirstOrDefault(n => string.Equals(n.Name, display, StringComparison.OrdinalIgnoreCase) && string.Equals(n.EffectiveTypeSuffix, st.Suffix, StringComparison.OrdinalIgnoreCase))
                : null;
            if (prevSt != null) x += Math.Max(g * 4, Math.Abs(st.Km - prevSt.Km) * unitsPerKm);
            if (node == null)
            {
                var isHalt = st.Suffix == "po";
                var traffic = st.Suffix.Length > 0 && !isHalt;
                node = new SceneryNode
                {
                    Name = display, TypeSuffix = isHalt ? "" : st.Suffix,
                    Kind = isHalt || traffic ? NodeKind.Stop : NodeKind.Station,
                    Passenger = isHalt || !traffic, Bold = !isHalt && !traffic,
                    Abbreviations = st.Abbreviations, X = Snap(x), Y = Snap(y),
                    LabelSide = ids.Count % 2 == 0 ? LabelSide.Top : LabelSide.Bottom,
                    DefaultStopKinds = isHalt || !traffic ? "ph" : "",
                };
                sc.Nodes.Add(node); createdNodes++;
            }
            else { x = node.X; y = node.Y; }
            if (st.Speed != null) speed = st.Speed;
            if (prev != null && prevSt != null)
            {
                var exists = sc.Edges.Any(e => e.LineId == ln.Id && ((e.NodeA == prev.Id && e.NodeB == node.Id) || (e.NodeA == node.Id && e.NodeB == prev.Id)));
                if (!exists)
                {
                    var e = new SceneryEdge
                    {
                        LineId = ln.Id, NodeA = prev.Id, NodeB = node.Id, KmA = Num.KmDot(prevSt.Km), KmB = Num.KmDot(st.Km),
                        Tracks = prevSt.SingleTrack ? TrackCount.Single : TrackCount.Double, Style = EdgeStyle.Direct,
                    };
                    var v = prevSt.Speed ?? speed ?? ln.DefaultVp;
                    e.Changes.Add(new SpeedChange { Km = e.KmA, Vp = v, Vl = e.Tracks == TrackCount.Single ? "" : v, SingleTrack = e.Tracks == TrackCount.Single });
                    sc.Edges.Add(e);
                }
            }
            if (st.Speed == null) st.Speed = speed;
            ids.Add(node.Id);
            prev = node; prevSt = st;
        }
        return ids;
    }

    /// <summary>Tworzy kartę pociągu: trasa po posterunkach schematu oraz godziny, czasy jazdy i skład z pliku.</summary>
    public static Timetable CreateTimetable(Scenery sc, MaSzynaTimetable tt, List<string> nodeIds, List<string> warnings)
    {
        var t = Samples.NewEmpty();
        t.TrainCategory = tt.Category.Length > 0 ? tt.Category : "ROJ";
        t.TrainNumber = tt.Number;
        t.RelationFrom = tt.From; t.RelationTo = tt.To;
        t.Route.StopPolicy = StopPolicy.Waypoints;
        foreach (var id in nodeIds) t.Route.Waypoints.Add(id);
        var route = SceneryRouter.FindRoute(sc, nodeIds);
        warnings.AddRange(SceneryRouter.ApplyToTimetable(sc, t, route));
        // dane z pliku – po identyfikatorach posterunków
        var byNode = new Dictionary<string, Queue<MaSzynaStation>>();
        for (var i = 0; i < nodeIds.Count && i < tt.Stations.Count; i++)
        {
            if (!byNode.TryGetValue(nodeIds[i], out var q)) byNode[nodeIds[i]] = q = new();
            q.Enqueue(tt.Stations[i]);
        }
        var passenger = RunTimeCalculator.IsPassenger(t.TrainCategory);
        for (var i = 0; i < t.Points.Count; i++)
        {
            var p = t.Points[i];
            if (!byNode.TryGetValue(p.NodeId, out var q) || q.Count == 0) continue;
            var st = q.Dequeue();
            var first = i == 0; var last = i == t.Points.Count - 1;
            p.Stop = first || last || st.Stop;
            p.Arrival = st.Arrival; p.Departure = st.Departure;
            p.StopKinds = p.Stop && passenger && sc.Node(p.NodeId)?.Passenger == true ? "ph" : "";
            p.Dwell = st.Stop && !first && !last && ClockTime.TryParse(st.Arrival, out var a) && ClockTime.TryParse(st.Departure, out var d) ? (d - a).ToString() : "";
            if (!first && st.TravelTime.Length > 0) { p.TravelNormal = st.TravelTime.Replace(',', '.'); p.TravelShort = ""; }
            p.ManualStop = true;
        }
        if (t.Points.Count > 0)
        {
            var f = t.Points[0];
            f.Loco1 = tt.Loco; f.LocoLoad = tt.LocoLoad; f.BrakePercent = tt.BrakePercent;
            if (string.IsNullOrWhiteSpace(f.BrakeType)) f.BrakeType = RunTimeCalculator.IsFreight(t.TrainCategory) ? "G" : "P";
        }
        return t;
    }
}
