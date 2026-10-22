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
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Sksrj.App.Views;

public partial class PromptDialog : Window
{
    public PromptDialog() => InitializeComponent();

    public static async Task<string?> ShowAsync(Window owner, string title, string message, string value, string ok)
    {
        var d = new PromptDialog();
        d.TitleText.Text = title; d.MessageText.Text = message; d.Input.Text = value; d.OkButton.Content = ok;
        d.Opened += (_, _) => { d.Input.Focus(); d.Input.SelectAll(); };
        return await d.ShowDialog<string?>(owner);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Close(Input.Text);
        else if (e.Key == Key.Escape) Close(null);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(Input.Text);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
