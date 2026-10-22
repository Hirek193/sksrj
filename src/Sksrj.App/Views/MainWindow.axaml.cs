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

using System.ComponentModel;
using Avalonia.Controls;
using Sksrj.App.ViewModels;

namespace Sksrj.App.Views;

public partial class MainWindow : Window
{
    private GridLength _previewWidth = new(620);
    private GridLength _inspectorWidth = new(420);
    private bool _wasScheme;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) { vm.PropertyChanged += OnVmChanged; UpdateRightColumn(vm); }
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainViewModel vm && e.PropertyName is nameof(MainViewModel.PreviewVisible) or nameof(MainViewModel.SelectedPage))
            UpdateRightColumn(vm);
    }

    private void UpdateRightColumn(MainViewModel vm)
    {
        var col = ContentGrid.ColumnDefinitions[3];
        // zapamiętaj szerokość ustawioną przez użytkownika
        if (col.Width.Value > 0) { if (_wasScheme) _inspectorWidth = col.Width; else _previewWidth = col.Width; }
        if (vm.IsSchemePage) col.Width = _inspectorWidth;
        else col.Width = vm.PreviewVisible ? _previewWidth : new GridLength(0);
        Splitter.IsVisible = col.Width.Value > 0;
        _wasScheme = vm.IsSchemePage;
    }
}
