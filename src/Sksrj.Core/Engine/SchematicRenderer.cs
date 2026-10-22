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

using Sksrj.Core.Layout;
using Sksrj.Core.Model;

namespace Sksrj.Core.Engine;

/// <summary>Kolory planu schematycznego (jasny – jak w PDF, ciemny – tylko w edytorze).</summary>
public sealed record SchemeColors(Rgb Background, Rgb Dots, Rgb Label, Rgb StationFill, Rgb StationStroke, Rgb ClosedFill, Rgb ClosedStroke, Rgb IconCutout, bool Dark)
{
    public static readonly SchemeColors Light = new(Rgb.White, new(214, 218, 226), new(26, 35, 126), Rgb.White, Rgb.Black, new(189, 189, 189), new(117, 117, 117), Rgb.White, false);
    public static readonly SchemeColors DarkTheme = new(new(23, 26, 31), new(52, 57, 66), new(190, 200, 255), new(23, 26, 31), new(236, 239, 244), new(74, 79, 87), new(150, 156, 165), new(23, 26, 31), true);

    /// <summary>Kolor linii – w trybie ciemnym zbyt ciemne kolory są rozjaśniane, by były czytelne na ciemnym tle.</summary>
    public Rgb LineColor(Rgb c)
    {
        if (!Dark) return c;
        var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        if (lum >= 0.32) return c;
        var k = Math.Clamp((0.32 - lum) / 0.32, 0, 1) * 0.75 + 0.1;
        byte Mix(byte v) => (byte)Math.Round(v + (235 - v) * k);
        return new Rgb(Mix(c.R), Mix(c.G), Mix(c.B));
    }
}

/// <summary>Rysuje plan schematyczny scenerii (styl mapy metra) jako listę operacji – dla edytora i eksportu PDF.</summary>
public static class SchematicRenderer
{
    public static readonly Rgb LabelColor = SchemeColors.Light.Label;
    public static readonly FontSpec LabelFont = new(10.5, true, false, true);
    public const double StationR = 9;
    [ThreadStatic] private static SchemeColors? _pal;
    private static SchemeColors Pal => _pal ?? SchemeColors.Light;

    public static (double X0, double Y0, double X1, double Y1) Bounds(Scenery sc)
    {
        if (sc.Nodes.Count == 0) return (0, 0, 800, 400);
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        void Add(double x, double y) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        foreach (var n in sc.Nodes) Add(n.X, n.Y);
        foreach (var e in sc.Edges) foreach (var v in e.Via) Add(v.X, v.Y);
        return (x0 - 90, y0 - 60, x1 + 90, y1 + 60);
    }

    public static Rgb NodeLineColor(Scenery sc, SceneryNode n)
    {
        var e = sc.Edges.FirstOrDefault(x => x.NodeA == n.Id || x.NodeB == n.Id);
        return Rgb.Parse(sc.Line(e?.LineId)?.Color, Rgb.Black);
    }

    public static List<string> LabelLines(SceneryNode n)
    {
        var text = n.DisplayLabel ?? "";
        if (text.Contains('\n')) return text.Split('\n').Select(x => x.Trim()).ToList();
        if (text.Length > 11 && text.Contains(' '))
        {
            // podział przy spacji najbliższej środka
            var mid = text.Length / 2; var best = -1;
            for (var i = 0; i < text.Length; i++)
                if (text[i] == ' ' && (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid))) best = i;
            return new List<string> { text[..best].Trim(), text[(best + 1)..].Trim() };
        }
        return new List<string> { text };
    }

    public static void Render(Canvas c, Scenery sc, SchemeColors? colors = null)
    {
        var prev = _pal;
        _pal = colors ?? SchemeColors.Light;
        try { RenderCore(c, sc); }
        finally { _pal = prev; }
    }

    private static void RenderCore(Canvas c, Scenery sc)
    {
        // ---- odcinki
        foreach (var e in sc.Edges)
        {
            var line = sc.Line(e.LineId);
            var col = Pal.LineColor(Rgb.Parse(line?.Color, Rgb.Grey));
            var path = SchematicGeometry.EdgePath(sc, e);
            if (path.Count < 2) continue;
            var w = line?.Width ?? 5;
            c.Polyline(path.Select(p => (p.X, p.Y)).ToList(), w, col, line?.Dashed == true ? new[] { w * 2.2, w * 1.4 } : null);
        }
        // ---- posterunki
        foreach (var n in sc.Nodes) RenderNode(c, sc, n);
    }

    public static void RenderNode(Canvas c, Scenery sc, SceneryNode n)
    {
        var lineCol = Pal.LineColor(NodeLineColor(sc, n));
        var d = SchematicGeometry.DirectionAt(sc, n);
        var perp = new Pt(-d.Y, d.X);
        // kreska w stronę opisu
        var toward = n.LabelSide switch { LabelSide.Top => new Pt(0, -1), LabelSide.Bottom => new Pt(0, 1), LabelSide.Left => new Pt(-1, 0), _ => new Pt(1, 0) };
        if (perp.X * toward.X + perp.Y * toward.Y < 0) perp = new Pt(-perp.X, -perp.Y);
        switch (n.Kind)
        {
            case NodeKind.Station:
                c.Ellipse(n.X, n.Y, StationR, StationR, Pal.StationFill, Pal.StationStroke, 3.6);
                break;
            case NodeKind.Closed:
                c.Ellipse(n.X, n.Y, StationR, StationR, Pal.ClosedFill, Pal.ClosedStroke, 3.2);
                var k = StationR * 0.62;
                c.Line(n.X - k, n.Y - k, n.X + k, n.Y + k, 2.6, Pal.ClosedStroke);
                c.Line(n.X - k, n.Y + k, n.X + k, n.Y - k, 2.6, Pal.ClosedStroke);
                break;
            case NodeKind.Stop:
                c.Line(n.X, n.Y, n.X + perp.X * 9, n.Y + perp.Y * 9, 3.2, lineCol);
                break;
            case NodeKind.End:
                c.Line(n.X - perp.X * 5, n.Y - perp.Y * 5, n.X + perp.X * 5, n.Y + perp.Y * 5, 3.2, lineCol);
                break;
        }
        RenderLabel(c, n);
    }

    private static void RenderLabel(Canvas c, SceneryNode n)
    {
        var lines = LabelLines(n);
        if (lines.Count == 0 || (lines.Count == 1 && lines[0].Length == 0)) return;
        const double lh = 12.5;
        var f = LabelFont;
        var showTrain = n.Passenger && n.Kind == NodeKind.Station;
        var iconW = (showTrain ? 12 : 0) + (n.Technical ? 11 : 0) + (n.Airport ? 12 : 0);
        double[] widths = lines.Select((l, i) => c.W(l, f) + (i == lines.Count - 1 ? iconW : 0)).ToArray();
        double baseY;
        switch (n.LabelSide)
        {
            case LabelSide.Top: baseY = n.Y - 17 - (lines.Count - 1) * lh; break;
            case LabelSide.Bottom: baseY = n.Y + 26; break;
            default: baseY = n.Y + 4 - (lines.Count - 1) * lh / 2; break;
        }
        for (var i = 0; i < lines.Count; i++)
        {
            var w = widths[i];
            var x = n.LabelSide switch
            {
                LabelSide.Left => n.X - 15 - w,
                LabelSide.Right => n.X + 15,
                _ => n.X - w / 2,
            };
            var y = baseY + i * lh;
            var tw = c.Text(x, y, lines[i], f, Align.Left, Pal.Label);
            if (i == lines.Count - 1)
            {
                var ix = x + tw + (tw > 0 ? 3 : 0);
                if (showTrain) { TrainIcon(c, ix, y); ix += 12; }
                if (n.Technical) { CommandIcon(c, ix + 4, y - 4); ix += 11; }
                if (n.Airport) { PlaneIcon(c, ix + 5, y - 4); }
            }
        }
    }

    /// <summary>Piktogram pociągu (obsługa podróżnych).</summary>
    public static void TrainIcon(Canvas c, double x, double baseline)
    {
        var col = Pal.Label; var cut = Pal.IconCutout;
        c.Fill(x, baseline - 8.5, 9, 7.2, col);
        c.Fill(x + 1.3, baseline - 7.3, 2.6, 2.4, cut);
        c.Fill(x + 5.1, baseline - 7.3, 2.6, 2.4, cut);
        c.Fill(x + 1.2, baseline - 3.2, 6.6, 0.9, cut);
        c.Line(x + 1.5, baseline - 1.3, x + 0.3, baseline + 0.4, 1, col);
        c.Line(x + 7.5, baseline - 1.3, x + 8.7, baseline + 0.4, 1, col);
    }

    /// <summary>Znak ⌘ – posterunek techniczny.</summary>
    public static void CommandIcon(Canvas c, double cx, double cy)
    {
        var col = Pal.Label;
        const double h = 1.7, r = 1.5;
        c.Line(cx - h, cy - h - r, cx - h, cy + h + r, 1.1, col);
        c.Line(cx + h, cy - h - r, cx + h, cy + h + r, 1.1, col);
        c.Line(cx - h - r, cy - h, cx + h + r, cy - h, 1.1, col);
        c.Line(cx - h - r, cy + h, cx + h + r, cy + h, 1.1, col);
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            c.Ellipse(cx + sx * (h + r * 0.9), cy + sy * (h + r * 0.9), r, r, null, col, 1.1);
    }

    public static void PlaneIcon(Canvas c, double cx, double cy)
    {
        var col = Pal.Label;
        c.Polygon(col, (cx - 5, cy + 0.6), (cx + 4, cy - 0.8), (cx + 5.2, cy), (cx + 4, cy + 0.8));
        c.Polygon(col, (cx - 0.5, cy), (cx + 1.8, cy - 4.8), (cx + 2.6, cy - 4.8), (cx + 1.6, cy));
        c.Polygon(col, (cx - 0.5, cy), (cx + 1.8, cy + 4.8), (cx + 2.6, cy + 4.8), (cx + 1.6, cy));
        c.Polygon(col, (cx - 4.5, cy), (cx - 4.2, cy - 2.4), (cx - 3.4, cy - 2.4), (cx - 3.2, cy));
    }

    /// <summary>Strona PDF ze schematem (dopasowana do A3 poziomo) z tytułem i legendą linii.</summary>
    public static PageDrawing ExportPage(Scenery sc, ITextMeasurer m)
    {
        var (x0, y0, x1, y1) = Bounds(sc);
        const double maxW = 1190.55 - 60, margin = 30, head = 46;
        var legendH = 18 + Math.Ceiling(sc.Lines.Count / 4.0) * 15;
        var w = x1 - x0; var h = y1 - y0;
        var scale = Math.Min(1.0, maxW / w);
        var page = new PageDrawing { Width = w * scale + 2 * margin, Height = h * scale + 2 * margin + head + legendH, Number = 1 };
        var inner = new PageDrawing { Width = w, Height = h };
        var ic = new Canvas(inner, m);
        Render(ic, sc);
        var c = new Canvas(page, m);
        c.Text(margin, margin + 14, string.IsNullOrWhiteSpace(sc.Name) ? "Schemat scenerii" : sc.Name, new FontSpec(16, true, false, true), Align.Left, LabelColor);
        c.Text(margin, margin + 30, "Plan schematyczny scenerii – " + AppInfo.Name, new FontSpec(9, false, false, true), Align.Left, Rgb.Grey);
        double Tx(double x) => margin + (x - x0) * scale;
        double Ty(double y) => margin + head + (y - y0) * scale;
        foreach (var op in inner.Ops) page.Ops.Add(Transform(op, Tx, Ty, scale));
        // legenda
        var ly = page.Height - margin - legendH + 14;
        var lx = margin;
        var i = 0;
        foreach (var l in sc.Lines)
        {
            var col = Rgb.Parse(l.Color, Rgb.Grey);
            var cx = lx + (i % 4) * 270; var cy = ly + (i / 4) * 15;
            c.Polyline(new[] { (cx, cy - 3.5), (cx + 28, cy - 3.5) }, 4, col, l.Dashed ? new[] { 8.0, 5.0 } : null);
            c.Text(cx + 36, cy, $"linia {l.Number}" + (string.IsNullOrWhiteSpace(l.Name) ? "" : $" – {l.Name}"), new FontSpec(9, false, false, true), Align.Left, Rgb.Black);
            i++;
        }
        return page;
    }

    private static DrawOp Transform(DrawOp op, Func<double, double> tx, Func<double, double> ty, double s) => op switch
    {
        LineOp l => l with { X1 = tx(l.X1), Y1 = ty(l.Y1), X2 = tx(l.X2), Y2 = ty(l.Y2), Width = l.Width * s },
        FillRectOp r => r with { X = tx(r.X), Y = ty(r.Y), W = r.W * s, H = r.H * s },
        PolygonOp p => p with { Points = p.Points.Select(q => (tx(q.X), ty(q.Y))).ToList() },
        EllipseOp e => e with { Cx = tx(e.Cx), Cy = ty(e.Cy), Rx = e.Rx * s, Ry = e.Ry * s, StrokeWidth = e.StrokeWidth * s },
        PolylineOp p => p with { Points = p.Points.Select(q => (tx(q.X), ty(q.Y))).ToList(), Width = p.Width * s, Dash = p.Dash?.Select(d => d * s).ToArray() },
        TextOp t => t with { X = tx(t.X), Y = ty(t.Y), Font = t.Font with { Size = t.Font.Size * s } },
        _ => op,
    };
}
