using System.Windows.Media;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Палитра ZI-scope: графитовая тёмная тема в духе Figma / Linear / VS Code, акцент — синий.
/// Единственный источник цветов интерфейса: XAML берёт их через ресурсы <c>Zs.*</c> из Effects.xaml,
/// тема WPF-UI перекрашивается в <see cref="Theme.Apply"/>.
/// </summary>
public static class Palette
{
    // ── Поверхности ──
    /// <summary>Фон окна.</summary>
    public static readonly Color Window = Color.FromRgb(0x1E, 0x20, 0x24);
    /// <summary>Панели и карточки.</summary>
    public static readonly Color Panel = Color.FromRgb(0x25, 0x28, 0x2D);
    /// <summary>Приподнятые элементы: кнопки, плитки, наведение на строки.</summary>
    public static readonly Color Raised = Color.FromRgb(0x2D, 0x30, 0x36);
    /// <summary>Наведение на приподнятый элемент.</summary>
    public static readonly Color RaisedHover = Color.FromRgb(0x34, 0x38, 0x3F);
    /// <summary>Поля ввода и «колодцы» (превью, гистограмма).</summary>
    public static readonly Color Input = Color.FromRgb(0x1A, 0x1C, 0x20);
    /// <summary>Границы и разделители.</summary>
    public static readonly Color Border = Color.FromRgb(0x36, 0x3A, 0x41);

    // ── Текст ──
    public static readonly Color Text = Color.FromRgb(0xE6, 0xE8, 0xEB);
    public static readonly Color TextSecondary = Color.FromRgb(0x9B, 0xA1, 0xA9);
    public static readonly Color TextDisabled = Color.FromRgb(0x6B, 0x70, 0x79);

    // ── Акцент ──
    public static readonly Color Accent = Color.FromRgb(0x3B, 0x82, 0xF6);
    public static readonly Color AccentHover = Color.FromRgb(0x4C, 0x8D, 0xF7);
    public static readonly Color AccentPressed = Color.FromRgb(0x2F, 0x6F, 0xD6);
    /// <summary>Фон выделения: акцент с α 0.16.</summary>
    public static readonly Color AccentSelection = WithAlpha(Accent, 0.16);

    // ── Статусы ──
    public static readonly Color Success = Color.FromRgb(0x22, 0xC5, 0x5E);
    public static readonly Color Warning = Color.FromRgb(0xF5, 0x9E, 0x0B);
    public static readonly Color Error = Color.FromRgb(0xEF, 0x44, 0x44);

    /// <summary>
    /// Цвет линий измерений на самом изображении (превью и файл). Это не цвет интерфейса:
    /// яркий голубой нужен, чтобы линия читалась на любом препарате.
    /// </summary>
    public static readonly Color Cyan = Color.FromRgb(0x06, 0xB6, 0xD4);

    // ── Цвета каналов и секторов (гистограммы, кривая, HSL) ──
    /// <summary>Общий канал (яркость) на графиках.</summary>
    public static readonly Color ChannelMaster = Color.FromRgb(0xE5, 0xE7, 0xEB);
    public static readonly Color ChannelRed = Color.FromRgb(0xF8, 0x71, 0x71);
    public static readonly Color ChannelGreen = Color.FromRgb(0x4A, 0xDE, 0x80);
    public static readonly Color ChannelBlue = Color.FromRgb(0x60, 0xA5, 0xFA);
    /// <summary>Каналы по индексу: 0 — общий, 1 — R, 2 — G, 3 — B.</summary>
    public static readonly IReadOnlyList<Color> Channels = [ChannelMaster, ChannelRed, ChannelGreen, ChannelBlue];

    /// <summary>8 секторов HSL в порядке DeepColor.Hsl: красный, оранжевый, жёлтый, зелёный, голубой, синий, фиолетовый, пурпурный.</summary>
    public static readonly IReadOnlyList<Color> HslSectors =
    [
        Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF9, 0x73, 0x16), Color.FromRgb(0xEA, 0xB3, 0x08), Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x3B, 0x82, 0xF6), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xD9, 0x46, 0xEF),
    ];

    // ── Готовые кисти (заморожены, можно из любого потока) ──
    public static readonly SolidColorBrush WindowBrush = Frozen(Window);
    public static readonly SolidColorBrush PanelBrush = Frozen(Panel);
    public static readonly SolidColorBrush RaisedBrush = Frozen(Raised);
    public static readonly SolidColorBrush RaisedHoverBrush = Frozen(RaisedHover);
    public static readonly SolidColorBrush InputBrush = Frozen(Input);
    public static readonly SolidColorBrush BorderBrush = Frozen(Border);
    public static readonly SolidColorBrush TextBrush = Frozen(Text);
    public static readonly SolidColorBrush TextSecondaryBrush = Frozen(TextSecondary);
    public static readonly SolidColorBrush TextDisabledBrush = Frozen(TextDisabled);
    public static readonly SolidColorBrush AccentBrush = Frozen(Accent);
    public static readonly SolidColorBrush AccentHoverBrush = Frozen(AccentHover);
    public static readonly SolidColorBrush AccentPressedBrush = Frozen(AccentPressed);
    public static readonly SolidColorBrush AccentSelectionBrush = Frozen(AccentSelection);
    public static readonly SolidColorBrush SuccessBrush = Frozen(Success);
    public static readonly SolidColorBrush WarningBrush = Frozen(Warning);
    public static readonly SolidColorBrush ErrorBrush = Frozen(Error);

    public static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), c.R, c.G, c.B);

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
