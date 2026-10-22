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
using Avalonia.Controls;
using Avalonia.Media;
using Sksrj.Core.Engine;

namespace Sksrj.App.Controls;

/// <summary>Wykres prędkości jazdy v(s) na tle prędkości dopuszczalnych i ograniczeń.</summary>
public sealed class SpeedChart : Control
{
    public static readonly StyledProperty<RunTimeResult?> ResultProperty =
        AvaloniaProperty.Register<SpeedChart, RunTimeResult?>(nameof(Result));

    public RunTimeResult? Result { get => GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    static SpeedChart() => AffectsRender<SpeedChart>(ResultProperty);

    private static readonly Typeface Face = new(new FontFamily("fonts:Inter#Inter, $Default"));

    private IBrush Res(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(fallback);

    private FormattedText T(string s, double size, IBrush b) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, b);

    public override void Render(DrawingContext ctx)
    {
        var r = Result;
        var fg = Res("Md.OnSurfaceVariant", Colors.Gray);
        var grid = Res("Md.OutlineVariant", Colors.LightGray);
        var primary = Res("Md.Primary", Color.FromRgb(0, 99, 155));
        if (r == null || !r.Ok || r.S.Length < 2)
        {
            ctx.DrawText(T("Brak danych do wykresu – uzupełnij trasę i posterunki.", 13, fg), new Point(16, 16));
            return;
        }
        double L = 46, R = 14, Tp = 70, B = 30;
        var w = Bounds.Width - L - R; var h = Bounds.Height - Tp - B;
        if (w < 50 || h < 50) return;
        var smax = r.S[^1];
        var vmax = Math.Max(40, Math.Ceiling(Math.Max(r.LimitBase.Max(), r.V.Max()) * 1.08 / 20) * 20);
        double X(double s) => L + s / Math.Max(0.001, smax) * w;
        double Y(double v) => Tp + h - v / vmax * h;

        var gridPen = new Pen(grid, 1);
        for (var v = 0.0; v <= vmax + 0.1; v += 20)
        {
            ctx.DrawLine(gridPen, new Point(L, Y(v)), new Point(L + w, Y(v)));
            var t = T(v.ToString(CultureInfo.InvariantCulture), 11, fg);
            ctx.DrawText(t, new Point(L - 6 - t.Width, Y(v) - t.Height / 2));
        }
        var steps = new[] { 0.5, 1, 2, 5, 10, 20, 50 };
        var kstep = steps.FirstOrDefault(s => smax / s <= 12, 100);
        for (var s = 0.0; s <= smax + 1e-6; s += kstep)
        {
            ctx.DrawLine(gridPen, new Point(X(s), Tp + h), new Point(X(s), Tp + h + 4));
            var t = T(s.ToString("0.#", CultureInfo.InvariantCulture), 11, fg);
            ctx.DrawText(t, new Point(X(s) - t.Width / 2, Tp + h + 6));
        }
        ctx.DrawText(T("km/h", 11, fg), new Point(4, Tp - 16));
        ctx.DrawText(T("km trasy", 11, fg), new Point(L + w - 46, Tp + h + 6 + 12));

        // posterunki
        var markPen = new Pen(grid, 1, new DashStyle(new double[] { 3, 3 }, 0));
        var lastX = new[] { double.MinValue, double.MinValue, double.MinValue };
        foreach (var m in r.Marks)
        {
            var x = X(m.S);
            ctx.DrawLine(m.Stop ? new Pen(fg, 1) : markPen, new Point(x, Tp), new Point(x, Tp + h));
            if (!m.Stop) continue;
            var t = T(m.Name, 10, fg);
            var lx0 = Math.Clamp(x - t.Width / 2, L - 12, L + w - t.Width);
            for (var row = 0; row < 3; row++)
            {
                if (lx0 < lastX[row] + 6) continue;
                ctx.DrawText(t, new Point(lx0, Tp - 14 - row * 12));
                lastX[row] = lx0 + t.Width;
                break;
            }
        }

        // prędkości dopuszczalne
        DrawSteps(ctx, r.S, r.LimitBase, X, Y, new Pen(fg, 1.5));
        var orange = new SolidColorBrush(Color.FromRgb(230, 120, 20));
        DrawSteps(ctx, r.S, r.LimitAll, X, Y, new Pen(orange, 1.5, new DashStyle(new double[] { 4, 2 }, 0)));
        // ograniczenia – pasy
        foreach (var m in r.Restrictions)
        {
            var c = m.R.Kind == Sksrj.Core.Model.RestrictionKind.Permanent ? Color.FromArgb(40, 120, 120, 120) : Color.FromArgb(55, 230, 120, 20);
            ctx.FillRectangle(new SolidColorBrush(c), new Rect(X(m.S0), Tp, Math.Max(2, X(m.S1) - X(m.S0)), h));
        }
        // jazda
        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(new Point(X(r.S[0]), Y(r.V[0])), false);
            for (var i = 1; i < r.S.Length; i++) gc.LineTo(new Point(X(r.S[i]), Y(r.V[i])));
            gc.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(primary, 2.2, lineJoin: PenLineJoin.Round), g);

        // legenda
        var lx = L; var ly = 2.0;
        void Leg(IPen p, string s) { ctx.DrawLine(p, new Point(lx, ly + 7), new Point(lx + 22, ly + 7)); ctx.DrawText(T(s, 11, fg), new Point(lx + 26, ly)); lx += 34 + T(s, 11, fg).Width; }
        Leg(new Pen(primary, 2.2), "prędkość jazdy");
        Leg(new Pen(fg, 1.5), "dopuszczalna");
        Leg(new Pen(orange, 1.5, new DashStyle(new double[] { 4, 2 }, 0)), "z ostrzeżeniami");
    }

    private static void DrawSteps(DrawingContext ctx, double[] s, double[] v, Func<double, double> X, Func<double, double> Y, IPen pen)
    {
        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(new Point(X(s[0]), Y(v[0])), false);
            for (var i = 1; i < s.Length; i++)
            {
                if (Math.Abs(v[i] - v[i - 1]) > 0.01) gc.LineTo(new Point(X(s[i]), Y(v[i - 1])));
                gc.LineTo(new Point(X(s[i]), Y(v[i])));
            }
            gc.EndFigure(false);
        }
        ctx.DrawGeometry(null, pen, g);
    }
}
