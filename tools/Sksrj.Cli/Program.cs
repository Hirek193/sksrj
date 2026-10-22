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
using Sksrj.Core.Layout;
using Sksrj.Core.Model;
using Sksrj.Core.Pdf;

// Użycie:
//   sksrj-cli <projekt.sksrj> <wyjście.pdf> [indeks-pociągu|all] [a5|a4]
//   sksrj-cli --scheme <projekt.sksrj> <wyjście.pdf>   – plan schematyczny scenerii
//   sksrj-cli --sample <katalog>                       – projekt przykładowy i jego PDF-y
if (args.Length >= 2 && args[0] == "--sample")
{
    Directory.CreateDirectory(args[1]);
    var sp = Samples.Project();
    ProjectSerializer.Save(sp, Path.Combine(args[1], "przyklady.sksrj"));
    for (var i = 0; i < sp.Timetables.Count; i++)
    {
        var path = Path.Combine(args[1], $"przyklad{i + 1}.pdf");
        PdfExporter.ExportFile(sp.Timetables[i], sp.Print, path, RestrictionTools.Effective(sp));
        var lr = TimetableLayout.Build(sp.Timetables[i], sp.Print, new PdfTextMeasurer(), null, RestrictionTools.Effective(sp));
        Console.WriteLine($"{path}: {lr.Pages.Count} stron");
        foreach (var w in lr.Warnings) Console.WriteLine("  ! " + w);
        var rt = RunTimeCalculator.Calculate(sp.Timetables[i], RestrictionTools.Effective(sp));
        if (rt.Ok) Console.WriteLine($"  {rt.TotalKm:0.0} km, A={rt.TotalA:0.0} B={rt.TotalB:0.0} C={rt.TotalC:0.0} D={rt.TotalD:0.0} min, ograniczeń: {rt.Restrictions.Count}");
    }
    using (var fs = File.Create(Path.Combine(args[1], "schemat.pdf"))) PdfExporter.ExportScheme(sp.Scenery, fs);
    return 0;
}
if (args.Length >= 3 && args[0] == "--scheme")
{
    var pr = ProjectSerializer.Load(args[1]);
    using var fs = File.Create(args[2]);
    PdfExporter.ExportScheme(pr.Scenery, fs);
    return 0;
}
if (args.Length < 2) { Console.Error.WriteLine("sksrj-cli <projekt.sksrj> <wyjście.pdf> [indeks|all] [a5|a4]"); return 1; }
var project = ProjectSerializer.Load(args[0]);
if (args.Length > 3) project.Print.Layout = args[3].Equals("a4", StringComparison.OrdinalIgnoreCase) ? PageLayoutKind.A4TwoUp : PageLayoutKind.A5;
var sel = args.Length > 2 ? args[2] : "all";
var list = sel == "all" ? project.Timetables.ToList() : new List<Timetable> { project.Timetables[int.Parse(sel)] };
var m = new PdfTextMeasurer();
var cards = list.Select(t => (t, TimetableLayout.Build(t, project.Print, m, null, RestrictionTools.Effective(project)))).ToList();
foreach (var (t, l) in cards) foreach (var w in l.Warnings) Console.WriteLine($"[{t.TrainNumber}] {w}");
using (var fs = File.Create(args[1])) PdfExporter.Export(cards, project.Print, fs, project.ProjectName);
Console.WriteLine($"Zapisano {args[1]}");
return 0;
