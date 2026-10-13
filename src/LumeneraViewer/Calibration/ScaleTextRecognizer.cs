using System.Windows;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Security.Cryptography;

namespace LumeneraViewer.ImageCalibration;

/// <summary>Итог распознавания подписи: значение (или null), сырой текст OCR и было ли распознавание вообще доступно.</summary>
/// <summary>Region — где прочитано число (над линией или под ней); null — не прочитано.</summary>
public sealed record ScaleTextResult(ScaleReading? Reading, string RawText, bool OcrAvailable, Rect? Region = null);

/// <summary>
/// Распознавание подписи над масштабной линией встроенным OCR Windows (<c>Windows.Media.Ocr</c>, офлайн).
/// Сначала — картинка, где цвет линии стал чёрным, остальное белым (подпись обычно того же цвета);
/// не вышло — обычная серая. Разбор текста — <see cref="ScaleTextParser"/>.
/// </summary>
public static class ScaleTextRecognizer
{
    public const string UnavailableMessage =
        "Распознавание текста Windows недоступно в этой системе (нет языка с поддержкой OCR). Введите число вручную.";

    private const int TargetTextHeight = 160;   // OCR лучше читает крупный текст: маленькую область увеличиваем

    /// <summary>Движок OCR: язык профиля пользователя, иначе английский/русский из установленных; null — OCR нет.</summary>
    public static OcrEngine? CreateEngine() => CreateEngines().FirstOrDefault();

    /// <summary>
    /// Движки по порядку попыток: английский (подписи «µm» латиницей — русский движок читает их как «рт»),
    /// затем язык профиля, затем русский. Пусто — OCR в системе нет.
    /// </summary>
    public static IReadOnlyList<OcrEngine> CreateEngines()
    {
        var list = new List<OcrEngine>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(OcrEngine? e)
        {
            if (e is not null && tags.Add(e.RecognizerLanguage.LanguageTag)) list.Add(e);
        }
        try
        {
            var langs = OcrEngine.AvailableRecognizerLanguages;
            var en = langs.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            if (en is not null) Add(OcrEngine.TryCreateFromLanguage(en));
            Add(OcrEngine.TryCreateFromUserProfileLanguages());
            var ru = langs.FirstOrDefault(l => l.LanguageTag.StartsWith("ru", StringComparison.OrdinalIgnoreCase));
            if (ru is not null) Add(OcrEngine.TryCreateFromLanguage(ru));
            if (list.Count == 0 && langs.FirstOrDefault() is { } any) Add(OcrEngine.TryCreateFromLanguage(any));
        }
        catch
        {
            // WinRT недоступен (старая или урезанная система) — это «OCR нет», а не ошибка программы
        }
        return list;
    }

    /// <summary>Распознать число над линией. Пиксели BGR24 картинки целиком; работа идёт в фоновом потоке.</summary>
    /// <summary>Подпись бывает над линией или под ней: сначала читаем сверху, не нашли число — снизу.</summary>
    public static async Task<ScaleTextResult> RecognizeAsync(byte[] bgr, int width, int height, DetectedLine line)
    {
        var above = await RecognizeAsync(bgr, width, height, line.TextRegionAbove, line.Color);
        if (above.Reading is not null || !above.OcrAvailable) return above;
        var below = await RecognizeAsync(bgr, width, height, line.TextRegionBelow, line.Color);
        if (below.Reading is not null) return below;
        var raw = string.Join(" | ", new[] { above.RawText, below.RawText }.Where(t => t.Length > 0));
        return new ScaleTextResult(null, raw, true);
    }

    /// <summary>Распознать число в области region (пиксели картинки); color — цвет подписи для бинаризации.</summary>
    public static Task<ScaleTextResult> RecognizeAsync(byte[] bgr, int width, int height, Rect region, LineColor? color) => Task.Run(async () =>
    {
        var engines = CreateEngines();
        if (engines.Count == 0) return new ScaleTextResult(null, "", false);

        var r = Rect.Intersect(region, new Rect(0, 0, width, height));
        if (r.IsEmpty || r.Width < 4 || r.Height < 4) return new ScaleTextResult(null, "", true);
        int x0 = (int)r.X, y0 = (int)r.Y, w = (int)r.Width, h = (int)r.Height;

        var texts = new List<string>();
        foreach (var engine in engines)
        foreach (var mask in color is null ? new bool[] { false } : new[] { true, false })
        {
            using var bmp = Crop(bgr, width, x0, y0, w, h, mask ? color : null, engine);
            var res = await engine.RecognizeAsync(bmp);
            var text = res.Text ?? "";
            if (text.Length > 0 && !texts.Contains(text)) texts.Add(text);
            if (ScaleTextParser.Parse(text) is { } reading) return new ScaleTextResult(reading, text, true, r);
        }
        return new ScaleTextResult(null, string.Join(" | ", texts), true);
    });

    /// <summary>Вырезать область, увеличить, перевести в серое (или маску цвета) и обрамить белым полем.</summary>
    private static SoftwareBitmap Crop(byte[] bgr, int stride3, int x0, int y0, int w, int h, LineColor? mask, OcrEngine engine)
    {
        var maxDim = (int)OcrEngine.MaxImageDimension;
        const int pad = 16;
        var k = Math.Clamp((int)Math.Ceiling(TargetTextHeight / (double)h), 1, 4);
        while (k > 1 && (w * k + 2 * pad > maxDim || h * k + 2 * pad > maxDim)) k--;
        int ow = Math.Min(w * k + 2 * pad, maxDim), oh = Math.Min(h * k + 2 * pad, maxDim);

        var o = new byte[ow * oh * 4];
        Array.Fill(o, (byte)255);
        var code = mask is { } c ? (byte)(c + 1) : (byte)0;
        for (var oy = pad; oy < oh - pad; oy++)
        {
            var sy = y0 + Math.Min(h - 1, (oy - pad) / k);
            for (var ox = pad; ox < ow - pad; ox++)
            {
                var sx = x0 + Math.Min(w - 1, (ox - pad) / k);
                var i = (sy * stride3 + sx) * 3;
                byte v;
                if (mask is not null)
                    v = ScaleLineDetector.ClassOf(bgr[i + 2], bgr[i + 1], bgr[i]) == code ? (byte)0 : (byte)255;
                else
                    v = (byte)((bgr[i] * 29 + bgr[i + 1] * 150 + bgr[i + 2] * 77) >> 8);
                var j = (oy * ow + ox) * 4;
                o[j] = o[j + 1] = o[j + 2] = v;
            }
        }
        return SoftwareBitmap.CreateCopyFromBuffer(CryptographicBuffer.CreateFromByteArray(o), BitmapPixelFormat.Bgra8, ow, oh, BitmapAlphaMode.Premultiplied);
    }
}
