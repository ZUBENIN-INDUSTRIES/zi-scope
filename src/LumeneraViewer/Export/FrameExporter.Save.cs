using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LumeneraViewer.Camera;
using LumeneraViewer.Imaging;
using LumeneraViewer.Overlays;
using LumeneraViewer.Settings;

namespace LumeneraViewer.Export;

public sealed partial class FrameExporter : IDisposable
{
    private readonly StaWorker _worker = new("Запись снимков");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Сколько снимков ещё пишется.</summary>
    public int Pending => _worker.Pending;

    /// <summary>Дождаться окончания записи (при закрытии окна).</summary>
    public bool WaitIdle(TimeSpan timeout) => _worker.WaitIdle(timeout);

    /// <summary>
    /// Сохранить обработанный кадр. Настройки копируются в момент вызова; кадр не изменяется.
    /// Возвращает путь основного файла. Ошибки записи (нет доступа, нет места) — исключением из задачи.
    /// </summary>
    public Task<string> SaveAsync(CameraFrame processed, OverlayState? overlays, CaptureMetadata meta, ExportSettings o, int? seriesIndex = null)
    {
        ArgumentNullException.ThrowIfNull(processed);
        var opt = new ExportSettings
        {
            Folder = o.Folder, Format = o.Format, JpegQuality = Math.Clamp(o.JpegQuality, 1, 100), TiffLzw = o.TiffLzw,
            BurnOverlays = o.BurnOverlays, AlsoSaveClean = o.AlsoSaveClean, WriteMetadata = o.WriteMetadata, Prefix = o.Prefix,
        };
        var burn = opt.BurnOverlays ? overlays?.ForExport() : null;   // сетка и перекрестие — только на экране
        return _worker.Run(() => Save(processed, burn, meta, opt, seriesIndex));
    }

    /// <summary>
    /// Сохранить кадр ровно в <paramref name="fullPath"/> (для серии): без суффиксов «_2» и без копии «_clean».
    /// Непустой <paramref name="overlays"/> впечатывается всегда — оформление решает вызывающий
    /// (BurnOverlays и AlsoSaveClean не учитываются); формат, качество и метаданные — из <paramref name="o"/>.
    /// Файл есть и <paramref name="overwrite"/> = false — <see cref="IOException"/> из задачи, файл не тронут.
    /// <paramref name="extraMetadata"/> попадает в поле «Extra» файла .json.
    /// </summary>
    public Task<string> SaveExactAsync(CameraFrame processed, OverlayState? overlays, CaptureMetadata meta, ExportSettings o,
        string fullPath, bool overwrite, IReadOnlyDictionary<string, string>? extraMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var opt = new ExportSettings
        {
            Folder = Path.GetDirectoryName(Path.GetFullPath(fullPath))!, Format = o.Format,
            JpegQuality = Math.Clamp(o.JpegQuality, 1, 100), TiffLzw = o.TiffLzw,
            BurnOverlays = true, AlsoSaveClean = false, WriteMetadata = o.WriteMetadata, Prefix = o.Prefix,
        };
        var path = Path.GetFullPath(fullPath);
        var extra = extraMetadata is null ? null : new Dictionary<string, string>(extraMetadata);
        var burn = overlays?.ForExport();   // сетка и перекрестие — только на экране
        return _worker.Run(() => SaveExact(processed, burn, meta, opt, path, overwrite, extra));
    }

    private static string SaveExact(CameraFrame f, OverlayState? overlays, CaptureMetadata meta, ExportSettings o,
        string path, bool overwrite, IReadOnlyDictionary<string, string>? extra)
    {
        if (!overwrite && File.Exists(path)) throw new IOException($"Файл уже существует: {path}");
        Directory.CreateDirectory(o.Folder);

        var clean = ToBitmap(f);
        var img = overlays is null ? clean : Burn(clean, overlays, f.Width, f.Height);
        var enc = CreateEncoder(o);
        enc.Frames.Add(BitmapFrame.Create(img));
        WriteAtomic(path, enc.Save, overwrite);
        var json = Path.ChangeExtension(path, ".json");
        if (o.WriteMetadata)
            WriteMetadata(json, Path.GetFileName(path), meta, f, o, overlays is not null, null, extra);
        else if (overwrite && File.Exists(json))
            File.Delete(json);   // метаданные прежнего снимка к новому файлу не относятся
        return path;
    }

    private static string Save(CameraFrame f, OverlayState? overlays, CaptureMetadata meta, ExportSettings o, int? seriesIndex)
    {
        if (string.IsNullOrWhiteSpace(o.Folder)) throw new IOException("Не задана папка для сохранения.");
        Directory.CreateDirectory(o.Folder);

        var ext = Extension(o.Format);
        var stem = Path.GetFileNameWithoutExtension(BuildFileName(o.Prefix, meta.Time, meta.Objective, seriesIndex, o.Format));
        var baseStem = stem;
        for (var n = 2; File.Exists(Path.Combine(o.Folder, stem + ext)) || File.Exists(Path.Combine(o.Folder, stem + ".json")); n++)
            stem = $"{baseStem}_{n}";

        var path = Path.Combine(o.Folder, stem + ext);
        var clean = ToBitmap(f);
        var main = overlays is null ? clean : Burn(clean, overlays, f.Width, f.Height);

        WriteAtomic(path, main, o);
        if (overlays is not null && o.AlsoSaveClean)
            WriteAtomic(Path.Combine(o.Folder, stem + "_clean" + ext), clean, o);
        if (o.WriteMetadata)
            WriteMetadata(Path.Combine(o.Folder, stem + ".json"), Path.GetFileName(path), meta, f, o, overlays is not null, seriesIndex);
        return path;
    }

    private static BitmapSource ToBitmap(CameraFrame f)
    {
        // BitmapSource.Create копирует пиксели — кадр остаётся нетронутым.
        var bmp = BitmapSource.Create(f.Width, f.Height, 96, 96, PixelFormats.Bgr24, null, f.Bgr24, f.Width * 3);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Впечатать оверлеи тем же кодом, что рисует превью, в полном разрешении кадра.</summary>
    private static BitmapSource Burn(BitmapSource src, OverlayState s, int w, int h)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(src, new Rect(0, 0, w, h));
            OverlayRenderer.Draw(dc, s, new Size(w, h), OverlayRenderer.ExportScale(w));
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var bgr = new FormatConvertedBitmap(rtb, PixelFormats.Bgr24, null, 0);
        bgr.Freeze();
        return bgr;
    }

    private static BitmapEncoder CreateEncoder(ExportSettings o) => o.Format switch
    {
        ImageFormatKind.Png => new PngBitmapEncoder(),
        ImageFormatKind.Jpeg => new JpegBitmapEncoder { QualityLevel = o.JpegQuality },
        ImageFormatKind.Tiff => new TiffBitmapEncoder { Compression = o.TiffLzw ? TiffCompressOption.Lzw : TiffCompressOption.None },
        ImageFormatKind.Bmp => new BmpBitmapEncoder(),
        ImageFormatKind.JpegXr => new WmpBitmapEncoder(),
        _ => throw new ArgumentOutOfRangeException(nameof(o)),
    };

    private static void WriteAtomic(string path, BitmapSource img, ExportSettings o)
    {
        var enc = CreateEncoder(o);
        enc.Frames.Add(BitmapFrame.Create(img));
        WriteAtomic(path, enc.Save);
    }

    /// <summary>Запись во временный файл и переименование — частичных файлов не остаётся.</summary>
    private static void WriteAtomic(string path, Action<Stream> write, bool overwrite = true)
    {
        var tmp = path + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                write(fs);
                fs.Flush(true);
            }
            File.Move(tmp, path, overwrite);   // overwrite = false и файл появился — IOException
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* временный файл мог не создаться */ }
            throw;
        }
    }

    private static void WriteMetadata(string path, string imageName, CaptureMetadata m, CameraFrame f, ExportSettings o, bool overlaysBurned, int? seriesIndex,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        var dto = new MetadataFile(
            m.Time, imageName, o.Format, f.Width, f.Height,
            m.CameraModel, m.CameraSerial, m.IsSimulated, m.ExposureMs, m.Gain, m.Adjustments,
            m.Objective, m.MicronsPerPixel, overlaysBurned, seriesIndex, extra);
        WriteAtomic(path, s => JsonSerializer.Serialize(s, dto, Json));
    }

    private sealed record MetadataFile(
        DateTime Time, string File, ImageFormatKind Format, int Width, int Height,
        string? CameraModel, string? CameraSerial, bool IsSimulated, float? ExposureMs, float? Gain,
        ImageAdjustments Adjustments, string? Objective, double? MicronsPerPixel, bool OverlaysBurned, int? SeriesIndex,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Extra = null);

    public void Dispose() => _worker.Dispose();
}
