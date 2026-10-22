namespace Sksrj.Core.Layout;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb Black = new(0, 0, 0);
    /// <summary>Kolor powtórzonych (niezmienionych) parametrów składu.</summary>
    public static readonly Rgb Grey = new(168, 168, 168);
    public static readonly Rgb LightGrey = new(200, 200, 200);
}

public readonly record struct FontSpec(double Size, bool Bold = false, bool Italic = false);

public enum Align { Left, Center, Right }

public abstract record DrawOp;
public sealed record LineOp(double X1, double Y1, double X2, double Y2, double Width, Rgb Color) : DrawOp;
public sealed record FillRectOp(double X, double Y, double W, double H, Rgb Color) : DrawOp;
public sealed record PolygonOp(IReadOnlyList<(double X, double Y)> Points, Rgb Color) : DrawOp;
/// <summary>Tekst; Y to linia bazowa.</summary>
public sealed record TextOp(double X, double Y, string Text, FontSpec Font, Rgb Color) : DrawOp;

/// <summary>Strona karty (współrzędne w punktach typograficznych, oś Y w dół).</summary>
public sealed class PageDrawing
{
    public double Width { get; init; }
    public double Height { get; init; }
    public int Number { get; init; }
    public List<DrawOp> Ops { get; } = new();
}

public interface ITextMeasurer
{
    double Width(string text, FontSpec font);
}

/// <summary>Pomocnik do budowy listy operacji rysowania.</summary>
public sealed class Canvas
{
    private readonly PageDrawing _page;
    private readonly ITextMeasurer _m;

    public Canvas(PageDrawing page, ITextMeasurer measurer) { _page = page; _m = measurer; }

    public ITextMeasurer Measurer => _m;
    public double W(string s, FontSpec f) => string.IsNullOrEmpty(s) ? 0 : _m.Width(s, f);

    public void Line(double x1, double y1, double x2, double y2, double w = 0.5, Rgb? c = null) =>
        _page.Ops.Add(new LineOp(x1, y1, x2, y2, w, c ?? Rgb.Black));

    public void HLine(double x1, double x2, double y, double w = 0.5, Rgb? c = null) => Line(x1, y, x2, y, w, c);
    public void VLine(double x, double y1, double y2, double w = 0.5, Rgb? c = null) { if (y2 - y1 > 0.01) Line(x, y1, x, y2, w, c); }

    public void Fill(double x, double y, double w, double h, Rgb? c = null)
    {
        if (w > 0.01 && h > 0.01) _page.Ops.Add(new FillRectOp(x, y, w, h, c ?? Rgb.Black));
    }

    public void Rect(double x, double y, double w, double h, double lw = 0.5, Rgb? c = null)
    {
        HLine(x, x + w, y, lw, c); HLine(x, x + w, y + h, lw, c);
        VLine(x, y, y + h, lw, c); VLine(x + w, y, y + h, lw, c);
    }

    public void Polygon(Rgb c, params (double, double)[] pts) => _page.Ops.Add(new PolygonOp(pts, c));

    /// <summary>Rysuje tekst; x to lewa krawędź / środek / prawa krawędź zależnie od wyrównania. Zwraca szerokość.</summary>
    public double Text(double x, double baseline, string text, FontSpec f, Align a = Align.Left, Rgb? c = null, bool underline = false)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var w = W(text, f);
        var x0 = a switch { Align.Center => x - w / 2, Align.Right => x - w, _ => x };
        _page.Ops.Add(new TextOp(x0, baseline, text, f, c ?? Rgb.Black));
        if (underline) HLine(x0, x0 + w, baseline + f.Size * 0.14, Math.Max(0.45, f.Size * 0.06), c);
        return w;
    }

    /// <summary>Tekst z cyfrą w indeksie górnym (np. 05:48⁵, 3⁵).</summary>
    public double TextSup(double x, double baseline, string main, string sup, FontSpec f, double supSize, double rise, Align a = Align.Left, Rgb? c = null)
    {
        var fs = f with { Size = supSize };
        var wm = W(main, f);
        var ws = string.IsNullOrEmpty(sup) ? 0 : W(sup, fs) + 0.4;
        var total = wm + ws;
        var x0 = a switch { Align.Center => x - total / 2, Align.Right => x - total, _ => x };
        Text(x0, baseline, main, f, Align.Left, c);
        if (ws > 0) Text(x0 + wm + 0.4, baseline - rise, sup, fs, Align.Left, c);
        return total;
    }

    /// <summary>Zawija tekst do zadanej szerokości (z zachowaniem ręcznych podziałów wiersza).</summary>
    public List<string> Wrap(string text, FontSpec f, double width)
    {
        var res = new List<string>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var words = raw.Split(' ');
            var cur = "";
            foreach (var w in words)
            {
                var cand = cur.Length == 0 ? w : cur + " " + w;
                if (cur.Length > 0 && W(cand, f) > width) { res.Add(cur); cur = w; }
                else cur = cand;
            }
            res.Add(cur);
        }
        return res;
    }
}
