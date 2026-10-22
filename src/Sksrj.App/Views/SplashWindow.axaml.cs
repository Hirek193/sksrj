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

using Avalonia.Controls;
using Sksrj.Core;

namespace Sksrj.App.Views;

/// <summary>Ekran startowy z logo i wymaganą atrybucją autora (ADDITIONAL_TERMS, pkt 1).</summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        TaglineText.Text = AppInfo.Tagline;
        VersionText.Text = "wersja " + AppInfo.Version;
        CopyrightText.Text = $"{AppInfo.Name} · {AppInfo.Copyright}";
        AttributionText.Text = AppInfo.AttributionLine1 + "\n" + AppInfo.AttributionLine2;
        LicenseText.Text = AppInfo.LicenseShort;
    }

    public void SetStatus(string text) => StatusText.Text = text;
}
