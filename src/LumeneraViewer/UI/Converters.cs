using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LumeneraViewer.ViewModels;

namespace LumeneraViewer.UI;

/// <summary>bool → Visibility; ConverterParameter="Invert" инвертирует.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object? p, CultureInfo c)
    {
        var b = value is true;
        if (p as string == "Invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>value.ToString() == параметр → true (для enum: ActiveTool == None и т. п.).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => string.Equals(value?.ToString(), p?.ToString(), StringComparison.Ordinal);
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>Цвет точки статуса: зелёный — поток идёт, жёлтый — ожидание, красный — нет камеры.</summary>
public sealed class StateToBrushConverter : IValueConverter
{
    private static readonly Brush Green = Freeze(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly Brush Yellow = Freeze(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush Red = Freeze(Color.FromRgb(0xEF, 0x44, 0x44));

    public object Convert(object value, Type t, object? p, CultureInfo c) => value switch
    {
        ConnectionState.Streaming or ConnectionState.Demo => Green,
        ConnectionState.Connecting or ConnectionState.Lost => Yellow,
        _ => Red,
    };

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();

    private static Brush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
