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

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Sksrj.App.ViewModels;
using Sksrj.App.Views;
using Sksrj.Core.Model;
using Sksrj.Core.Pdf;

namespace Sksrj.App;

public partial class App : Application
{
    /// <summary>Minimalny czas wyświetlania ekranu startowego (atrybucja musi być czytelna).</summary>
    private static readonly TimeSpan MinSplash = TimeSpan.FromSeconds(2.2);

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow();
            desktop.MainWindow = splash;
            splash.Show();
            var args = desktop.Args ?? Array.Empty<string>();
            Dispatcher.UIThread.Post(() => _ = StartAsync(desktop, splash, args), DispatcherPriority.Background);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop, SplashWindow splash, string[] args)
    {
        var sw = Stopwatch.StartNew();
        TimetableProject? project = null; string? path = null; string? error = null;
        var file = args.FirstOrDefault(File.Exists);
        await Task.Run(() =>
        {
            // rozgrzanie czcionek PDF i przygotowanie projektu w tle
            EmbeddedFontResolver.Install();
            _ = new PdfTextMeasurer().Width("SKSRJ", new Sksrj.Core.Layout.FontSpec(10));
            try
            {
                if (file != null) { project = ProjectSerializer.Load(file); path = file; }
            }
            catch (Exception ex) { error = ex.Message; }
            project ??= Samples.Project();
        });
        splash.SetStatus("Przygotowywanie interfejsu…");
        await Task.Delay(30);

        var window = new MainWindow();
        var vm = new MainViewModel(new Services.DialogService(window), project, path);
        window.DataContext = vm;

        var rest = MinSplash - sw.Elapsed;
        if (rest > TimeSpan.Zero) await Task.Delay(rest);

        desktop.MainWindow = window;
        window.Show();
        splash.Close();
        if (error != null) vm.ShowMessage("Nie udało się otworzyć pliku: " + error);
    }
}
