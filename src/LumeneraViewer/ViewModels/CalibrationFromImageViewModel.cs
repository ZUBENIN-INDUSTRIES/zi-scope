using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumeneraViewer.ImageCalibration;

namespace LumeneraViewer.ViewModels;

/// <summary>
/// Окно «Калибровка по снимку» (истории 30–32): картинка, найденная линия (концы двигаются), число и единицы,
/// выбор увеличения, итог «N px = L мкм → X мкм/px». Сохранение — <see cref="ScalePanelViewModel.SaveCalibration"/>.
/// </summary>
public partial class CalibrationFromImageViewModel : ObservableObject
{
    private const double MinLinePx = 5;
    private static readonly string[] LineColorNames = { "синяя", "жёлтая", "белая", "чёрная", "красная" };

    public MainViewModel Host { get; }
    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);
    public BitmapSource Image { get; }
    public int ImageWidth => Image.PixelWidth;
    public int ImageHeight => Image.PixelHeight;


    /// <summary>Окно надо закрыть (true — сохранено).</summary>
    public event Action<bool>? CloseRequested;

    public CalibrationFromImageViewModel(MainViewModel host, string path)
    {
        Host = host;
        FilePath = path;
        Image = Load(path);
        Magnifications = host.AvailableMagnifications.ToList();
        _selectedMagnification = host.CurrentMagnification;
        if (!Magnifications.Contains(_selectedMagnification)) Magnifications.Insert(0, _selectedMagnification);
    }

    /// <summary>Прочитать картинку целиком в память (файл не держится открытым).</summary>
    public static BitmapSource Load(string path)
    {
        using var fs = File.OpenRead(path);
        var frame = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }

    // ───── Линия ─────

    [ObservableProperty, NotifyPropertyChangedFor(nameof(LengthPx), nameof(NeedsManualLine), nameof(HintText))]
    [NotifyPropertyChangedFor(nameof(ResultText), nameof(WidthWarning), nameof(CanSave), nameof(SaveText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Point? _a;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(LengthPx), nameof(NeedsManualLine), nameof(HintText))]
    [NotifyPropertyChangedFor(nameof(ResultText), nameof(WidthWarning), nameof(CanSave), nameof(SaveText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Point? _b;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HintText))]
    private bool _isBusy;

    /// <summary>Что нашлось: линия и распознанный текст.</summary>
    [ObservableProperty] private string _detectionText = "Ищу линию…";
    [ObservableProperty] private string _ocrText = "";
    /// <summary>Сырой текст OCR — только для подсказки; null — подсказки нет.</summary>
    [ObservableProperty] private string? _ocrRawText;

    /// <summary>Область подписи над линией (для показа), null — нет.</summary>
    [ObservableProperty] private Rect? _textRegion;

    public double LengthPx => A is { } a && B is { } b ? (b - a).Length : 0;
    public bool NeedsManualLine => A is null || B is null;

    public string HintText => IsBusy ? "Ищу линию и подпись…"
        : A is null ? "Проведите линию вручную: кликните в начале масштабной линии"
        : B is null ? "Теперь кликните в конце линии"
        : "Концы линии можно перетащить мышью";

    // Что пользователь уже трогал руками: поздний результат анализа это не перезаписывает.
    private bool _applyingAnalysis;
    private bool _lineEdited;
    private bool _valueEdited;

    partial void OnAChanged(Point? value) { if (!_applyingAnalysis) _lineEdited = true; }
    partial void OnBChanged(Point? value) { if (!_applyingAnalysis) _lineEdited = true; }
    partial void OnValueTextChanged(string value) { if (!_applyingAnalysis) _valueEdited = true; }
    partial void OnUnitIndexChanged(int value) { if (!_applyingAnalysis) _valueEdited = true; }

    /// <summary>Клик по картинке в ручном режиме: первый — начало, второй — конец.</summary>
    public void Click(Point p)
    {
        if (A is null) A = p;
        else if (B is null) B = p;
    }

    [RelayCommand]
    private void Redraw()
    {
        A = null;
        B = null;
        TextRegion = null;
    }

    // ───── Длина ─────

    public string[] UnitNames { get; } = { "мкм", "мм" };

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ResultText), nameof(CanSave), nameof(SaveText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _valueText = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ResultText), nameof(CanSave), nameof(SaveText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private int _unitIndex;

    public double? Microns =>
        double.TryParse(ValueText.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 && !double.IsInfinity(v)
            ? (UnitIndex == 1 ? v * 1000 : v)
            : null;

    // ───── Увеличение и пересчёт ширины ─────

    public List<int> Magnifications { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SaveText))]
    private int _selectedMagnification;

    /// <summary>Ширина кадра камеры; 0 — кадра ещё не было.</summary>
    public int CameraWidth => Host.FrameWidth;
    public bool WidthDiffers => CameraWidth > 0 && CameraWidth != ImageWidth;

    public string? WidthWarning => WidthDiffers
        ? $"Ширина снимка {ImageWidth} px, а кадра камеры {CameraWidth} px. мкм/px пересчитан в пиксели кадра по отношению ширин."
        : CameraWidth == 0 ? "Кадр камеры ещё не получен: считаю, что снимок в полном разрешении камеры." : null;

    public bool HasWidthWarning => WidthWarning is not null;

    public double? ImageMicronsPerPixel => LengthPx >= MinLinePx && Microns is { } m ? m / LengthPx : null;

    /// <summary>Итог в пикселях кадра камеры — то, что сохраняется.</summary>
    public double? CameraMicronsPerPixel => ImageMicronsPerPixel is { } k
        ? (WidthDiffers ? PixelSizeTable.ToCameraPixels(k, ImageWidth, CameraWidth) : k)
        : null;

    public string ResultText
    {
        get
        {
            if (LengthPx < MinLinePx) return "Линия не задана";
            var px = LengthPx.ToString("0.#", CultureInfo.InvariantCulture);
            if (Microns is not { } m) return $"{px} px = ? мкм — введите длину";
            var text = $"{px} px = {m.ToString("0.###", CultureInfo.InvariantCulture)} мкм → {Fmt(ImageMicronsPerPixel!.Value)} мкм/px";
            if (WidthDiffers) text += $"\nв пикселях кадра камеры: {Fmt(CameraMicronsPerPixel!.Value)} мкм/px";
            return text;
        }
    }

    private static string Fmt(double k) => k.ToString(k < 0.01 ? "0.00000" : "0.0000", CultureInfo.InvariantCulture);

    public bool CanSave => CameraMicronsPerPixel is > 0 && !IsBusy;
    public string SaveText => $"Сохранить для ×{SelectedMagnification}";

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (CameraMicronsPerPixel is not { } k) return;
        Host.ScalePanel.SaveCalibration(SelectedMagnification, k);
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    // ───── Распознавание ─────

    /// <summary>Записать результат анализа, не помечая поля как «тронутые пользователем».</summary>
    private void Apply(Action set)
    {
        _applyingAnalysis = true;
        try { set(); }
        finally { _applyingAnalysis = false; }
    }

    /// <summary>Найти линию и прочитать подпись (фон), результат — в свойства (UI-поток).</summary>
    public async Task AnalyzeAsync()
    {
        IsBusy = true;
        try
        {
            var image = Image;   // заморожена — читать можно из фонового потока
            var (px, w, h, line) = await Task.Run(() =>
            {
                var (p, pw, ph) = ScaleLineDetector.ToBgr24(image);
                return (p, pw, ph, ScaleLineDetector.Detect(p, pw, ph));
            });
            if (line is null)
            {
                DetectionText = "Линия не найдена.";
                OcrText = "Проведите линию вручную и введите её длину.";
                return;
            }
            // Пока шёл поиск, пользователь мог уже провести линию сам — его линию не трогаем.
            if (!_lineEdited)
                Apply(() => { A = line.A; B = line.B; TextRegion = line.TextRegionAbove; });
            DetectionText = $"Найдена {LineColorNames[(int)line.Color]} линия: {line.LengthPx:0} px"
                            + (_lineEdited ? " (оставлена ваша линия)" : "");

            OcrText = "Распознаю подпись…";
            OcrRawText = null;
            var ocr = await ScaleTextRecognizer.RecognizeAsync(px, w, h, line);
            if (!ocr.OcrAvailable) OcrText = ScaleTextRecognizer.UnavailableMessage;
            else if (ocr.Reading is { } r)
            {
                // Рамка — там, где число нашлось (над линией или под ней), если линию не правили вручную.
                if (!_lineEdited && ocr.Region is { } where) TextRegion = where;
                // Показываем прочтение («100 µm»), а не сырой OCR («100 pm») — он только в подсказке.
                OcrRawText = $"Текст OCR: «{ocr.RawText.Trim()}»";
                if (_valueEdited)
                    OcrText = $"Распознано: {r.Display} — оставлено введённое вами число.";
                else
                {
                    Apply(() =>
                    {
                        ValueText = r.Value.ToString("0.###", CultureInfo.InvariantCulture);
                        UnitIndex = r.Unit == ScaleUnit.Millimeters ? 1 : 0;
                    });
                    OcrText = $"Распознано: {r.Display}. Проверьте число.";
                }
            }
            else OcrText = ocr.RawText.Length > 0
                ? $"Число не распознано (прочитано «{ocr.RawText.Trim()}») — введите вручную."
                : "Число над линией не распознано — введите вручную.";
        }
        catch (Exception ex)
        {
            DetectionText = "Не удалось разобрать снимок: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanSave));
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
