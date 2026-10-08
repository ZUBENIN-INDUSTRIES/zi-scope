using System.Windows.Media;

namespace LumeneraViewer.UI.Effects;

/// <summary>
/// Палитра ZI-scope: тёмная тема Google Gemini (история 34) — почти чёрный тёплый фон,
/// мягкий голубой акцент, приглушённые пастельные статусы.
/// Единственный источник цветов интерфейса: XAML берёт их через ресурсы <c>Zs.*</c> из Effects.xaml,
/// тема WPF-UI перекрашивается в <see cref="Theme.Apply"/>.
/// </summary>
public static class Palette
{
    // ── Поверхности ──
    /// <summary>Фон окна.</summary>
    public static readonly Color Window = Color.FromRgb(0x13, 0x13, 0x14);
    /// <summary>Панели и карточки.</summary>
    public static readonly Color Panel = Color.FromRgb(0x1E, 0x1F, 0x20);
    /// <summary>Приподнятые элементы: кнопки, плитки, наведение на строки.</summary>
    public static readonly Color Raised = Color.FromRgb(0x28, 0x2A, 0x2C);
    /// <summary>Наведение на приподнятый элемент.</summary>
    public static readonly Color RaisedHover = Color.FromRgb(0x33, 0x35, 0x37);
    /// <summary>Поля ввода и «колодцы» (превью, гистограмма); отличаются от панели границей.</summary>
    public static readonly Color Input = Panel;
    /// <summary>Границы и разделители.</summary>
    public static readonly Color Border = Color.FromRgb(0x44, 0x47, 0x46);

    // ── Текст ──
    public static readonly Color Text = Color.FromRgb(0xE3, 0xE3, 0xE3);
    public static readonly Color TextSecondary = Color.FromRgb(0xC4, 0xC7, 0xC5);
    /// <summary>Приглушённый текст: подписи, отключённое, подсказки.</summary>
    public static readonly Color TextDisabled = Color.FromRgb(0x8E, 0x91, 0x8F);

    // ── Акцент: голубой Gemini ──
    public static readonly Color Accent = Color.FromRgb(0xA8, 0xC7, 0xFA);
    public static readonly Color AccentHover = Color.FromRgb(0xBA, 0xD3, 0xFB);
    public static readonly Color AccentPressed = Color.FromRgb(0x93, 0xB7, 0xF3);
    /// <summary>Текст и иконки на заливке акцентом (основная кнопка, бегунок переключателя).</summary>
    public static readonly Color OnAccent = Color.FromRgb(0x06, 0x2E, 0x6F);
    /// <summary>Фон выделения: акцент с α 0.16.</summary>
    public static readonly Color AccentSelection = WithAlpha(Accent, 0.16);

    // ── Статусы ──
    public static readonly Color Success = Color.FromRgb(0x6D, 0xD5, 0x8C);
    public static readonly Color Warning = Color.FromRgb(0xFD, 0xD6, 0x63);
    public static readonly Color Error = Color.FromRgb(0xF2, 0xB8, 0xB5);

    // ── Полупрозрачные слои ──
    /// <summary>Плашки поверх превью и миниатюр (зум, подписи плиток): фон окна α 0.85.</summary>
    public static readonly Color Scrim = WithAlpha(Window, 0.85);
    /// <summary>Тонкие линии сетки на графиках: основной текст α 0.08.</summary>
    public static readonly Color GridLine = WithAlpha(Text, 0.08);

    /// <summary>
    /// Цвет линий измерений на самом изображении (превью и файл). Это не цвет интерфейса:
    /// яркий голубой нужен, чтобы линия читалась на любом препарате.
    /// </summary>
    public static readonly Color Cyan = Color.FromRgb(0x06, 0xB6, 0xD4);

    // ── Цвета каналов и секторов (гистограммы, кривая, HSL) — мягкие тона Google ──
    /// <summary>Общий канал (яркость) на графиках.</summary>
    public static readonly Color ChannelMaster = Text;
    public static readonly Color ChannelRed = Color.FromRgb(0xF2, 0x8B, 0x82);
    public static readonly Color ChannelGreen = Color.FromRgb(0x81, 0xC9, 0x95);
    public static readonly Color ChannelBlue = Color.FromRgb(0x8A, 0xB4, 0xF8);
    /// <summary>Каналы по индексу: 0 — общий, 1 — R, 2 — G, 3 — B.</summary>
    public static readonly IReadOnlyList<Color> Channels = [ChannelMaster, ChannelRed, ChannelGreen, ChannelBlue];

    // Оттенки HSL, которых нет среди каналов и статусов
    private static readonly Color HueOrange = Color.FromRgb(0xFC, 0xAD, 0x70);
    private static readonly Color HueAqua = Color.FromRgb(0x78, 0xD9, 0xEC);
    private static readonly Color HuePurple = Color.FromRgb(0xC5, 0x8A, 0xF9);
    private static readonly Color HueMagenta = Color.FromRgb(0xFF, 0x8B, 0xCB);

    /// <summary>8 секторов HSL в порядке DeepColor.Hsl: красный, оранжевый, жёлтый, зелёный, голубой, синий, фиолетовый, пурпурный.</summary>
    public static readonly IReadOnlyList<Color> HslSectors =
        [ChannelRed, HueOrange, Warning, ChannelGreen, HueAqua, ChannelBlue, HuePurple, HueMagenta];

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
    public static readonly SolidColorBrush OnAccentBrush = Frozen(OnAccent);
    /// <summary>Подложка подсказки клавиши на акцентной кнопке: цвет текста на акценте α 0.12.</summary>
    public static readonly SolidColorBrush OnAccentSubtleBrush = Frozen(WithAlpha(OnAccent, 0.12));
    public static readonly SolidColorBrush SuccessBrush = Frozen(Success);
    public static readonly SolidColorBrush WarningBrush = Frozen(Warning);
    public static readonly SolidColorBrush ErrorBrush = Frozen(Error);
    public static readonly SolidColorBrush ScrimBrush = Frozen(Scrim);
    public static readonly SolidColorBrush GridLineBrush = Frozen(GridLine);

    public static Color WithAlpha(Color c, double a) => Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0, 1) * 255), c.R, c.G, c.B);

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
