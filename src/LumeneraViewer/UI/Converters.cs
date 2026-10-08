using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LumeneraViewer.UI.Effects;
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
    public object Convert(object value, Type t, object? p, CultureInfo c) => value switch
    {
        ConnectionState.Streaming or ConnectionState.Demo => Palette.SuccessBrush,
        ConnectionState.Connecting or ConnectionState.Lost => Palette.WarningBrush,
        _ => Palette.ErrorBrush,
    };

    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
