using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Перекраска тёмной темы WPF-UI в палитру ZI-scope (тёмный Gemini). Вызывается один раз в <c>App.OnStartup</c>,
/// до создания окна. Ресурсы пишутся прямо в <c>Application.Resources</c>, поэтому перекрывают
/// и словарь темы, и то, что записал <see cref="ApplicationAccentColorManager"/>.
/// </summary>
public static class Theme
{
    /// <summary>Шрифт интерфейса: Segoe UI Variable, на старых Windows — обычный Segoe UI (Google Sans офлайн недоступен).</summary>
    public static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    public const double FontSize = 13;

    /// <summary>Скругление полей ввода, списков, всплывающих меню (Gemini: 8).</summary>
    public static readonly CornerRadius RadiusControl = new(8);
    /// <summary>Скругление панелей и диалогов (Gemini: 16).</summary>
    public static readonly CornerRadius RadiusPanel = new(16);

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
        // Инверсный текст в тёмной теме WPF-UI рисует бегунок включённого ToggleSwitch: тёмный на голубом.
        Set("TextFillColorInverse", Palette.OnAccent);

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
        Set("SmokeFillColorDefault", Palette.Scrim);   // затемнение под диалогами

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

        // Акцент: только сплошной голубой Gemini, без «светлых» вариантов WinUI
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
        r["AccentTextFillColorPrimaryBrush"] = Palette.AccentBrush;
        r["AccentTextFillColorSecondaryBrush"] = Palette.AccentBrush;
        r["AccentTextFillColorTertiaryBrush"] = Palette.AccentHoverBrush;
        r["AccentControlElevationBorderBrush"] = Palette.AccentBrush;
        Set("TextOnAccentFillColorPrimary", Palette.OnAccent);
        Set("TextOnAccentFillColorSecondary", Palette.OnAccent);
        Set("TextOnAccentFillColorDisabled", Palette.TextDisabled);
        Set("TextOnAccentFillColorSelectedText", Palette.OnAccent);

        // Включённый переключатель (как Material 3): голубая дорожка, тёмно-синий бегунок
        foreach (var s in new[] { "", "PointerOver", "Pressed" })
        {
            r["ToggleSwitchKnobFillOn" + s] = Palette.OnAccentBrush;
            r["ToggleSwitchStrokeOn" + s] = Palette.AccentBrush;
        }
        r["ToggleSwitchFillOn"] = Palette.AccentBrush;
        r["ToggleSwitchFillOnPointerOver"] = Palette.AccentHoverBrush;
        r["ToggleSwitchFillOnPressed"] = Palette.AccentPressedBrush;

        // Статусы
        Set("SystemFillColorSuccess", Palette.Success);
        Set("SystemFillColorCaution", Palette.Warning);
        Set("SystemFillColorCritical", Palette.Error);

        // Скругления: поля и меню 8, диалоги 16, кнопки — «таблетки»
        r["ControlCornerRadius"] = RadiusControl;
        r["OverlayCornerRadius"] = RadiusPanel;
        r["DialogOverlayCornerRadius"] = RadiusPanel;
        foreach (var type in new[] { typeof(Wpf.Ui.Controls.Button), typeof(Button), typeof(ToggleButton) })
            PillStyle(r, type);
        // Флажок остаётся квадратным (как в Material): радиус 8 на квадрате 20 px превратил бы его в кружок
        var check = new Style(typeof(CheckBox), r[typeof(CheckBox)] as Style);
        check.Resources["ControlCornerRadius"] = new CornerRadius(4);
        r[typeof(CheckBox)] = check;

        // Шрифт
        r["ContentControlThemeFontFamily"] = Font;
        r["ControlContentThemeFontSize"] = FontSize;
    }

    /// <summary>Неявный стиль «как у темы, но таблеткой» для типа кнопки.</summary>
    private static void PillStyle(ResourceDictionary r, Type type)
    {
        var style = new Style(type, r[type] as Style);
        style.Setters.Add(new Setter(Pill.IsPillProperty, true));
        r[type] = style;
    }
}
