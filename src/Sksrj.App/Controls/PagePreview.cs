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

    private static readonly FontFamily Serif = new("avares://SKSRJ/Assets/Fonts#Liberation Serif");
    private readonly Dictionary<(bool, bool), Typeface> _faces = new();
    private readonly Dictionary<Rgb, IBrush> _brushes = new();

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

    private IBrush B(Rgb c)
    {
        if (!_brushes.TryGetValue(c, out var b)) _brushes[c] = b = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)).ToImmutable();
        return b;
    }

    private Typeface Face(FontSpec f)
    {
        var key = (f.Bold, f.Italic);
        if (!_faces.TryGetValue(key, out var t))
            _faces[key] = t = new Typeface(Serif, f.Italic ? FontStyle.Italic : FontStyle.Normal, f.Bold ? FontWeight.Bold : FontWeight.Normal);
        return t;
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

    private void DrawPage(DrawingContext ctx, PageDrawing page)
    {
        foreach (var op in page.Ops)
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
                {
                    var g = new StreamGeometry();
                    using (var gc = g.Open())
                    {
                        gc.BeginFigure(new Point(p.Points[0].X, p.Points[0].Y), true);
                        for (var i = 1; i < p.Points.Count; i++) gc.LineTo(new Point(p.Points[i].X, p.Points[i].Y));
                        gc.EndFigure(true);
                    }
                    ctx.DrawGeometry(B(p.Color), null, g);
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
}
