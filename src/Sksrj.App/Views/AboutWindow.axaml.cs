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
using Avalonia.Controls;
using Avalonia.Interactivity;
using Sksrj.Core;

namespace Sksrj.App.Views;

/// <summary>Okno „O programie” – Appropriate Legal Notices (GPL v3 § 5d) z atrybucją z ADDITIONAL_TERMS.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        NameText.Text = AppInfo.Name;
        TaglineText.Text = $"{AppInfo.Tagline} · wersja {AppInfo.Version}";
        CopyrightText.Text = AppInfo.Copyright;
        AttributionText.Text = AppInfo.AttributionLine1 + "\n" + AppInfo.AttributionLine2;
        LicenseText.Text = "Ten program jest wolnym oprogramowaniem: możesz go rozpowszechniać i modyfikować na warunkach GNU General Public License w wersji 3 lub (według uznania) dowolnej późniejszej, z warunkami dodatkowymi wg § 7 (zakładka „Warunki dodatkowe”). Program jest rozpowszechniany BEZ JAKIEJKOLWIEK GWARANCJI, nawet domyślnej gwarancji przydatności handlowej lub przydatności do określonego celu.";
        DisclaimerText.Text = AppInfo.Disclaimer;
        LicenseBox.Text = Load("Sksrj.Legal.LICENSE");
        TermsBox.Text = Load("Sksrj.Legal.ADDITIONAL_TERMS");
        NoticesBox.Text = Load("Sksrj.Legal.THIRD-PARTY-NOTICES");
    }

    private static string Load(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (s == null) return "(brak pliku – zobacz repozytorium: " + AppInfo.SourceUrl + ")";
        using var r = new StreamReader(s);
        return r.ReadToEnd().Replace("\r\n", "\n");
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnSource(object? sender, RoutedEventArgs e)
    {
        try { TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(AppInfo.SourceUrl)); } catch { }
    }
}
