using Sksrj.Core.Layout;
using Sksrj.Core.Model;
using Sksrj.Core.Pdf;

// Użycie:
//   sksrj-cli <projekt.sksrj> <wyjście.pdf> [indeks-pociągu|all] [a5|a4]
//   sksrj-cli --sample <katalog>      – zapisuje projekt przykładowy i jego PDF-y
if (args.Length >= 2 && args[0] == "--sample")
{
    Directory.CreateDirectory(args[1]);
    var sp = Samples.Project();
    ProjectSerializer.Save(sp, Path.Combine(args[1], "przyklady.sksrj"));
    for (var i = 0; i < sp.Timetables.Count; i++)
    {
        var path = Path.Combine(args[1], $"przyklad{i + 1}.pdf");
        PdfExporter.ExportFile(sp.Timetables[i], sp.Print, path);
        var lr = TimetableLayout.Build(sp.Timetables[i], sp.Print, new PdfTextMeasurer());
        Console.WriteLine($"{path}: {lr.Pages.Count} stron");
        foreach (var w in lr.Warnings) Console.WriteLine("  ! " + w);
    }
    var two = new PrintSettings { Layout = PageLayoutKind.A4TwoUp };
    PdfExporter.ExportFile(sp.Timetables[1], two, Path.Combine(args[1], "przyklad2-a4.pdf"));
    return 0;
}
if (args.Length < 2) { Console.Error.WriteLine("sksrj-cli <projekt.sksrj> <wyjście.pdf> [indeks|all] [a5|a4]"); return 1; }
var project = ProjectSerializer.Load(args[0]);
if (args.Length > 3) project.Print.Layout = args[3].Equals("a4", StringComparison.OrdinalIgnoreCase) ? PageLayoutKind.A4TwoUp : PageLayoutKind.A5;
var sel = args.Length > 2 ? args[2] : "all";
var list = sel == "all" ? project.Timetables.ToList() : new List<Timetable> { project.Timetables[int.Parse(sel)] };
var m = new PdfTextMeasurer();
var cards = list.Select(t => (t, TimetableLayout.Build(t, project.Print, m))).ToList();
foreach (var (t, l) in cards) foreach (var w in l.Warnings) Console.WriteLine($"[{t.TrainNumber}] {w}");
using (var fs = File.Create(args[1])) PdfExporter.Export(cards, project.Print, fs, project.ProjectName);
Console.WriteLine($"Zapisano {args[1]}");
return 0;
