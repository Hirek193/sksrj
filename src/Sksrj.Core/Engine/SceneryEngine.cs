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

public readonly record struct Pt(double X, double Y)
{
    public static Pt operator +(Pt a, Pt b) => new(a.X + b.X, a.Y + b.Y);
    public static Pt operator -(Pt a, Pt b) => new(a.X - b.X, a.Y - b.Y);
    public double Len => Math.Sqrt(X * X + Y * Y);
}

/// <summary>Geometria rysunku schematu (styl mapy metra: odcinki pod kątem 0/45/90°).</summary>
public static class SchematicGeometry
{
    public static List<Pt> EdgePath(Scenery sc, SceneryEdge e)
    {
        var a = sc.Node(e.NodeA); var b = sc.Node(e.NodeB);
        var res = new List<Pt>();
        if (a == null || b == null) return res;
        var stops = new List<Pt> { new(a.X, a.Y) };
        stops.AddRange(e.Via.Select(v => new Pt(v.X, v.Y)));
        stops.Add(new Pt(b.X, b.Y));
        res.Add(stops[0]);
        for (var i = 1; i < stops.Count; i++)
        {
            foreach (var p in Route(stops[i - 1], stops[i], e.Style).Skip(1)) res.Add(p);
        }
        return res;
    }

    /// <summary>Punkty odcinka P→Q wg stylu (bez powtórzeń).</summary>
    public static List<Pt> Route(Pt p, Pt q, EdgeStyle style)
    {
        var dx = q.X - p.X; var dy = q.Y - p.Y;
        var adx = Math.Abs(dx); var ady = Math.Abs(dy);
        var list = new List<Pt> { p };
        if (adx < 0.5 || ady < 0.5 || style == EdgeStyle.Direct || Math.Abs(adx - ady) < 0.5) { list.Add(q); return list; }
        switch (style)
        {
            case EdgeStyle.HorizontalFirst: list.Add(new Pt(q.X, p.Y)); break;
            case EdgeStyle.VerticalFirst: list.Add(new Pt(p.X, q.Y)); break;
            default:
            {
                var d = Math.Min(adx, ady);
                var diag = new Pt(Math.Sign(dx) * d, Math.Sign(dy) * d);
                if (style == EdgeStyle.DiagonalFirst) list.Add(p + diag);
                else list.Add(q - diag);
                break;
            }
        }
        list.Add(q);
        return list;
    }

    public static double PolyLength(IReadOnlyList<Pt> pts)
    {
        double l = 0;
        for (var i = 1; i < pts.Count; i++) l += (pts[i] - pts[i - 1]).Len;
        return l;
    }

    public static double DistanceToPolyline(Pt p, IReadOnlyList<Pt> pts)
    {
        var best = double.MaxValue;
        for (var i = 1; i < pts.Count; i++) best = Math.Min(best, DistToSegment(p, pts[i - 1], pts[i]));
        return best;
    }

    private static double DistToSegment(Pt p, Pt a, Pt b)
    {
        var ab = b - a; var ap = p - a;
        var l2 = ab.X * ab.X + ab.Y * ab.Y;
        var t = l2 < 1e-9 ? 0 : Math.Clamp((ap.X * ab.X + ap.Y * ab.Y) / l2, 0, 1);
        return (p - new Pt(a.X + t * ab.X, a.Y + t * ab.Y)).Len;
    }

    /// <summary>Kierunek (znormalizowany) linii przy posterunku – do rysowania kresek przystanków.</summary>
    public static Pt DirectionAt(Scenery sc, SceneryNode n)
    {
        foreach (var e in sc.Edges)
        {
            if (e.NodeA != n.Id && e.NodeB != n.Id) continue;
            var path = EdgePath(sc, e);
            if (path.Count < 2) continue;
            var d = e.NodeA == n.Id ? path[1] - path[0] : path[^1] - path[^2];
            var l = d.Len;
            if (l > 1e-6) return new Pt(d.X / l, d.Y / l);
        }
        return new Pt(1, 0);
    }
}

/// <summary>Wyszukiwanie trasy na schemacie i budowa karty rozkładu jazdy.</summary>
public static class SceneryRouter
{
    public sealed record Hop(SceneryEdge Edge, bool Forward);

    public sealed class RouteResult
    {
        public List<string> Nodes { get; } = new();
        public List<Hop> Hops { get; } = new();
        public List<string> Warnings { get; } = new();
        public double Km { get; set; }
        public bool Ok => Nodes.Count >= 2 && Hops.Count == Nodes.Count - 1;
    }

    public static double EdgeKm(Scenery sc, SceneryEdge e)
    {
        if (Num.TryParseKm(e.KmA, out var a) && Num.TryParseKm(e.KmB, out var b) && Math.Abs(b - a) > 1e-6) return Math.Abs(b - a);
        return SchematicGeometry.PolyLength(SchematicGeometry.EdgePath(sc, e)) * sc.KmPerUnit;
    }

    /// <summary>Najkrótsza trasa przez kolejne punkty (Dijkstra; odcinki „omijane” mają wysoki koszt).</summary>
    public static RouteResult FindRoute(Scenery sc, IReadOnlyList<string> waypoints)
    {
        var res = new RouteResult();
        if (waypoints.Count == 0) return res;
        res.Nodes.Add(waypoints[0]);
        for (var w = 1; w < waypoints.Count; w++)
        {
            var leg = ShortestPath(sc, waypoints[w - 1], waypoints[w], res.Hops.LastOrDefault()?.Edge);
            if (leg == null)
            {
                res.Warnings.Add($"Brak połączenia między „{sc.Node(waypoints[w - 1])?.Name}” a „{sc.Node(waypoints[w])?.Name}”.");
                break;
            }
            foreach (var (node, hop) in leg) { res.Nodes.Add(node); res.Hops.Add(hop); res.Km += EdgeKm(sc, hop.Edge); }
        }
        return res;
    }

    /// <summary>Kara [km] za zmianę linii – trasa preferuje jazdę tą samą linią kolejową.</summary>
    public const double LineChangePenalty = 3.0;

    private static List<(string node, Hop hop)>? ShortestPath(Scenery sc, string from, string to, SceneryEdge? arrivedBy)
    {
        if (from == to) return new();
        var adj = new Dictionary<string, List<Hop>>();
        foreach (var e in sc.Edges)
        {
            if (!adj.TryGetValue(e.NodeA, out var la)) adj[e.NodeA] = la = new();
            if (!adj.TryGetValue(e.NodeB, out var lb)) adj[e.NodeB] = lb = new();
            la.Add(new Hop(e, true)); lb.Add(new Hop(e, false));
        }
        // stan = (posterunek, linia, którą dojechano)
        var start = (from, arrivedBy?.LineId ?? "");
        var dist = new Dictionary<(string, string), double> { [start] = 0 };
        var prev = new Dictionary<(string, string), ((string, string) st, Hop hop)>();
        var pq = new PriorityQueue<(string node, string line), double>();
        pq.Enqueue(start, 0);
        (string, string)? best = null;
        while (pq.TryDequeue(out var st, out var du))
        {
            if (du > dist.GetValueOrDefault(st, double.MaxValue) + 1e-9) continue;
            if (st.node == to) { best = st; break; }
            if (!adj.TryGetValue(st.node, out var hops)) continue;
            foreach (var h in hops)
            {
                if (st.node == from && arrivedBy != null && ReferenceEquals(h.Edge, arrivedBy)) continue; // bez zawracania
                if (prev.TryGetValue(st, out var pv) && ReferenceEquals(pv.hop.Edge, h.Edge)) continue;
                var v = h.Forward ? h.Edge.NodeB : h.Edge.NodeA;
                var cost = EdgeKm(sc, h.Edge) * (h.Edge.AvoidInRouting ? 50 : 1) + 0.001;
                if (st.line.Length > 0 && st.line != h.Edge.LineId) cost += LineChangePenalty;
                var ns = (v, h.Edge.LineId);
                var nd = du + cost;
                if (nd < dist.GetValueOrDefault(ns, double.MaxValue) - 1e-12)
                {
                    dist[ns] = nd; prev[ns] = (st, h); pq.Enqueue(ns, nd);
                }
            }
        }
        if (best == null) return null;
        var path = new List<(string, Hop)>();
        var cur = best.Value;
        while (cur != start) { var (p, h) = prev[cur]; path.Add((cur.Item1, h)); cur = p; }
        path.Reverse();
        return path;
    }

    // ---------------------------------------------------------------- automatyczny kilometraż
    /// <summary>Nadaje kilometraż odcinkom linii na podstawie długości rysunku (od posterunku startowego, km 0.000).</summary>
    public static int AutoKilometrage(Scenery sc, SceneryLine line, string? startNodeId = null, double startKm = 0)
    {
        var edges = sc.Edges.Where(e => e.LineId == line.Id).ToList();
        if (edges.Count == 0) return 0;
        var deg = new Dictionary<string, int>();
        foreach (var e in edges) { deg[e.NodeA] = deg.GetValueOrDefault(e.NodeA) + 1; deg[e.NodeB] = deg.GetValueOrDefault(e.NodeB) + 1; }
        var start = startNodeId != null && deg.ContainsKey(startNodeId) ? startNodeId
            : deg.Where(kv => kv.Value == 1).Select(kv => sc.Node(kv.Key)).Where(n => n != null).OrderBy(n => n!.X).ThenBy(n => n!.Y).FirstOrDefault()?.Id
              ?? edges[0].NodeA;
        var km = new Dictionary<string, double> { [start] = startKm };
        var queue = new Queue<string>(); queue.Enqueue(start);
        var done = new HashSet<SceneryEdge>();
        var count = 0;
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            foreach (var e in edges.Where(x => !done.Contains(x) && (x.NodeA == u || x.NodeB == u)))
            {
                done.Add(e);
                var v = e.NodeA == u ? e.NodeB : e.NodeA;
                var len = SchematicGeometry.PolyLength(SchematicGeometry.EdgePath(sc, e)) * sc.KmPerUnit;
                var kv = km[u] + len;
                if (!km.ContainsKey(v)) { km[v] = kv; queue.Enqueue(v); }
                var ka = e.NodeA == u ? km[u] : km[v]; var kb = e.NodeA == u ? km[v] : km[u];
                e.KmA = Num.KmDot(ka); e.KmB = Num.KmDot(kb);
                count++;
            }
        }
        return count;
    }

    // ---------------------------------------------------------------- generowanie karty
    private static string Fmt(double v) => Num.KmDot(v);

    private static IEnumerable<SpeedChange> EdgeChanges(Scenery sc, SceneryEdge e, bool forward)
    {
        var line = sc.Line(e.LineId);
        var single = e.Tracks == TrackCount.Single;
        Num.TryParseKm(e.KmA, out var kA); Num.TryParseKm(e.KmB, out var kB);
        var list = e.Changes.Select(c => (km: Num.TryParseKm(c.Km, out var k) ? k : kA, vp: c.Vp, vl: c.Vl, bold: c.BoldKm)).ToList();
        if (list.Count == 0) list.Add((kA, line?.DefaultVp ?? "60", line?.DefaultVl ?? "60", BoldMode.Auto));
        list[0] = (kA, list[0].vp, list[0].vl, list[0].bold);
        var dir = Math.Sign(kB - kA);
        if (dir != 0) list = list.OrderBy(x => x.km * dir).ToList();
        if (forward)
        {
            foreach (var c in list) yield return new SpeedChange { Km = Fmt(c.km), Vp = c.vp, Vl = single ? "" : c.vl, SingleTrack = single, BoldKm = c.bold };
            yield break;
        }
        // jazda B→A: segment i [k_i, k_{i+1}] o prędkości v_i; tor prawy staje się lewym
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var kmStart = i == list.Count - 1 ? kB : list[i + 1].km;
            var c = list[i];
            yield return new SpeedChange { Km = Fmt(kmStart), Vp = single ? c.vp : c.vl, Vl = single ? "" : c.vp, SingleTrack = single, BoldKm = c.bold };
        }
    }

    private static (double kmIn, double kmOut) NodeKms(SceneryEdge? inE, bool inFwd, SceneryEdge? outE, bool outFwd)
    {
        double kin = double.NaN, kout = double.NaN;
        if (inE != null) { var s = inFwd ? inE.KmB : inE.KmA; if (Num.TryParseKm(s, out var k)) kin = k; }
        if (outE != null) { var s = outFwd ? outE.KmA : outE.KmB; if (Num.TryParseKm(s, out var k)) kout = k; }
        if (double.IsNaN(kin)) kin = kout;
        if (double.IsNaN(kout)) kout = kin;
        return (kin, kout);
    }

    /// <summary>Czy pociąg zatrzymuje się na posterunku wg zasady postojów trasy.</summary>
    public static bool IsStop(Timetable t, SceneryNode node, bool first, bool last, ISet<string> waypoints) =>
        first || last || t.Route.StopPolicy switch
        {
            StopPolicy.AllPassenger => node.Passenger && node.Kind != NodeKind.Closed,
            StopPolicy.StationsOnly => node.Passenger && node.Kind == NodeKind.Station,
            StopPolicy.Waypoints => waypoints.Contains(node.Id),
            _ => false,
        };

    /// <summary>Ustawia postój, rodzaj postoju („ph” dla pociągu pasażerskiego na posterunku z obsługą podróżnych) i czas postoju.</summary>
    private static void ApplyStop(Timetable t, SceneryNode node, RoutePoint p, bool first, bool last, bool stop)
    {
        p.Stop = stop;
        p.StopKinds = stop && node.Passenger && RunTimeCalculator.IsPassenger(t.TrainCategory) ? node.DefaultStopKinds : "";
        p.Dwell = stop && !first && !last ? (node.DefaultDwell ?? "").Trim() : "";
        if (!stop) p.Arrival = "";
    }

    /// <summary>
    /// Ponownie stosuje zasadę postojów trasy do posterunków karty pochodzących ze schematu
    /// (z pominięciem posterunków, w których postój zmieniono ręcznie). Zwraca liczbę zmienionych posterunków.
    /// </summary>
    public static int ApplyStopPolicy(Scenery sc, Timetable t)
    {
        var waypoints = new HashSet<string>(t.Route.Waypoints);
        var n = 0;
        for (var i = 0; i < t.Points.Count; i++)
        {
            var p = t.Points[i];
            var node = sc.Node(p.NodeId);
            if (node == null || p.ManualStop) continue;
            var before = (p.Stop, p.StopKinds, p.Dwell);
            ApplyStop(t, node, p, i == 0, i == t.Points.Count - 1, IsStop(t, node, i == 0, i == t.Points.Count - 1, waypoints));
            if (before != (p.Stop, p.StopKinds, p.Dwell)) n++;
        }
        return n;
    }

    private static bool SameStretch(LineSection a, LineSection b)
    {
        if (!string.Equals((a.LineNumber ?? "").Trim(), (b.LineNumber ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        if (!Num.TryParseKm(a.KmStart, out var a0) || !Num.TryParseKm(a.KmEnd, out var a1) || !Num.TryParseKm(b.KmStart, out var b0) || !Num.TryParseKm(b.KmEnd, out var b1)) return false;
        if (Math.Sign(a1 - a0) != Math.Sign(b1 - b0)) return false;
        var overlap = Math.Min(Math.Max(a0, a1), Math.Max(b0, b1)) - Math.Max(Math.Min(a0, a1), Math.Min(b0, b1));
        return overlap > 0.0005;
    }

    private static double V(string? s) =>
        double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;

    private static string MinV(string cur, double limit)
    {
        var v = V(cur);
        if (double.IsNaN(limit) || (!double.IsNaN(v) && v <= limit)) return cur;
        return limit.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Nakłada ograniczenia prędkości posterunków (tryb „zmiana prędkości w karcie”) na zmiany prędkości odcinka linii:
    /// w zakresie km ograniczenia prędkość VP/VL nie przekracza wartości ograniczenia.
    /// Tor nr 1 = prawy przy jeździe w kierunku rosnącego km.
    /// </summary>
    public static void ApplyNodeLimits(Scenery sc, LineSection sec)
    {
        var line = (sec.LineNumber ?? "").Trim();
        if (!Num.TryParseKm(sec.KmStart, out var k0) || !Num.TryParseKm(sec.KmEnd, out var k1) || sec.Changes.Count == 0) return;
        var dir = k1 >= k0 ? 1 : -1;
        double Pos(double km) => (km - k0) * dir;
        var len = Pos(k1);
        var limits = new List<(double a, double b, double v, RestrictionTrack track)>();
        foreach (var n in sc.Nodes)
            foreach (var l in n.SpeedLimits)
            {
                if (!l.Active || l.Mode != NodeLimitMode.Profile) continue;
                if (!string.Equals((l.Line ?? "").Trim(), line, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Num.TryParseKm(l.KmFrom, out var la) || !Num.TryParseKm(l.KmTo, out var lb)) continue;
                var v = V(l.Speed); if (double.IsNaN(v) || v <= 0) continue;
                var a = Math.Max(0, Math.Min(Pos(la), Pos(lb))); var b = Math.Min(len, Math.Max(Pos(la), Pos(lb)));
                if (b - a < 0.0005) continue;
                limits.Add((a, b, v, l.Track));
            }
        if (limits.Count == 0) return;

        var changes = sec.Changes.Select(c => (s: Num.TryParseKm(c.Km, out var k) ? Math.Clamp(Pos(k), 0, len) : 0, c)).ToList();
        changes[0] = (0, changes[0].c);
        var cuts = new SortedSet<double>(changes.Select(x => x.s));
        foreach (var l in limits) { cuts.Add(l.a); if (l.b < len - 0.0005) cuts.Add(l.b); }
        var result = new List<SpeedChange>();
        foreach (var s0 in cuts)
        {
            if (s0 > len - 0.0005 && s0 > 0) continue;
            var baseIdx = changes.FindLastIndex(x => x.s <= s0 + 1e-6);
            var bc = changes[Math.Max(0, baseIdx)].c;
            var isOriginal = changes.Any(x => Math.Abs(x.s - s0) < 1e-6);
            double vpLim = double.NaN, vlLim = double.NaN;
            foreach (var l in limits.Where(l => s0 >= l.a - 1e-6 && s0 < l.b - 1e-6))
            {
                // VP – tor prawy w kierunku jazdy pociągu po odcinku: przy km rosnącym to tor nr 1
                var appliesVp = bc.SingleTrack || l.track == RestrictionTrack.Both || (l.track == RestrictionTrack.Track1) == (dir > 0);
                var appliesVl = bc.SingleTrack || l.track == RestrictionTrack.Both || (l.track == RestrictionTrack.Track2) == (dir > 0);
                if (appliesVp) vpLim = double.IsNaN(vpLim) ? l.v : Math.Min(vpLim, l.v);
                if (appliesVl) vlLim = double.IsNaN(vlLim) ? l.v : Math.Min(vlLim, l.v);
            }
            var nc = new SpeedChange
            {
                Km = Fmt(k0 + dir * s0), Vp = MinV(bc.Vp, vpLim), Vl = bc.SingleTrack ? bc.Vl : MinV(bc.Vl, vlLim),
                SingleTrack = bc.SingleTrack, BoldKm = isOriginal ? bc.BoldKm : BoldMode.Auto,
            };
            var last = result.LastOrDefault();
            if (last != null && last.Vp == nc.Vp && last.Vl == nc.Vl && last.SingleTrack == nc.SingleTrack) continue;
            result.Add(nc);
        }
        sec.Changes.Clear();
        foreach (var c in result) sec.Changes.Add(c);
    }

    private static string LineNo(Scenery sc, SceneryEdge? e) => sc.Line(e?.LineId)?.Number?.Trim() ?? "";

    /// <summary>
    /// Buduje odcinki linii i posterunki karty z trasy na schemacie. Istniejące posterunki (dopasowane po NodeId)
    /// zachowują dane pociągu (postoje, numery, skład, godziny) oraz – gdy oznaczone ManualInfra – ręczne zmiany infrastruktury.
    /// </summary>
    public static List<string> ApplyToTimetable(Scenery sc, Timetable t, RouteResult route)
    {
        var warn = new List<string>(route.Warnings);
        if (!route.Ok) { warn.Add("Trasa jest niekompletna – zaznacz co najmniej początek i koniec na schemacie."); return warn; }

        // ---- odcinki linii
        var sections = new List<LineSection>();
        LineSection? cur = null; double curEnd = double.NaN; int curDir = 0;
        foreach (var hop in route.Hops)
        {
            var e = hop.Edge;
            Num.TryParseKm(hop.Forward ? e.KmA : e.KmB, out var k0);
            Num.TryParseKm(hop.Forward ? e.KmB : e.KmA, out var k1);
            var line = LineNo(sc, e);
            var dir = Math.Sign(k1 - k0);
            var cont = cur != null && cur.LineNumber == line && Math.Abs(curEnd - k0) < 0.0015 && (curDir == 0 || dir == 0 || dir == curDir);
            if (!cont)
            {
                cur = new LineSection { LineNumber = line, KmStart = Fmt(k0), KmEnd = Fmt(k1), Track = TrackSide.Right };
                sections.Add(cur); curDir = dir;
            }
            else { cur!.KmEnd = Fmt(k1); if (curDir == 0) curDir = dir; }
            curEnd = k1;
            foreach (var ch in EdgeChanges(sc, e, hop.Forward))
            {
                var last = cur.Changes.LastOrDefault();
                if (last != null && last.Vp == ch.Vp && last.Vl == ch.Vl && last.SingleTrack == ch.SingleTrack) continue;
                if (last != null && last.Km == ch.Km) cur.Changes.Remove(last);
                cur.Changes.Add(ch);
            }
        }

        // ---- tor jazdy wybrany wcześniej w zakładce Trasa
        foreach (var sec in sections)
        {
            var prevSec = t.Sections.FirstOrDefault(o => SameStretch(o, sec));
            if (prevSec != null) sec.Track = prevSec.Track;
        }

        // ---- ograniczenia prędkości posterunków (geometria głowic itp.)
        foreach (var sec in sections) ApplyNodeLimits(sc, sec);

        // ---- posterunki
        var old = t.Points.ToList();
        var used = new HashSet<RoutePoint>();
        var points = new List<RoutePoint>();
        var waypoints = new HashSet<string>(t.Route.Waypoints);
        for (var i = 0; i < route.Nodes.Count; i++)
        {
            var node = sc.Node(route.Nodes[i]);
            if (node == null) continue;
            var inHop = i > 0 ? route.Hops[i - 1] : null;
            var outHop = i < route.Hops.Count ? route.Hops[i] : null;
            var existing = old.FirstOrDefault(p => p.NodeId == node.Id && !used.Contains(p));
            var p = existing ?? new RoutePoint { NodeId = node.Id };
            if (existing != null) used.Add(existing);
            var first = i == 0; var last = i == route.Nodes.Count - 1;

            if (existing == null || !existing.ManualInfra)
            {
                p.Name = node.Name; p.Bold = node.Bold; p.Uppercase = node.Uppercase; p.TypeSuffix = node.EffectiveTypeSuffix;
                var (kin, kout) = NodeKms(inHop?.Edge, inHop?.Forward ?? true, outHop?.Edge, outHop?.Forward ?? true);
                var lineIn = LineNo(sc, inHop?.Edge); var lineOut = LineNo(sc, outHop?.Edge);
                p.Km = double.IsNaN(kin) ? node.Km
                    : (inHop != null && outHop != null && (lineIn != lineOut || Math.Abs(kin - kout) > 0.0015)) ? $"{Fmt(kin)}/{Fmt(kout)}" : Fmt(first ? kout : kin);
                p.Line = first ? lineOut : lineIn;
                var abbr = new List<string>();
                if (!string.IsNullOrWhiteSpace(node.OpeningHours)) abbr.Add(node.OpeningHours.Trim());
                var nodeAbbr = (node.Abbreviations ?? "").Replace("\r", "").TrimEnd();
                var isTrafficPost = node.Kind != NodeKind.Stop || node.Technical;
                var blocking = isTrafficPost ? (outHop?.Edge.Blocking ?? "").Trim() : "";
                var text = string.Join(", ", abbr.Concat(new[] { nodeAbbr }).Where(x => x.Length > 0));
                if (blocking.Length > 0) text = text.Length == 0 ? blocking : text + ", " + blocking;
                p.Abbreviations = text;
                if (existing == null) { p.NoteTop = ""; p.NoteBottom = ""; }
            }
            p.TracksToNext = outHop?.Edge.Tracks ?? inHop?.Edge.Tracks ?? TrackCount.Double;

            if (existing == null || !existing.ManualStop)
                ApplyStop(t, node, p, first, last, IsStop(t, node, first, last, waypoints));
            points.Add(p);
        }

        // skład – jeśli pierwszy posterunek nie ma danych, uzupełnij z profilu trakcyjnego
        if (points.Count > 0)
        {
            var f = points[0];
            var pr = TractionPresets.Find(t.RunCalc.Preset);
            if (pr != null && string.IsNullOrWhiteSpace(f.Loco1) && string.IsNullOrWhiteSpace(f.Vmax))
            {
                f.Loco1 = pr.Loco; f.Vmax = pr.Vmax.ToString(CultureInfo.InvariantCulture);
                f.TrainLength = pr.Length.ToString(CultureInfo.InvariantCulture);
                f.BrakeType = RunTimeCalculator.IsFreight(t.TrainCategory) ? "G" : "P";
            }
        }

        // ---- podmiana zawartości karty
        t.Sections.Clear(); foreach (var s in sections) t.Sections.Add(s);
        t.Points.Clear(); foreach (var p in points) t.Points.Add(p);
        t.Route.Linked = true;
        t.ManualSections = false;
        var firstNode = sc.Node(route.Nodes[0]); var lastNode = sc.Node(route.Nodes[^1]);
        if (string.IsNullOrWhiteSpace(t.RelationFrom) || old.Count == 0 || old[0].Name == t.RelationFrom) t.RelationFrom = firstNode?.Name ?? "";
        if (string.IsNullOrWhiteSpace(t.RelationTo) || old.Count == 0 || old[^1].Name == t.RelationTo) t.RelationTo = lastNode?.Name ?? "";
        return warn;
    }
}
