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

using System.Reflection;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Sksrj.Core.Layout;
using Sksrj.Core.Model;

namespace Sksrj.Core.Pdf;

/// <summary>Czcionka szeryfowa wbudowana w program (Liberation Serif – metrycznie zgodna z Times New Roman, licencja SIL OFL).</summary>
public sealed class EmbeddedFontResolver : IFontResolver
{
    public const string Family = "Liberation Serif";
    private static readonly object Lock = new();
    private static bool _installed;

    public static void Install()
    {
        lock (Lock)
        {
            if (_installed) return;
            if (GlobalFontSettings.FontResolver == null) GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
            _installed = true;
        }
    }

    public const string SansFamily = "Liberation Sans";

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (familyName.Equals(SansFamily, StringComparison.OrdinalIgnoreCase))
            return new FontResolverInfo(bold ? "LiberationSans-Bold" : "LiberationSans-Regular");
        var face = (bold, italic) switch
        {
            (true, true) => "LiberationSerif-BoldItalic",
            (true, false) => "LiberationSerif-Bold",
            (false, true) => "LiberationSerif-Italic",
            _ => "LiberationSerif-Regular",
        };
        return new FontResolverInfo(face);
    }

    public byte[]? GetFont(string faceName) => LoadFontBytes(faceName);

    public static byte[] LoadFontBytes(string faceName)
    {
        var asm = typeof(EmbeddedFontResolver).Assembly;
        using var s = asm.GetManifestResourceStream($"Sksrj.Fonts.{faceName}.ttf")
                      ?? throw new InvalidOperationException($"Brak czcionki {faceName}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}

/// <summary>Pomiar tekstu za pomocą PDFsharp – ten sam pomiar używany jest dla PDF i podglądu.</summary>
public sealed class PdfTextMeasurer : ITextMeasurer
{
    private readonly XGraphics _g;
    private readonly Dictionary<(double, bool, bool, bool), XFont> _fonts = new();
    private readonly Dictionary<(string, double, bool, bool, bool), double> _cache = new();

    public PdfTextMeasurer()
    {
        EmbeddedFontResolver.Install();
        _g = XGraphics.CreateMeasureContext(new XSize(2000, 2000), XGraphicsUnit.Point, XPageDirection.Downwards);
    }

    internal XFont Font(FontSpec f)
    {
        var key = (f.Size, f.Bold, f.Italic, f.Sans);
        if (!_fonts.TryGetValue(key, out var font))
        {
            var style = (f.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular) | (f.Italic && !f.Sans ? XFontStyleEx.Italic : XFontStyleEx.Regular);
            font = new XFont(f.Sans ? EmbeddedFontResolver.SansFamily : EmbeddedFontResolver.Family, f.Size, style, new XPdfFontOptions(PdfFontEncoding.Unicode));
            _fonts[key] = font;
        }
        return font;
    }

    public double Width(string text, FontSpec font)
    {
        var key = (text, font.Size, font.Bold, font.Italic, font.Sans);
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var w)) return w;
            w = _g.MeasureString(text, Font(font)).Width;
            if (_cache.Count > 20000) _cache.Clear();
            _cache[key] = w;
            return w;
        }
    }
}

public static class PdfExporter
{
    /// <summary>Zamienia strony A5 na arkusze wg ustawień (A5 lub 2×A5 na A4 poziomo).</summary>
    public static List<(double W, double H, List<(PageDrawing Page, double Dx)> Items)> Sheets(IReadOnlyList<PageDrawing> pages, PageLayoutKind layout)
    {
        var res = new List<(double, double, List<(PageDrawing, double)>)>();
        if (layout == PageLayoutKind.A5)
        {
            foreach (var p in pages) res.Add((p.Width, p.Height, new() { (p, 0) }));
            return res;
        }
        const double a4w = 841.89, a4h = 595.28;
        for (var i = 0; i < pages.Count; i += 2)
        {
            var items = new List<(PageDrawing, double)> { (pages[i], (a4w / 2 - pages[i].Width) / 2) };
            if (i + 1 < pages.Count) items.Add((pages[i + 1], a4w / 2 + (a4w / 2 - pages[i + 1].Width) / 2));
            res.Add((a4w, a4h, items));
        }
        return res;
    }

    public static void Export(IEnumerable<(Timetable t, LayoutResult layout)> cards, PrintSettings print, Stream output, string title)
    {
        EmbeddedFontResolver.Install();
        var measurer = new PdfTextMeasurer();
        using var doc = new PdfDocument();
        doc.Info.Title = title;
        doc.Info.Creator = AppInfo.Name;
        foreach (var (_, layout) in cards)
        {
            foreach (var (w, h, items) in Sheets(layout.Pages, print.Layout))
            {
                var page = doc.AddPage();
                page.Width = XUnit.FromPoint(w);
                page.Height = XUnit.FromPoint(h);
                using var g = XGraphics.FromPdfPage(page);
                foreach (var (pd, dx) in items)
                {
                    var state = g.Save();
                    g.TranslateTransform(dx, (h - pd.Height) / 2);
                    Render(g, pd, measurer);
                    g.Restore(state);
                }
            }
        }
        doc.Save(output, false);
    }

    public static void ExportFile(Timetable t, PrintSettings print, string path, IEnumerable<SpeedRestriction>? restrictions = null)
    {
        var m = new PdfTextMeasurer();
        var layout = TimetableLayout.Build(t, print, m, null, restrictions);
        using var fs = File.Create(path);
        Export(new[] { (t, layout) }, print, fs, $"Karta rozkładu jazdy pociągu {t.TrainCategory} {t.TrainNumber}".Trim());
    }

    /// <summary>Eksport planu schematycznego scenerii do PDF.</summary>
    public static void ExportScheme(Scenery sc, Stream output)
    {
        EmbeddedFontResolver.Install();
        var m = new PdfTextMeasurer();
        var pd = Engine.SchematicRenderer.ExportPage(sc, m);
        using var doc = new PdfDocument();
        doc.Info.Title = sc.Name;
        doc.Info.Creator = AppInfo.Name;
        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(pd.Width); page.Height = XUnit.FromPoint(pd.Height);
        using (var g = XGraphics.FromPdfPage(page)) Render(g, pd, m);
        doc.Save(output, false);
    }

    private static XColor C(Rgb c) => XColor.FromArgb(c.R, c.G, c.B);

    public static void Render(XGraphics g, PageDrawing page, PdfTextMeasurer m)
    {
        foreach (var op in page.Ops)
        {
            switch (op)
            {
                case LineOp l:
                    g.DrawLine(new XPen(C(l.Color), l.Width) { LineCap = XLineCap.Flat }, l.X1, l.Y1, l.X2, l.Y2);
                    break;
                case FillRectOp r:
                    g.DrawRectangle(new XSolidBrush(C(r.Color)), r.X, r.Y, r.W, r.H);
                    break;
                case PolygonOp p:
                    g.DrawPolygon(new XSolidBrush(C(p.Color)), p.Points.Select(x => new XPoint(x.X, x.Y)).ToArray(), XFillMode.Winding);
                    break;
                case EllipseOp el:
                {
                    var rect = new XRect(el.Cx - el.Rx, el.Cy - el.Ry, el.Rx * 2, el.Ry * 2);
                    if (el.Fill is { } f && el.Stroke is { } st) g.DrawEllipse(new XPen(C(st), el.StrokeWidth), new XSolidBrush(C(f)), rect);
                    else if (el.Fill is { } f2) g.DrawEllipse(new XSolidBrush(C(f2)), rect);
                    else if (el.Stroke is { } st2) g.DrawEllipse(new XPen(C(st2), el.StrokeWidth), rect);
                    break;
                }
                case PolylineOp pl:
                {
                    var pen = new XPen(C(pl.Color), pl.Width) { LineJoin = pl.RoundJoin ? XLineJoin.Round : XLineJoin.Miter, LineCap = pl.Dash == null ? XLineCap.Round : XLineCap.Flat };
                    if (pl.Dash != null) pen.DashPattern = pl.Dash.Select(d => d / Math.Max(0.1, pl.Width)).ToArray();
                    g.DrawLines(pen, pl.Points.Select(x => new XPoint(x.X, x.Y)).ToArray());
                    break;
                }
                case TextOp t:
                    g.DrawString(t.Text, m.Font(t.Font), new XSolidBrush(C(t.Color)), t.X, t.Y, XStringFormats.BaseLineLeft);
                    break;
            }
        }
    }
}
