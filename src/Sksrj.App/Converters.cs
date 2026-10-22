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

using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sksrj.App;

/// <summary>Wiązanie wartości wyliczenia z ComboBox.SelectedIndex.</summary>
public sealed class EnumIndexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum e ? System.Convert.ToInt32(e, CultureInfo.InvariantCulture) : -1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return value is int i && i >= 0 && t.IsEnum ? Enum.ToObject(t, i) : Avalonia.Data.BindingOperations.DoNothing;
    }
}

public sealed class CountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        int i => i > 0,
        ICollection c => c.Count > 0,
        _ => false,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Kolor zapisany jako #RRGGBB → pędzel.</summary>
public sealed class HexBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try { return value is string s && s.Length > 0 ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(s)) : Avalonia.Media.Brushes.Transparent; }
        catch { return Avalonia.Media.Brushes.Transparent; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class Conv
{
    /// <summary>Przyciemnienie posterunków pomijanych w karcie.</summary>
    public static readonly IValueConverter HiddenOpacity = new Avalonia.Data.Converters.FuncValueConverter<bool, double>(b => b ? 0.55 : 1.0);
}
