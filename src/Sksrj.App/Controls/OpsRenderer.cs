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
using Avalonia;
using Avalonia.Media;
using Sksrj.Core.Layout;

namespace Sksrj.App.Controls;

/// <summary>Rysuje operacje z <see cref="PageDrawing"/> w Avalonii (te same, które trafiają do PDF).</summary>
public sealed class OpsRenderer
{
    private static readonly FontFamily Serif = new("avares://SKSRJ/Assets/Fonts#Liberation Serif");
    private static readonly FontFamily Sans = new("avares://SKSRJ/Assets/Fonts#Liberation Sans");
    private readonly Dictionary<(bool, bool, bool), Typeface> _faces = new();
    private readonly Dictionary<Rgb, IBrush> _brushes = new();

    public IBrush B(Rgb c)
    {
        if (!_brushes.TryGetValue(c, out var b)) _brushes[c] = b = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)).ToImmutable();
        return b;
    }

    private Typeface Face(FontSpec f)
    {
        var key = (f.Bold, f.Italic, f.Sans);
        if (!_faces.TryGetValue(key, out var t))
            _faces[key] = t = new Typeface(f.Sans ? Sans : Serif, f.Italic ? FontStyle.Italic : FontStyle.Normal, f.Bold ? FontWeight.Bold : FontWeight.Normal);
        return t;
    }

    public void Draw(DrawingContext ctx, IEnumerable<DrawOp> ops)
    {
        foreach (var op in ops)
        {
            switch (op)
            {
                case LineOp l:
                    ctx.DrawLine(new Pen(B(l.Color), l.Width, lineCap: PenLineCap.Flat), new Point(l.X1, l.Y1), new Point(l.X2, l.Y2));
                    break;
                case FillRectOp r:
                    ctx.FillRectangle(B(r.Color), new Rect(r.X, r.Y, r.W, r.H));
                    break;
                case PolygonOp p:
                    ctx.DrawGeometry(B(p.Color), null, Poly(p.Points, true));
                    break;
                case EllipseOp e:
                    ctx.DrawEllipse(e.Fill is { } f ? B(f) : null, e.Stroke is { } s ? new Pen(B(s), e.StrokeWidth) : null, new Point(e.Cx, e.Cy), e.Rx, e.Ry);
                    break;
                case PolylineOp pl:
                {
                    var pen = new Pen(B(pl.Color), pl.Width, lineCap: pl.Dash == null ? PenLineCap.Round : PenLineCap.Flat, lineJoin: pl.RoundJoin ? PenLineJoin.Round : PenLineJoin.Miter);
                    if (pl.Dash != null) pen.DashStyle = new DashStyle(pl.Dash.Select(d => d / Math.Max(0.1, pl.Width)), 0);
                    ctx.DrawGeometry(null, pen, Poly(pl.Points, false));
                    break;
                }
                case TextOp t:
                {
                    var ft = new FormattedText(t.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face(t.Font), t.Font.Size, B(t.Color));
                    ctx.DrawText(ft, new Point(t.X, t.Y - ft.Baseline));
                    break;
                }
            }
        }
    }

    private static StreamGeometry Poly(IReadOnlyList<(double X, double Y)> pts, bool closed)
    {
        var g = new StreamGeometry();
        using var gc = g.Open();
        gc.BeginFigure(new Point(pts[0].X, pts[0].Y), closed);
        for (var i = 1; i < pts.Count; i++) gc.LineTo(new Point(pts[i].X, pts[i].Y));
        gc.EndFigure(closed);
        return g;
    }
}
