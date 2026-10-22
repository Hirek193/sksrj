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
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Sksrj.App.ViewModels;
using Sksrj.Core.Model;

namespace Sksrj.App.Views;

public partial class SchemeInspector : UserControl
{
    public SchemeInspector() => InitializeComponent();

    private void OnPaletteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not LineColor c) return;
        var ic = b.FindAncestorOfType<ItemsControl>();
        if (ic?.Tag is SceneryLine line) line.Color = c.Hex;
        if (b.FindAncestorOfType<FlyoutPresenter>()?.Parent is Popup p) p.Close();
    }
}
