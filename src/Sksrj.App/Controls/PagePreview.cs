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
using Sksrj.Core.Layout;
using Sksrj.Core.Model;
using Sksrj.Core.Pdf;

namespace Sksrj.App.Controls;

/// <summary>Podgląd stron karty – rysuje dokładnie te same operacje co eksport PDF.</summary>
public sealed class PagePreview : Control
{
    public static readonly StyledProperty<IReadOnlyList<PageDrawing>?> PagesProperty =
        AvaloniaProperty.Register<PagePreview, IReadOnlyList<PageDrawing>?>(nameof(Pages));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<PagePreview, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<PageLayoutKind> SheetLayoutProperty =
        AvaloniaProperty.Register<PagePreview, PageLayoutKind>(nameof(SheetLayout));

    public IReadOnlyList<PageDrawing>? Pages { get => GetValue(PagesProperty); set => SetValue(PagesProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public PageLayoutKind SheetLayout { get => GetValue(SheetLayoutProperty); set => SetValue(SheetLayoutProperty, value); }

    static PagePreview()
    {
        AffectsMeasure<PagePreview>(PagesProperty, ZoomProperty, SheetLayoutProperty);
        AffectsRender<PagePreview>(PagesProperty, ZoomProperty, SheetLayoutProperty);
    }

    private const double Gap = 20, Pad = 24;
    private double Scale => Zoom * 96.0 / 72.0;

    private readonly OpsRenderer _r = new();

    private List<(double W, double H, List<(PageDrawing Page, double Dx)> Items)> Sheets() =>
        Pages == null ? new() : PdfExporter.Sheets(Pages, SheetLayout);

    protected override Size MeasureOverride(Size availableSize)
    {
        var sheets = Sheets();
        if (sheets.Count == 0) return new Size(0, 0);
        var w = sheets.Max(s => s.W) * Scale + 2 * Pad;
        var h = sheets.Sum(s => s.H * Scale) + Gap * (sheets.Count - 1) + 2 * Pad;
        return new Size(w, h);
    }

    public override void Render(DrawingContext ctx)
    {
        var sheets = Sheets();
        var s = Scale;
        var y = Pad;
        var totalW = Bounds.Width;
        var shadow = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        foreach (var (w, h, items) in sheets)
        {
            var x = Math.Max(Pad, (totalW - w * s) / 2);
            var rect = new Rect(x, y, w * s, h * s);
            ctx.FillRectangle(shadow, rect.Translate(new Vector(0, 2)).Inflate(1.5), 2);
            ctx.FillRectangle(Brushes.White, rect);
            foreach (var (page, dx) in items)
            {
                using (ctx.PushClip(rect))
                using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(x + dx * s, y + (h - page.Height) / 2 * s)))
                    DrawPage(ctx, page);
            }
            y += h * s + Gap;
        }
    }

    private void DrawPage(DrawingContext ctx, PageDrawing page) => _r.Draw(ctx, page.Ops);
}
