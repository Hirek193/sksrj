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

namespace Sksrj.Core.Model;

/// <summary>Projekty przykładowe.</summary>
public static class Samples
{
    public static TimetableProject Project()
    {
        var p = new TimetableProject { ProjectName = "Przykłady" };
        p.Scenery = SampleScenery.Build();
        AddSampleRestrictions(p);
        p.Timetables.Add(FromScenery(p, "ROJ", "63215", "700215", "EZT (np. EN57)", StopPolicy.AllPassenger, "06:12",
            "Koleje Fikcyjne Spółka z o.o.", "- (A) 14.XII-30.VI ;", "Kaletnik", "Rypinek", "Korty", "Leosin"));
        p.Timetables.Add(FromScenery(p, "TME", "44210", "744210", "Pociąg towarowy ciężki (np. ET22 + 2000 t)", StopPolicy.None, "10:05",
            "Cargo Fikcyjne S.A.", "- (1)-(7) 14.XII-12.XII ;", "kon3", "Sosiny", "kon2"));
        p.Timetables.Add(Fictional());
        p.Timetables.Add(Ir11Example());
        return p;
    }

    private static void AddSampleRestrictions(TimetableProject p)
    {
        p.Restrictions.Add(new SpeedRestriction { Kind = RestrictionKind.Permanent, Line = "201", KmFrom = "18.200", KmTo = "19.100", Speed = "80", Reason = "obiekt inżynieryjny – most na rzece Wkrze" });
        p.Restrictions.Add(new SpeedRestriction { Kind = RestrictionKind.Permanent, Line = "201", KmFrom = "80.500", KmTo = "81.300", Speed = "70", Reason = "łuk o małym promieniu" });
        p.Restrictions.Add(new SpeedRestriction { Kind = RestrictionKind.Temporary, Line = "201", KmFrom = "34.300", KmTo = "34.800", Speed = "40", Track = RestrictionTrack.Track1, Reason = "zły stan nawierzchni", ValidFrom = "01.10.2026", ValidTo = "31.12.2026" });
        p.Restrictions.Add(new SpeedRestriction { Kind = RestrictionKind.Temporary, Line = "204", KmFrom = "8.000", KmTo = "8.600", Speed = "30", Reason = "usterka rozjazdu", ValidFrom = "15.10.2026" });
    }

    /// <summary>Karta pociągu z trasą ułożoną na schemacie (punkty trasy – nazwy posterunków lub klucze).</summary>
    public static Timetable FromScenery(TimetableProject p, string cat, string num, string sys, string preset, StopPolicy policy, string dep, string carrier, string days, params string[] waypoints)
    {
        var sc = p.Scenery;
        var t = new Timetable
        {
            TrainCategory = cat, TrainNumber = num, SystemNumber = sys, Carrier = carrier, RunningDays = days,
            ApplicationNumber = $"KF-2027/{num[^3..]}", ApplicationDate = "14.04.2026", OrderNumber = $"RRJ_2027_{sys}_15", OrderDate = "02.10.2026",
        };
        var pr = TractionPresets.Find(preset);
        if (pr != null)
        {
            t.RunCalc.Preset = pr.Name;
            t.RunCalc.StartAcceleration = pr.A0.ToString(System.Globalization.CultureInfo.InvariantCulture);
            t.RunCalc.PowerPerTon = pr.PowerPerTon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            t.RunCalc.Braking = pr.Braking.ToString(System.Globalization.CultureInfo.InvariantCulture);
            t.RunCalc.ResistanceA = pr.ResA.ToString(System.Globalization.CultureInfo.InvariantCulture);
            t.RunCalc.ResistanceC = pr.ResC.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        t.Route.StopPolicy = policy;
        // klucze kon1..kon3 oznaczają kolejne „Koniec scenerii”
        var ends = sc.Nodes.Where(n => n.Name == "Koniec scenerii").ToList();
        foreach (var w in waypoints)
        {
            SceneryNode? n = w.StartsWith("kon") && int.TryParse(w[3..], out var k) && k <= ends.Count ? ends[k - 1]
                : sc.Nodes.FirstOrDefault(x => string.Equals(x.Name, w, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(x.TypeSuffix))
                  ?? sc.Nodes.FirstOrDefault(x => string.Equals(x.Name, w, StringComparison.OrdinalIgnoreCase));
            if (n != null) t.Route.Waypoints.Add(n.Id);
        }
        var route = Engine.SceneryRouter.FindRoute(sc, t.Route.Waypoints.ToList());
        Engine.SceneryRouter.ApplyToTimetable(sc, t, route);
        if (t.Points.Count > 0)
        {
            t.Points[0].Departure = dep;
            if (cat.StartsWith("T")) { t.Points[0].Loco1 = "ET22"; t.Points[0].LocoLoad = "2000"; t.Points[0].TrainLength = "560"; t.Points[0].Vmax = "70"; t.Points[0].BrakePercent = "48"; t.Points[0].BrakeType = "G"; }
            else { t.Points[0].LocoLoad = "126"; t.Points[0].BrakePercent = "64"; }
        }
        var res = Engine.RunTimeCalculator.Calculate(t, Engine.RestrictionTools.Effective(p));
        Engine.RunTimeCalculator.Apply(t, res);
        return t;
    }

    public static Timetable NewEmpty()
    {
        var t = new Timetable { TrainCategory = "ROJ", TrainNumber = "", Carrier = "" };
        var sec = new LineSection { LineNumber = "1", KmStart = "0.000", KmEnd = "10.000" };
        sec.Changes.Add(new SpeedChange { Km = "0.000", Vp = "80", Vl = "80" });
        t.Sections.Add(sec);
        t.Points.Add(new RoutePoint { Name = "Stacja A", Bold = true, Km = "0.000", Departure = "08:00", Stop = true });
        t.Points.Add(new RoutePoint { Name = "Stacja B", Bold = true, Km = "10.000", Arrival = "08:10", TravelNormal = "10", Stop = true });
        return t;
    }

    private static SpeedChange S(string km, string vp, string vl, bool single = false) => new() { Km = km, Vp = vp, Vl = vl, SingleTrack = single };

    private static LineSection L(string line, string k0, string k1, TrackSide side, params SpeedChange[] ch)
    {
        var s = new LineSection { LineNumber = line, KmStart = k0, KmEnd = k1, Track = side };
        foreach (var c in ch) s.Changes.Add(c);
        return s;
    }

    /// <summary>Pociąg na fikcyjnej trasie – pokazuje wszystkie elementy karty.</summary>
    public static Timetable Fictional()
    {
        var t = new Timetable
        {
            TrainCategory = "ROJ",
            TrainNumber = "63215",
            SystemNumber = "700215",
            RelationFrom = "Kolejowo Główne",
            RelationTo = "Rudnik Wielki",
            TimetableKind = "ROCZNY ROZKŁAD JAZDY",
            Carrier = "Koleje Fikcyjne Spółka z o.o.",
            ApplicationNumber = "KF-2027/015",
            ApplicationDate = "14.04.2026",
            OrderNumber = "RRJ_2027_700215_15",
            OrderDate = "02.10.2026",
            RunningDays = "- (A) 14.XII-30.VI ;\n- (C) 4.VII-31.VIII ;",
            Remarks = "",
        };
        t.Footnotes.Add(new Footnote { Marker = "*1)", Text = "wyjazd w kierunku Zalesia Dolnego wyłącznie z toru nr 4" });
        t.Footnotes.Add(new Footnote { Marker = "*2)", Text = "jazda po torze lewym na podstawie rozkazu pisemnego „S” – zamknięcie toru nr 1" });

        t.Sections.Add(L("901", "0.400", "12.400", TrackSide.Right,
            S("0.400", "60", "60"), S("1.850", "100", "100"), S("9.200", "120", "100")));
        t.Sections.Add(L("901", "12.400", "24.500", TrackSide.Left,
            S("12.400", "120", "100"), S("17.600", "100", "100"), S("23.900", "60", "60")));
        t.Sections.Add(L("902", "0.000", "31.250", TrackSide.Right,
            S("0.000", "60", "", true), S("0.950", "90", "", true), S("14.300", "70", "", true),
            S("15.100", "90", "", true), S("30.600", "40", "", true)));

        t.Points.Add(new RoutePoint
        {
            Name = "Kolejowo Główne", Bold = true, Uppercase = true, StopKinds = "ph", Km = "0.400",
            Abbreviations = "ZSM KG,\nR1, RT, H, SS", Departure = "08:12", Stop = true,
            Loco1 = "EN57", LocoLoad = "126", TrainLength = "65", Vmax = "110", BrakePercent = "64", BrakeType = "P",
        });
        t.Points.Add(new RoutePoint { Name = "Kolejowo Towarowe", TypeSuffix = "podg", Km = "2.315", Abbreviations = "ZS KG, R1, H, SS", Departure = "08:15.3", TravelNormal = "3.3", TravelShort = "3.0" });
        t.Points.Add(new RoutePoint { Name = "Brzozowa Wola", TypeSuffix = "po", StopKinds = "ph", Km = "6.870", Abbreviations = "R1, H, SS", Arrival = "08:19", Departure = "08:19.5", Stop = true, Dwell = "0.5", TravelNormal = "3.7", TravelShort = "3.4" });
        t.Points.Add(new RoutePoint { Name = "Lipowiec", TypeSuffix = "pe", Km = "12.400", Abbreviations = "*2), APO KG, H", Departure = "08:23.8", TravelNormal = "4.3", TravelShort = "4.0" });
        t.Points.Add(new RoutePoint
        {
            Name = "Mostki", Bold = true, StopKinds = "ph,zd", Km = "24.120", NoteBottom = "24.500/0.000",
            Abbreviations = "*1), R1, R5, RT, H, SS", Arrival = "08:31.5", Departure = "08:34", Stop = true, Dwell = "2.5",
            TravelNormal = "7.7", TravelShort = "7.2", TrainNumber = "63217", TracksToNext = TrackCount.Single,
        });
        t.Points.Add(new RoutePoint { Name = "Mostki Wschodnie", TypeSuffix = "podg", Km = "1.240", Abbreviations = "R5, H", Departure = "08:35.9", TravelNormal = "1.9", TracksToNext = TrackCount.Single });
        t.Points.Add(new RoutePoint { Name = "Zalesie Dolne", Bold = true, StopKinds = "ph", Km = "14.700", Abbreviations = "R5, RT, H, Rd1", Arrival = "08:46.2", Departure = "08:47", Stop = true, Dwell = "0.8", TravelNormal = "10.3", TravelShort = "9.7", TracksToNext = TrackCount.Single });
        t.Points.Add(new RoutePoint { Name = "Kamionka", TypeSuffix = "po", StopKinds = "ph", Km = "22.050", Abbreviations = "R5, H, NZ", Arrival = "08:52.4", Departure = "08:53", Stop = true, Dwell = "0.6", TravelNormal = "5.4", TravelShort = "5.0", BrakePercent = "58", TracksToNext = TrackCount.Single });
        t.Points.Add(new RoutePoint { Name = "Rudnik Wielki", Bold = true, Uppercase = true, StopKinds = "ph", Km = "31.250", Abbreviations = "R5, R7, RT, H", Arrival = "09:01.6", Stop = true, TravelNormal = "8.6", TravelShort = "8.1", TracksToNext = TrackCount.Single });
        return t;
    }

    /// <summary>Odwzorowanie przykładowej karty z Załącznika 5 do Ir-11 (strony 2–3) – do porównania wyglądu.</summary>
    public static Timetable Ir11Example()
    {
        var t = new Timetable
        {
            TrainCategory = "EIJ",
            TrainNumber = "1500/1",
            SystemNumber = "114144",
            RelationFrom = "Warszawa Grochów",
            RelationTo = "Gdynia Główna",
            TimetableKind = "RRJ - KOREKTA PARAMETRÓW LINII",
            Carrier = "PKP INTERCITY Spółka Akcyjna",
            ApplicationNumber = "BBPR2-222",
            ApplicationDate = "09.12.2020",
            OrderNumber = "ZRJP_2021-114144_2-1",
            OrderDate = "03.02.2021",
            UseCurrentGenerationTime = false,
            GenerationDate = "24.03.2021",
            GenerationTime = "12:27:35",
            RunningDays = "- 14.III-25.III ;",
        };
        t.Footnotes.Add(new Footnote { Marker = "*1)", Text = "południowy okręg nastawczy stacji" });
        t.Footnotes.Add(new Footnote { Marker = "*2)", Text = "zachodni okręg nastawczy stacji" });

        t.Sections.Add(L("545", "2.234", "0.346", TrackSide.Right, S("2.234", "60", "", true)));
        t.Sections.Add(L("9", "6.754", "8.317", TrackSide.Right, S("6.754", "80", "100"), S("7.500", "100", "100")));
        t.Sections.Add(L("501", "1.558", "-0.146", TrackSide.Right, S("1.558", "60", "", true), S("1.136", "60", "60"), S("-0.030", "60", "", true)));
        t.Sections.Add(L("20", "12.493", "10.880", TrackSide.Right, S("12.493", "60", "60"), S("12.440", "80", "80"), S("11.920", "80", "100"), S("11.360", "80", "80")));
        t.Sections.Add(L("509", "10.256", "-0.600", TrackSide.Right, S("10.256", "80", "", true), S("10.247", "80", "80")));
        t.Sections.Add(L("20", "-0.600", "3.587", TrackSide.Right, S("-0.600", "40", "", true), S("0.222", "40", "40"), S("1.374", "40", "0"), S("3.480", "40", "40")));
        t.Sections.Add(L("46", "4.350", "3.365", TrackSide.Right, S("4.350", "60", "60"), S("3.550", "60", "", true)));
        t.Sections.Add(L("3", "3.365", "3.082", TrackSide.Right, S("3.365", "90", "", true)));
        t.Sections.Add(L("1", "3.082", "2.620", TrackSide.Right, S("3.082", "90", "90")));
        t.Sections.Add(L("2", "-2.620", "4.407", TrackSide.Right, S("-2.620", "80", "85"), S("-0.600", "60", "60"), S("0.700", "80", "80"), S("3.450", "60", "60")));

        const string ed = "ED250";
        t.Points.Add(new RoutePoint { Name = "Wwa Grochów", TypeSuffix = "stth", Km = "2.234", Abbreviations = "R2, H, S", Departure = "05:38", TrainCategory = "PWJ", TrainNumber = "01500", TracksToNext = TrackCount.Single, Loco1 = ed, LocoLoad = "414", TrainLength = "187", Vmax = "200", BrakePercent = "56", BrakeType = "P" });
        t.Points.Add(new RoutePoint { Name = "Wwa Praga WPT M", TypeSuffix = "pzs", NoteTop = "0.346/6.754", Km = "7.094", Abbreviations = "*1), R1, H", Departure = "05:41", TravelNormal = "3", TravelShort = "2.7", TrainNumber = "01501" });
        t.Points.Add(new RoutePoint { Name = "Wwa Praga R15", TypeSuffix = "pzs", Km = "8.317/1.558", Departure = "05:43", TravelShort = "2", TrainNumber = "01500" });
        t.Points.Add(new RoutePoint { Name = "Wwa Praga WPT J", TypeSuffix = "pzs", Km = "0.000", Abbreviations = "*2), R1, H, SS", NoteBottom = "-0.146/12.493", Departure = "05:45", TravelNormal = "2", TravelShort = "1.7" });
        t.Points.Add(new RoutePoint { Name = "Wwa Gdańska WG", Bold = true, NoteTop = "10.880/10.256", Km = "9.172", Abbreviations = "RT, H", Departure = "05:48.5", TravelShort = "3.5" });
        t.Points.Add(new RoutePoint { Name = "Wwa Gdańska GT", TypeSuffix = "gt", StopKinds = "pt", Km = "8.700", Abbreviations = "SS", Arrival = "05:49.5", Departure = "06:23.5", Stop = true, TravelNormal = "1", TravelShort = "0.6" });
        t.Points.Add(new RoutePoint { Name = "Warszawa Jelonki", Bold = true, Km = "3.500", Abbreviations = "ZSM WGT,\nR3, RT, H, PP", Departure = "06:28.1", TravelShort = "4.6" });
        t.Points.Add(new RoutePoint { Name = "Wwa GłT GT22-39", TypeSuffix = "gt", Km = "1.000", Departure = "06:30", TravelShort = "1.9" });
        t.Points.Add(new RoutePoint { Name = "Wwa Gł Tow", Uppercase = true, TypeSuffix = "stth", StopKinds = "pt", Km = "-0.600", Abbreviations = "R3, RT, UZS, H", Arrival = "06:31.5", Departure = "07:00.5", Stop = true, TravelShort = "1.5", TrainNumber = "01501", TracksToNext = TrackCount.Single });
        t.Points.Add(new RoutePoint { Name = "Wwa GłT GT81-85", TypeSuffix = "gt", Km = "0.600", Departure = "07:02.7", TravelShort = "2.2" });
        t.Points.Add(new RoutePoint { Name = "Wwa Czyste", TypeSuffix = "stth", Km = "3.587/4.350", Abbreviations = "R2, H", Departure = "07:07.8", TravelShort = "5.1", TrainNumber = "01500" });
        t.Points.Add(new RoutePoint { Name = "Wwa Zachodnia", Bold = true, StopKinds = "pn,pm,ph", NoteTop = "3.365/3.365", Km = "3.082", Abbreviations = "R2, R3, R5, RT, H, SS", NoteBottom = "2.620/-2.620", Arrival = "07:10", Departure = "07:11", Stop = true, TravelNormal = "2.2", TravelShort = "2.1", TrainCategory = "EIJ", TrainNumber = "1501", LocoLoad = "445", BrakePercent = "153" });
        t.Points.Add(new RoutePoint { Name = "Wwa Centralna", Bold = true, StopKinds = "ph", Km = "0.000", Abbreviations = "R2, RT, H, SS", Arrival = "07:16", Departure = "07:20", Stop = true, TravelNormal = "5", TravelShort = "4.8" });
        t.Points.Add(new RoutePoint { Name = "Wwa Wschodnia", Bold = true, Uppercase = true, StopKinds = "ph", Km = "4.254", Abbreviations = "R6, R7, R1,\nR2, R307, RT, H, PP", NoteBottom = "4.407/4.407", Arrival = "07:26", Stop = true, TravelNormal = "6", TravelShort = "5.7" });
        return t;
    }
}
