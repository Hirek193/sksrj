using Sksrj.Core.Util;

namespace Sksrj.Core.Model;

public static class TimetableTools
{
    /// <summary>
    /// Przelicza godziny: od odjazdu z pierwszego posterunku, dodając czasy jazdy (normalne, a gdy brak – skrócone)
    /// oraz czasy postoju na posterunkach z postojem. Zwraca liczbę zmienionych posterunków.
    /// </summary>
    public static int RecalculateTimes(Timetable t, bool useShortTimes = false)
    {
        var pts = t.Points;
        if (pts.Count < 2) return 0;
        if (!ClockTime.TryParse(pts[0].Departure, out var cur)) return 0;
        var changed = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            var p = pts[i];
            var src = useShortTimes ? (string.IsNullOrWhiteSpace(p.TravelShort) ? p.TravelNormal : p.TravelShort)
                                    : (string.IsNullOrWhiteSpace(p.TravelNormal) ? p.TravelShort : p.TravelNormal);
            if (!Duration.TryParse(src, out var run)) continue;
            var arr = cur + run;
            var last = i == pts.Count - 1;
            string newArr, newDep;
            if (last) { newArr = arr.ToString(); newDep = ""; }
            else if (p.Stop)
            {
                Duration.TryParse(p.Dwell, out var dw);
                if (dw.Tenths == 0 && ClockTime.TryParse(p.Arrival, out var oa) && ClockTime.TryParse(p.Departure, out var od)) dw = od - oa;
                newArr = arr.ToString(); newDep = (arr + dw).ToString();
            }
            else { newArr = ""; newDep = arr.ToString(); }
            if (p.Arrival != newArr || p.Departure != newDep) changed++;
            p.Arrival = newArr; p.Departure = newDep;
            cur = ClockTime.TryParse(last ? newArr : newDep, out var c2) ? c2 : arr;
        }
        return changed;
    }

    /// <summary>Wyznacza czasy jazdy (normalne) z różnicy godzin. Zwraca liczbę uzupełnionych posterunków.</summary>
    public static int RecalculateTravelTimes(Timetable t)
    {
        var pts = t.Points;
        var changed = 0;
        for (var i = 1; i < pts.Count; i++)
        {
            var prev = pts[i - 1];
            var prevStr = string.IsNullOrWhiteSpace(prev.Departure) ? prev.Arrival : prev.Departure;
            var p = pts[i];
            var curStr = (p.Stop || i == pts.Count - 1) && !string.IsNullOrWhiteSpace(p.Arrival) ? p.Arrival : (string.IsNullOrWhiteSpace(p.Departure) ? p.Arrival : p.Departure);
            if (!ClockTime.TryParse(prevStr, out var a) || !ClockTime.TryParse(curStr, out var b)) continue;
            var d = (b - a).ToString();
            if (p.TravelNormal != d) { p.TravelNormal = d; changed++; }
            if (p.Stop && ClockTime.TryParse(p.Arrival, out var pa) && ClockTime.TryParse(p.Departure, out var pd))
                p.Dwell = (pd - pa).ToString();
        }
        return changed;
    }

    /// <summary>Kopia posterunku (bez parametrów zmiany składu i numeru).</summary>
    public static RoutePoint ClonePoint(RoutePoint p) => ProjectSerializer.Clone(p);
}
