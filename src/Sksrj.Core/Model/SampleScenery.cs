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

using Sksrj.Core.Engine;

namespace Sksrj.Core.Model;

/// <summary>Przykładowa sceneria – odwzorowanie schematu Kaletnik – Rypinek – Leosin – Sosiny.</summary>
public static class SampleScenery
{
    private sealed record N(string Key, string Name, string Suffix, double X, double Y, NodeKind Kind, LabelSide Side, bool Pass, bool Tech, string Label = "", bool Upper = false, bool Air = false);

    public static Scenery Build()
    {
        var sc = new Scenery { Name = "Sceneria Leosin", KmPerUnit = 0.03, GridSize = 13 };
        const NodeKind S = NodeKind.Station, T = NodeKind.Stop, C = NodeKind.Closed, E = NodeKind.End;
        const LabelSide Top = LabelSide.Top, Bot = LabelSide.Bottom, Lf = LabelSide.Left, Rt = LabelSide.Right;
        var nodes = new N[]
        {
            // linia 201 – górny ciąg
            new("kal", "Kaletnik", "", 38, 66, S, Top, true, false),
            new("kals", "Kaletnik Śródmieście", "", 118, 66, T, Top, true, false),
            new("mly", "Młynów", "", 197, 66, T, Top, true, false),
            new("mlyp", "Młynów", "podg", 276, 66, T, Top, false, true),
            new("emi", "Emilianowo", "stth", 354, 66, S, Top, false, true),
            new("gro", "Grodzisk", "", 433, 66, T, Top, true, false),
            new("tom", "Tomaszewo", "", 512, 66, S, Top, true, false),
            new("tomz", "Tomaszewo Zalew", "", 591, 66, T, Top, true, false),
            new("cze", "Czerepy", "podg", 670, 66, T, Top, false, true),
            new("tomw", "Tomaszewo Wschodnie", "", 749, 66, T, Top, true, false),
            new("kom", "Komnice", "", 827, 66, S, Top, true, false),
            new("bro", "Brody", "", 907, 66, T, Top, true, false),
            new("wie", "Wielichowo", "", 986, 66, T, Top, true, false),
            new("wit", "Witnica", "stth", 1065, 66, S, Top, false, true),
            new("witm", "Witnica Miasto", "", 1144, 66, T, Top, true, false),
            new("kam", "Kamienna Wola", "", 1222, 66, T, Top, true, false),
            new("chr", "Chrusty", "", 1301, 66, T, Top, true, false),
            new("sadp", "Sadkowice", "podg", 1380, 66, T, Top, false, true),
            new("sad", "Sadkowice", "", 1459, 66, T, Top, true, false),
            new("gra", "Grabina", "", 1538, 66, S, Top, true, false),
            new("gral", "Grabina Las", "", 1617, 66, T, Top, true, false),
            new("lip", "Lipka", "pb", 1696, 66, T, Top, false, true),
            new("rypm", "Rypinek Miasto", "", 1775, 66, T, Top, true, false),
            new("rypb", "Rypinek Brzesk", "podg", 1854, 66, T, Top, false, true),
            new("ryp", "Rypinek", "", 1933, 66, S, Top, true, false),
            new("grab", "Grabowiec", "", 1933, 197, T, Bot, true, false),
            // linia 201 – dolny ciąg
            new("kacf", "Kaczyce Fabryczne", "", 1854, 197, T, Bot, true, false),
            new("kac", "Kaczyce", "", 1775, 197, S, Bot, true, false),
            new("byc", "Byczyna", "", 1696, 197, T, Bot, true, false),
            new("orzm", "Orzechowo Miasto", "", 1617, 197, S, Bot, true, false),
            new("orz", "Orzechowo", "", 1538, 197, S, Bot, true, false),
            new("nzim", "Nowa Zimnica", "", 1459, 197, T, Bot, true, false),
            new("bor", "Borkowice", "", 1380, 197, S, Bot, true, false),
            new("kor", "Korty", "podg", 1301, 197, S, Top, false, true),
            new("leo", "Leosin", "", 907, 197, S, Bot, true, false, "", true),
            new("kon1", "Koniec scenerii", "", 749, 327, S, Lf, false, true, "Koniec\nscenerii"),
            // linia 202
            new("kty", "Korytowo", "", 38, 145, S, Top, true, false),
            new("ktyc", "Korytowo - Centrum", "", 118, 145, S, Top, true, false, "Korytowo -\nCentrum"),
            new("ktyr", "Korytowo Rynek", "", 38, 197, T, Bot, true, false),
            new("jan", "Janowice", "", 118, 197, T, Bot, true, false),
            new("sos", "Sosnowa", "", 197, 197, T, Bot, true, false),
            new("ltop", "Leosin Topola", "", 276, 197, S, Bot, true, false),
            new("llip", "Leosin Lipa", "", 354, 197, T, Bot, true, false),
            new("lstr", "Leosin Strzelecka", "", 433, 197, T, Bot, true, false),
            new("lpot", "Leosin Potockich", "", 512, 197, T, Bot, true, false),
            new("lpol", "Leosin Polesie", "", 591, 197, S, Bot, true, false),
            new("lrez", "Leosin Rezerwat", "", 670, 197, T, Bot, true, false),
            new("lsie", "Leosin Siennicka", "", 749, 197, T, Bot, true, false),
            new("lpos", "Leosin Postojowy", "", 827, 197, S, Bot, false, true),
            new("ljer", "Leosin Jerzyce", "", 986, 197, T, Top, true, false),
            new("lzaw", "Leosin Zawodzie", "", 1065, 197, T, Top, true, false),
            new("lzac", "Leosin Zachodni", "", 1144, 197, T, Top, true, false),
            new("slg", "Stary Leosin", "", 1222, 197, T, Top, true, false),
            new("ras", "Raszewy", "", 1327, 277, T, Top, true, false),
            new("lig", "Ligota", "", 1407, 277, T, Top, false, true),
            new("bar", "Baranowicze", "", 1486, 277, S, Top, true, false),
            new("rad", "Radzieszyn", "", 1565, 277, T, Top, true, false),
            new("str", "Strzegomiany", "", 1644, 277, S, Top, true, false),
            new("dawj", "Dawidy Jezioro", "", 1723, 277, T, Top, true, false),
            new("dawp", "Dawidy Potok", "", 1802, 277, T, Top, true, false),
            new("daw", "Dawidy", "", 1854, 277, S, Top, true, false),
            new("sosp", "Sosiny", "podg", 1907, 277, S, Rt, false, true),
            new("kon2", "Koniec scenerii", "", 1933, 434, S, Bot, false, true, "Koniec\nscenerii"),
            new("dawl", "Dawidy Lotnisko", "", 1854, 302, S, Bot, true, false, "Dawidy Lotnisko\nDawidy Airport", false, true),
            // linia 207
            new("nie", "Nieszawa", "", 1538, 145, T, Top, true, false),
            new("npp", "Nowe Podole", "pb", 1617, 145, C, Top, false, true),
            new("npo", "Nowe Podole", "", 1696, 145, T, Top, true, false),
            new("pod", "Podole", "", 1775, 145, S, Top, true, false),
            // linia 203
            new("osi", "Osiny", "", 907, 145, T, Top, true, false),
            new("bog", "Boguszyce", "", 986, 145, S, Top, true, false),
            // bocznica 205
            new("lpto", "Leosin Postojowy tory odstawcze", "", 749, 145, T, Top, false, true, "Leosin Postojowy\ntory odstawcze"),
            new("lplk", "Leosin Postojowy lokomotywownia", "", 670, 145, E, Lf, false, true, "Leosin Postojowy\nlokomotywownia"),
            // linia 204
            new("kon3", "Koniec scenerii", "", 1160, 381, E, Lf, false, true, "Koniec\nscenerii"),
            new("pog", "Pogoria", "", 1249, 381, S, Top, true, false),
            new("jaw", "Jaworzno", "", 1328, 381, T, Top, true, false),
            new("jawo", "Jaworzno", "odst", 1407, 381, T, Top, false, true),
            new("fil", "Filipowice", "", 1486, 381, S, Top, true, false),
            new("pia", "Piaski", "", 1565, 381, T, Top, true, false),
            new("ost", "Ostrowy", "", 1644, 381, T, Top, true, false),
            new("osto", "Ostrowy", "odst", 1723, 381, T, Top, false, true),
            new("czr", "Czernica", "", 1802, 381, S, Top, true, false),
            new("mro", "Mroczyska", "", 1854, 381, C, Top, false, false),
        };
        var ids = new Dictionary<string, SceneryNode>();
        foreach (var n in nodes)
        {
            var node = new SceneryNode
            {
                Name = n.Name, TypeSuffix = n.Suffix, X = n.X, Y = n.Y, Kind = n.Kind, LabelSide = n.Side, Passenger = n.Pass, Technical = n.Tech,
                Label = n.Label, Uppercase = n.Upper, Airport = n.Air, Bold = n.Kind == S && n.Pass || n.Upper,
                DefaultStopKinds = n.Pass ? "ph" : "",
            };
            ids[n.Key] = node;
            sc.Nodes.Add(node);
        }

        SceneryLine L(string num, string name, string color, string v, TrackCount tr, bool dashed = false)
        {
            var l = new SceneryLine { Number = num, Name = name, Color = color, DefaultVp = v, DefaultVl = v, DefaultTracks = tr, Dashed = dashed };
            sc.Lines.Add(l);
            return l;
        }
        var red = L("201", "Kaletnik – Leosin", "#E30613", "120", TrackCount.Double);
        var green = L("202", "Korytowo – Leosin – Sosiny", "#00692F", "80", TrackCount.Double);
        var blue = L("203", "Leosin – Boguszyce", "#1037FF", "50", TrackCount.Single);
        var orange = L("204", "Pogoria – Sosiny", "#D0661A", "80", TrackCount.Single);
        var grey = L("205", "Bocznica Leosin Postojowy", "#8A8A8A", "20", TrackCount.Single, true);
        var lgrey = L("206", "Czernica – Koniec scenerii", "#A6A6A6", "60", TrackCount.Single);
        var g207 = L("207", "Orzechowo – Podole", "#00692F", "70", TrackCount.Single);
        var g208 = L("208", "Dawidy Potok – Dawidy Lotnisko", "#00692F", "60", TrackCount.Single);
        var g209 = L("209", "Korytowo – Korytowo Centrum", "#00692F", "40", TrackCount.Single);

        SceneryEdge Ed(SceneryLine l, string a, string b, EdgeStyle st = EdgeStyle.DiagonalFirst, TrackCount? tr = null, string blocking = "", params (double x, double y)[] via)
        {
            var e = new SceneryEdge { LineId = l.Id, NodeA = ids[a].Id, NodeB = ids[b].Id, Style = st, Tracks = tr ?? l.DefaultTracks, Blocking = blocking };
            foreach (var (x, y) in via) e.Via.Add(new ViaPoint { X = x, Y = y });
            sc.Edges.Add(e);
            return e;
        }
        void Chain(SceneryLine l, string blocking, TrackCount? tr, params string[] keys) { for (var i = 1; i < keys.Length; i++) Ed(l, keys[i - 1], keys[i], EdgeStyle.DiagonalFirst, tr, blocking); }

        Chain(red, "SS", null, "kal", "kals", "mly", "mlyp", "emi", "gro", "tom", "tomz", "cze", "tomw", "kom", "bro", "wie", "wit", "witm", "kam", "chr", "sadp", "sad", "gra", "gral", "lip", "rypm", "rypb", "ryp");
        var curve = Ed(red, "ryp", "grab", EdgeStyle.DiagonalFirst, null, "SS");
        Chain(red, "SS", null, "grab", "kacf", "kac", "byc", "orzm", "orz", "nzim", "bor", "kor");
        var bypass = Ed(red, "kor", "leo", EdgeStyle.StraightFirst, null, "SS", (1250, 222), (932, 222));
        var leoOut = Ed(red, "leo", "kon1", EdgeStyle.DiagonalFirst, null, "SS");

        Ed(green, "kty", "ktyr", EdgeStyle.DiagonalFirst, null, "S");
        Chain(green, "S", null, "ktyr", "jan", "sos", "ltop", "llip", "lstr", "lpot", "lpol", "lrez", "lsie", "lpos", "leo", "ljer", "lzaw", "lzac", "slg", "kor");
        Ed(green, "kor", "ras", EdgeStyle.StraightFirst, TrackCount.Single);
        Chain(green, "", TrackCount.Single, "ras", "lig", "bar", "rad", "str", "dawj", "dawp", "daw", "sosp");
        Ed(green, "sosp", "kon2", EdgeStyle.DiagonalFirst, TrackCount.Single);
        Ed(g209, "kty", "ktyc");
        Ed(g207, "orz", "nie"); Chain(g207, "", null, "nie", "npp", "npo", "pod");
        Ed(g208, "dawp", "dawl", EdgeStyle.DiagonalFirst);
        Ed(blue, "leo", "osi"); Ed(blue, "osi", "bog");
        var sid1 = Ed(grey, "lpos", "lpto", EdgeStyle.DiagonalFirst); var sid2 = Ed(grey, "lpto", "lplk");
        sid1.AvoidInRouting = true; sid2.AvoidInRouting = true; sid1.Electrified = false; sid2.Electrified = false;
        Chain(orange, "", null, "kon3", "pog", "jaw", "jawo", "fil", "pia", "ost", "osto", "czr", "mro");
        Ed(orange, "mro", "sosp", EdgeStyle.HorizontalFirst);
        Ed(lgrey, "czr", "kon2", EdgeStyle.DiagonalFirst, null, "", (1907, 407));

        // kilometraż wg długości rysunku
        SceneryRouter.AutoKilometrage(sc, red, ids["kal"].Id);
        SceneryRouter.AutoKilometrage(sc, green, ids["kty"].Id);
        SceneryRouter.AutoKilometrage(sc, orange, ids["kon3"].Id);
        foreach (var l in new[] { blue, grey, lgrey, g207, g208, g209 }) SceneryRouter.AutoKilometrage(sc, l);

        // prędkości szczegółowe
        curve.Changes.Add(new SpeedChange { Km = curve.KmA, Vp = "100", Vl = "100" });
        bypass.Changes.Add(new SpeedChange { Km = bypass.KmA, Vp = "100", Vl = "100" });
        if (Util.Num.TryParseKm(bypass.KmB, out var kb)) bypass.Changes.Add(new SpeedChange { Km = Util.Num.KmDot(kb - 1.2), Vp = "60", Vl = "60" });
        leoOut.Changes.Add(new SpeedChange { Km = leoOut.KmA, Vp = "60", Vl = "60" });
        if (Util.Num.TryParseKm(leoOut.KmA, out var lo)) leoOut.Changes.Add(new SpeedChange { Km = Util.Num.KmDot(lo + 1.5), Vp = "100", Vl = "100" });

        // metadane posterunków (radiotelefony, SHP, zdalne sterowanie)
        void Meta(string key, string abbr, string dwell = "") { ids[key].Abbreviations = abbr; if (dwell.Length > 0) ids[key].DefaultDwell = dwell; }
        foreach (var k in new[] { "kal", "emi", "tom", "kom", "wit", "gra", "ryp", "kac", "orzm", "orz", "bor" }) Meta(k, "R1, H");
        foreach (var k in new[] { "mlyp", "cze", "sadp", "lip", "rypb" }) Meta(k, "R1");
        Meta("kor", "ZSM LE, R1, R2, H"); Meta("leo", "R1, R2, RT, H", "1");
        foreach (var k in new[] { "kty", "ltop", "lpol", "lpos", "bar", "str", "daw", "sosp", "dawl", "pod", "bog" }) Meta(k, "R2, H");
        foreach (var k in new[] { "pog", "fil", "czr" }) Meta(k, "R3, H");
        foreach (var k in new[] { "jawo", "osto" }) Meta(k, "R3");
        ids["npp"].Abbreviations = "Z"; ids["mro"].Abbreviations = "Z";
        ids["leo"].Notes = "Stacja węzłowa – 8 krawędzi peronowych, nastawnia LE.";
        ids["leo"].Platforms = "4 perony / 8 krawędzi";
        return sc;
    }
}
