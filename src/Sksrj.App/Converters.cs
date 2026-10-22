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
