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
using Avalonia.VisualTree;
using Sksrj.App.ViewModels;
using Sksrj.Core.Dictionaries;

namespace Sksrj.App.Views;

public partial class PointsView : UserControl
{
    public PointsView() => InitializeComponent();

    private MainViewModel? Vm => DataContext as MainViewModel;

    private static CodeEntry? Take(object? sender)
    {
        if (sender is not ListBox lb || lb.SelectedItem is not CodeEntry e) return null;
        lb.SelectedItem = null;
        // zamknij flyout, w którym znajduje się lista
        if (lb.FindAncestorOfType<FlyoutPresenter>()?.Parent is Popup popup) popup.Close();
        return e;
    }

    private void OnTypePicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.SetPointTypeCommand.Execute(c); }
    private void OnStopKindPicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.InsertStopKindCommand.Execute(c); }
    private void OnAbbreviationPicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.InsertAbbreviationCommand.Execute(c); }
    private void OnBrakePicked(object? sender, SelectionChangedEventArgs e) { if (Take(sender) is { } c) Vm?.SetBrakeTypeCommand.Execute(c); }
}
