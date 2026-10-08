using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Перекраска тёмной темы WPF-UI в палитру ZI-scope. Вызывается один раз в <c>App.OnStartup</c>,
/// до создания окна. Ресурсы пишутся прямо в <c>Application.Resources</c>, поэтому перекрывают
/// и словарь темы, и то, что записал <see cref="ApplicationAccentColorManager"/>.
/// </summary>
public static class Theme
{
    /// <summary>Шрифт интерфейса: Segoe UI Variable, на старых Windows — обычный Segoe UI.</summary>
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    public const double FontSize = 13;

    public static void Apply(Application app)
    {
        ApplicationAccentColorManager.Apply(Palette.Accent, Palette.AccentHover, Palette.Accent, Palette.AccentPressed);
        var r = app.Resources;

        // Цвета темы WPF-UI: ключ цвета и одноимённая кисть «…Brush».
        void Set(string key, Color c)
        {
            r[key] = c;
            r[key + "Brush"] = Palette.Frozen(c);
        }

        // Текст
        Set("TextFillColorPrimary", Palette.Text);
        Set("TextFillColorSecondary", Palette.TextSecondary);
        Set("TextFillColorTertiary", Palette.TextSecondary);
        Set("TextFillColorDisabled", Palette.TextDisabled);
        Set("TextPlaceholderColor", Palette.TextDisabled);
        // Инверсный текст в тёмной теме WPF-UI рисует бегунок включённого ToggleSwitch: белый на синем.
        Set("TextFillColorInverse", Colors.White);

        // Фоны
        Set("ApplicationBackgroundColor", Palette.Window);
        r["ApplicationBackgroundBrush"] = Palette.WindowBrush;
        Set("SolidBackgroundFillColorBase", Palette.Window);
        Set("SolidBackgroundFillColorSecondary", Palette.Panel);
        Set("SolidBackgroundFillColorTertiary", Palette.Raised);
        Set("SolidBackgroundFillColorQuarternary", Palette.Raised);
        Set("LayerFillColorDefault", Palette.Panel);
        Set("LayerOnMicaBaseAltFillColorDefault", Palette.Panel);
        Set("CardBackgroundFillColorDefault", Palette.Panel);
        Set("CardBackgroundFillColorSecondary", Palette.Raised);
        Set("CardStrokeColorDefault", Palette.Border);
        Set("SmokeFillColorDefault", Color.FromArgb(0x99, 0x10, 0x11, 0x14));

        // Контролы: кнопки, поля, списки
        Set("ControlFillColorDefault", Palette.Raised);
        Set("ControlFillColorSecondary", Palette.RaisedHover);
        Set("ControlFillColorTertiary", Palette.Panel);
        Set("ControlFillColorDisabled", Palette.Panel);
        Set("ControlFillColorInputActive", Palette.Input);
        Set("ControlAltFillColorSecondary", Palette.Input);
        Set("ControlStrokeColorDefault", Palette.Border);
        Set("ControlStrokeColorSecondary", Palette.Border);
        Set("ControlStrongStrokeColorDefault", Palette.TextDisabled);
        Set("ControlStrongFillColorDefault", Palette.TextSecondary);
        Set("DividerStrokeColorDefault", Palette.Border);
        Set("SurfaceStrokeColorDefault", Palette.Border);
        Set("SubtleFillColorSecondary", Palette.Raised);
        Set("SubtleFillColorTertiary", Palette.Panel);
        var edge = Palette.BorderBrush;
        r["ControlElevationBorderBrush"] = edge;
        r["CircleElevationBorderBrush"] = edge;
        r["TextControlElevationBorderBrush"] = edge;

        // Акцент: только сплошной синий, без «светлых» вариантов WinUI
        r["SystemAccentColor"] = Palette.Accent;
        r["SystemAccentColorPrimary"] = Palette.AccentHover;
        r["SystemAccentColorSecondary"] = Palette.Accent;
        r["SystemAccentColorTertiary"] = Palette.AccentPressed;
        r["SystemAccentBrush"] = Palette.AccentBrush;
        r["SystemFillColorAttentionBrush"] = Palette.AccentBrush;
        r["AccentFillColorDefaultBrush"] = Palette.AccentBrush;
        r["AccentFillColorSecondaryBrush"] = Palette.AccentHoverBrush;
        r["AccentFillColorTertiaryBrush"] = Palette.AccentPressedBrush;
        r["AccentFillColorDisabledBrush"] = Palette.RaisedBrush;
        r["AccentTextFillColorPrimaryBrush"] = Palette.AccentHoverBrush;
        r["AccentTextFillColorSecondaryBrush"] = Palette.AccentHoverBrush;
        r["AccentTextFillColorTertiaryBrush"] = Palette.AccentBrush;
        r["AccentControlElevationBorderBrush"] = Palette.AccentBrush;
        Set("TextOnAccentFillColorPrimary", Colors.White);
        Set("TextOnAccentFillColorSecondary", Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));
        Set("TextOnAccentFillColorDisabled", Palette.TextDisabled);
        Set("TextOnAccentFillColorSelectedText", Colors.White);

        // Включённый переключатель: синяя дорожка, белый бегунок
        var white = Palette.Frozen(Colors.White);
        foreach (var s in new[] { "", "PointerOver", "Pressed" })
        {
            r["ToggleSwitchKnobFillOn" + s] = white;
            r["ToggleSwitchStrokeOn" + s] = Palette.AccentBrush;
        }
        r["ToggleSwitchFillOn"] = Palette.AccentBrush;
        r["ToggleSwitchFillOnPointerOver"] = Palette.AccentHoverBrush;
        r["ToggleSwitchFillOnPressed"] = Palette.AccentPressedBrush;

        // Статусы
        Set("SystemFillColorSuccess", Palette.Success);
        Set("SystemFillColorCaution", Palette.Warning);
        Set("SystemFillColorCritical", Palette.Error);

        // Шрифт
        r["ContentControlThemeFontFamily"] = Font;
        r["ControlContentThemeFontSize"] = FontSize;
    }
}
