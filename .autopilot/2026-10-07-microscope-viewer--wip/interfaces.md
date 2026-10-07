# Интерфейсы и правила проекта

## Правила проекта (для каждого исполнителя)

- Стек: WPF, .NET 8, `net8.0-windows`, x64, C# 12, `Nullable` и `ImplicitUsings` включены. SDK 8.0.425 установлен.
- Проект приложения: `src/LumeneraViewer/LumeneraViewer.csproj`. Тесты: `tests/LumeneraViewer.Tests/` (xUnit, `net8.0-windows`, x64), создаются в таске 01 и добавляются в `LumeneraViewer.sln`.
- Сборка: `dotnet build LumeneraViewer.sln -c Debug`. Тесты: `dotnet test LumeneraViewer.sln`. Запуск: `dotnet run --project src/LumeneraViewer`.
- NuGet разрешён только такой: `WPF-UI` (lepoco), `CommunityToolkit.Mvvm`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`. Если нужна другая зависимость — верни `BLOCKED`, не ставь сам.
- Тексты интерфейса на русском. Комментарии в коде на русском, по делу, без лишнего.
- Нельзя трогать: `installer/*.exe` (проприетарный установщик драйверов) и всю папку `.autopilot/`, включая `interfaces.md` — её дополняет только оркестратор по блоку INTERFACES из твоего отчёта (кроме папки `screens/` для скриншотов).
- Камеры на машине разработки нет. Всё проверяется в демо-режиме (`SimulatedCamera`) и тестами.
- Программа никогда не ходит в сеть.
- Пространства имён совпадают с папками: `LumeneraViewer.Camera`, `.Drivers`, `.Imaging`, `.Overlays`, `.Export`, `.Settings`, `.ViewModels`, `.Views`, `.UI.Effects`, `.UI.Controls`.

## Границы, решённые в спецификации

| Модуль | Владеет | Выставляет | Прячет |
|---|---|---|---|
| `Camera` | подключение и поток кадров | `interface ICamera : IDisposable { CameraInfo? Info {get;} bool IsStreaming {get;} event Action<CameraFrame> FrameReceived; event Action<string> Error; void Open(); void Start(); void Stop(); void Close(); IReadOnlyList<CameraProperty> SupportedProperties {get;} PropertyRange? GetRange(CameraProperty p); float? Get(CameraProperty p); bool Set(CameraProperty p, float value); }` · `enum CameraProperty { Exposure, Gain }` (Exposure — мс) · `record PropertyRange(float Min, float Max, float Default)` · `record CameraInfo(string Model, string Serial, int Width, int Height, bool IsSimulated)` · `sealed class CameraFrame { required byte[] Bgr24; required int Width; required int Height; long Sequence; DateTime Timestamp }` (пиксели всегда **BGR24**, stride = Width·3) · `static class CameraFactory { static bool IsDriverAvailable(); static int CountRealCameras(); static ICamera CreateReal(CameraSettings s); static ICamera CreateSimulated(); }` · `sealed class SimulatedCamera : ICamera { float Focus {get;set;} /*0..1, 0.5 — резко*/ }` | P/Invoke `lucamapi.dll`, структуры LuCam, дебайеринг, потоки. `FrameReceived` вызывается в фоновом потоке |
| `Drivers` | наличие драйверов и их установку | `sealed class DriverService(string? driversFolder = null /* по умолчанию <AppContext.BaseDirectory>\Drivers */) { string DriversFolder {get;} bool IsInstalled(); string? FindBundledInstaller(); Task<DriverInstallResult> InstallAsync(bool silent, CancellationToken ct = default); }` · `enum DriverInstallResult { Success, SuccessRebootRequired, Cancelled, InstallerNotFound, Failed, TimedOut }` · `static class DriverStartupFlow { static Task<bool> EnsureDriversAsync(MainViewModel host); static Task ReinstallAsync(MainViewModel host); }` (true — драйверы есть, можно подключать реальную камеру) | runas, коды возврата, аргументы InstallShield |
| `Imaging` | обработку пикселей и анализ | `record ImageAdjustments(double Brightness=0, double Contrast=0, double Gamma=1, double Saturation=100, double Temperature=0, double Tint=0, bool Grayscale=false, bool Negative=false) { static ImageAdjustments Default; bool IsIdentity; }` · `static class ImageProcessor { static void Apply(ReadOnlySpan<byte> srcBgr, Span<byte> dstBgr, int w, int h, ImageAdjustments a); static (double r,double g,double b) ComputeGrayWorldGains(ReadOnlySpan<byte> bgr,int w,int h,double roiFraction=0.5); static (double temperature,double tint) GainsToTemperatureTint((double r,double g,double b) gains); }` · `static class FrameAnalysis { static Histogram ComputeHistogram(ReadOnlySpan<byte> bgr,int w,int h,int step=4); static double ComputeSharpness(ReadOnlySpan<byte> bgr,int w,int h,double roiFraction=0.5); static double MeanLuma(ReadOnlySpan<byte> bgr,int w,int h,double roiFraction=0.5); }` · `sealed class Histogram { int[] R,G,B,Luma (по 256); double ClippedFraction /* доля пикселей с любым каналом = 255 */ }` | LUT, параллелизм, unsafe |
| `Overlays` | масштаб, калибровку, измерения в координатах кадра (пиксели кадра) | `static class ScaleBarMath { static (double lengthMicrons, double lengthPixels, string label) Choose(double micronsPerPixel, int frameWidth, double targetFraction = 0.2, double? fixedMicrons = null); static string FormatLength(double microns); }` (длина из ряда 1-2-5×10ⁿ мкм; подпись «500 мкм», «1 мм», «2.5 мм» и т. п.) · `static class Calibration { static double MicronsPerPixel(Point a, Point b, double knownMicrons); }` · `record Measurement(Point A, Point B)` + `static class MeasurementText { static string Label(Measurement m, double? umPerPx); }` · `record OverlayState(...)` + `static class OverlayRenderer { static void Draw(DrawingContext dc, OverlayState s, Size frameSize, double scale /* 1 — превью, frameWidth/1600 — впечатывание */); }` — один код рисования для превью и для файла. Точную форму `OverlayState` таск 05 записывает в «Что построено» | геометрию подписей, «круглые» числа |
| `Export` | запись файлов | `enum ImageFormatKind { Png, Jpeg, Tiff, Bmp, JpegXr }` (создаётся в таске 01, файл `Export/ImageFormatKind.cs`) · `record CaptureMetadata(...)` · `sealed class FrameExporter { Task<string> SaveAsync(CameraFrame processed, OverlayState? overlays, CaptureMetadata meta, ExportSettings o, int? seriesIndex = null); static string BuildFileName(string prefix, DateTime t, string? objective, int? seriesIndex, ImageFormatKind f); static string Extension(ImageFormatKind f); }` | энкодеры, атомарную запись (временный файл → rename) |
| `Settings` | постоянные настройки | `sealed class AppSettings` (поля ниже) · `sealed class SettingsStore(string? path = null /* по умолчанию %AppData%\LumeneraViewer\settings.json */) { string Path {get;} AppSettings Load(); void Save(AppSettings s); void ScheduleSave(AppSettings s) /* с задержкой 1 с */; void Flush(); }` | JSON (`System.Text.Json`, enum строками), восстановление: битый файл → переименовать в `.bak`, вернуть настройки по умолчанию |
| `UI` | окно, карточки, эффекты | `MainWindow`, `Views/*Panel.xaml`, `UI/Effects/*`, `ViewModels/*` | остальное |

### `AppSettings` — полный состав (создаётся целиком в таске 01; поздние таски поля не добавляют, а если поле нужно — `BLOCKED` или запись в «Что построено» с добавлением поля в конец класса)

```csharp
public sealed class AppSettings {
    public CameraSettings Camera { get; set; } = new();
    public ImageAdjustments Adjustments { get; set; } = ImageAdjustments.Default;
    public float? ExposureMs { get; set; }            // последнее значение, null — взять из камеры
    public float? Gain { get; set; }
    public List<AdjustmentPreset> Presets { get; set; } = new();
    public List<ObjectiveProfile> Objectives { get; set; } = new() { new("Объектив 1", null) };
    public string CurrentObjective { get; set; } = "Объектив 1";
    public ScaleBarSettings ScaleBar { get; set; } = new();
    public GuideSettings Guides { get; set; } = new();
    public ExportSettings Export { get; set; } = new();
    public TimelapseSettings Timelapse { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    public Dictionary<string, System.Text.Json.JsonElement> Extensions { get; set; } = new(); // для будущих узких функций (R04)
}
public sealed class CameraSettings { public int CameraIndex {get;set;} = 1; public bool SwapRedBlue {get;set;} public int DemosaicMethod {get;set;} = 2; public bool PreferDemo {get;set;} }
public sealed record AdjustmentPreset(string Name, ImageAdjustments Adjustments, float? ExposureMs, float? Gain);
public sealed record ObjectiveProfile(string Name, double? MicronsPerPixel);   // namespace LumeneraViewer.Settings
public enum ScaleBarCorner { BottomRight, BottomLeft, TopRight, TopLeft }
public enum OverlayColor { White, Black, Yellow }
public enum LengthUnit { Micrometers, Millimeters }
public sealed class ScaleBarSettings { public bool Enabled {get;set;} = true; public ScaleBarCorner Corner {get;set;} public OverlayColor Color {get;set;} public bool Background {get;set;} = true; public double FontSize {get;set;} = 16; public double Thickness {get;set;} = 4; public double? FixedMicrons {get;set;} public LengthUnit Unit {get;set;} public double? CustomX {get;set;} public double? CustomY {get;set;} /* позиция после перетаскивания, в долях кадра 0..1; null — угол */ }
public sealed class GuideSettings { public bool Crosshair {get;set;} public bool Grid {get;set;} public double GridStepMicrons {get;set;} = 100; public double GridStepPercent {get;set;} = 10; }
public sealed class ExportSettings { public string Folder {get;set;} = <Изображения>\LumeneraCaptures; public ImageFormatKind Format {get;set;} = Png; public int JpegQuality {get;set;} = 95; public bool TiffLzw {get;set;} public bool BurnOverlays {get;set;} = true; public bool AlsoSaveClean {get;set;} public bool WriteMetadata {get;set;} = true; public string Prefix {get;set;} = "Lumenera"; }
public sealed class TimelapseSettings { public double IntervalSeconds {get;set;} = 10; public int Count {get;set;} = 0; /* 0 — бесконечно */ }
public sealed class WindowSettings { public double Width {get;set;} = 1400; public double Height {get;set;} = 880; public bool Maximized {get;set;} public bool PanelCollapsed {get;set;} }
```

### Хост окна — `MainViewModel` (создаётся в таске 02; это контракт для тасков 03–06)

```csharp
public enum ConnectionState { NoDrivers, NoCamera, Connecting, Streaming, Lost, Demo }
public enum PreviewTool { None, Ruler, Calibrate }
public enum NotifyKind { Info, Success, Warning, Error }

public partial class MainViewModel : ObservableObject {
    AppSettings Settings {get;}               void SaveSettingsSoon();
    ICamera? Camera {get;}                    event Action? CameraChanged;      // в UI-потоке, после открытия/закрытия
    ConnectionState State {get;}              Task ConnectAsync();  void StartDemo();  // переподключение/демо
    int FrameWidth {get;} int FrameHeight {get;}                                   // observable
    ImageAdjustments Adjustments {get;set;}   // читает конвейер обработки; пишет ColorPanel
    bool IsFrozen {get;set;}
    CameraFrame? LatestProcessedFrame {get;}  // то, что на экране, полное разрешение, после коррекции (с учётом паузы)
    CameraFrame? LatestRawFrame {get;}
    event Action<CameraFrame>? RawFrameReady;        // фоновый поток, каждый принятый кадр
    event Action<CameraFrame>? ProcessedFrameReady;  // фоновый поток, после обработки
    PreviewTool ActiveTool {get;set;}         // при != None левый drag превью не панорамирует
    void Notify(string title, string message, NotifyKind kind = NotifyKind.Info);   // Snackbar
    Window Owner {get;}                       // для диалогов

    CameraPanelViewModel CameraPanel {get;}   // таск 04
    ColorPanelViewModel ColorPanel {get;}     // таск 04
    ScalePanelViewModel ScalePanel {get;}     // таск 05
    MeasurePanelViewModel MeasurePanel {get;} // таск 05
    CapturePanelViewModel CapturePanel {get;} // таск 06; CapturePanel.CaptureCommand — кнопка «Снимок» и Пробел
    GalleryViewModel Gallery {get;}           // таск 06
}
```

Слоты: таск 02 создаёт **заглушки** (пустой `UserControl` + ViewModel-класс с конструктором `(MainViewModel host)`) для `CameraPanel`, `ColorPanel`, `ScalePanel`, `MeasurePanel`, `CapturePanel`, `GalleryStrip` (VM `GalleryViewModel`), `PreviewOverlay` (`Views/PreviewOverlay.xaml`, DataContext — `MainViewModel`, лежит поверх изображения внутри того же трансформа зума и имеет размер кадра в пикселях: `e.GetPosition(overlay)` — координаты кадра), `Drivers/DriverStartupFlow.cs` (заглушка: `EnsureDriversAsync` возвращает `CameraFactory.IsDriverAvailable()`). Таски 03–06 заполняют **только свои** заглушки и не трогают `MainViewModel`/`MainWindow`. Если без правки хоста не обойтись — `BLOCKED` с описанием.

**Швы для тестов:** `ImageProcessor`, `FrameAnalysis`, `ScaleBarMath`, `Calibration`, `MeasurementText`, `FrameExporter.BuildFileName` и `SaveAsync` во временную папку, `SettingsStore` с путём во временную папку, `DriverService.FindBundledInstaller` с заданной папкой, `SimulatedCamera` (кадры приходят, размеры верны).

## Что построено

<!-- дописывает оркестратор по блоку INTERFACES из отчёта таска -->

## Из таска 01 — ядро

- Пакеты: WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2; тесты: xunit 2.9.3, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1. В sln только платформа x64 (Debug|x64, Release|x64); тесты в папке решения `tests`.
- Типы контракта камеры — `Camera/CameraContracts.cs`. `LuCamException : Exception { uint Code }`. `LuCamCamera(CameraSettings) : ICamera`; `Info.Model` реальной камеры = "Lumenera INFINITY", `Serial` из `LucamQueryVersion`.
- `LuCamCamera.Set` при ошибке возвращает false (без исключения); `Get`/`GetRange` → null. Исключения бросают только `Open`/`Start`.
- `SimulatedCamera.Set` клампит в диапазон и возвращает true; `Focus` клампится 0..1; `Start()` сам вызывает `Open()`.
- `Histogram.SampleCount`. `FrameAnalysis.MeanLuma` → 0..1. `ComputeSharpness` — абсолютная дисперсия лапласиана (сравнима только между кадрами одного размера).
- `ComputeGrayWorldGains` → `(meanG/meanR, 1, meanG/meanB)`. `ImageAdjustments.IsIdentity` — `[JsonIgnore]`. Saturation 0..200 (100 — нейтраль).
- `SettingsStore` пишет атомарно (.tmp → rename), кириллица без экранирования, null-поля при загрузке заменяются значениями по умолчанию. `InternalsVisibleTo LumeneraViewer.Tests`.
- Тесты: `dotnet test LumeneraViewer.sln`; один класс — `--filter "FullyQualifiedName~ИмяКласса"`.
- (ремонт 01) `SettingsStore : IDisposable`; `event Action<Exception>? SaveFailed` — ошибка отложенного сохранения наружу не бросается; `ScheduleSave` сериализует снимок в момент вызова; `Flush()` при ошибке бросает; `Dispose()` = Flush без исключений + остановка таймера.
- (ремонт 01) `LuCamCamera.Stop()` ждёт поток конвертации **без таймаута**. **Правило для UI: из обработчиков `FrameReceived` / `RawFrameReady` / `ProcessedFrameReady` в UI-поток — только `Dispatcher.BeginInvoke`, никогда `Invoke`**, иначе Stop из UI-потока зависнет.
- (ремонт 01) `ImageAdjustments.Saturation` клампится в 0..200.

## Из таска 02 — оболочка

- `MainViewModel(Window owner, SettingsStore store)` — по контракту, плюс: `double PreviewZoom {get;set;}` (observable; экранных px на пиксель кадра; пишет окно — **так оверлей узнаёт зум**), `string SettingsPath`, `void ResetSettings()`, `void Shutdown()`, `bool IsPanelCollapsed`, `double Fps`, `WriteableBitmap? PreviewBitmap`, команды Retry/Demo/InstallDrivers/ToggleFreeze/TogglePanel/ShowAdvanced.
- `LatestRawFrame` — raw-кадр, из которого получен `LatestProcessedFrame`; в паузе заморожен. При `Adjustments.IsIdentity` обработанный кадр — **тот же объект**, что raw (не мутировать кадры!). `StartDemo()` — fire-and-forget. `Notify` можно звать из любого потока.
- Диалоги: `new ContentDialog(ContentDialogHost.GetForWindow(host.Owner))` (хост уже в MainWindow).
- Карточки панели в MainWindow: `fx:SpotlightCard > StackPanel{ c:CardHeader Title Icon; v:XxxPanel DataContext="{Binding XxxPanel}" }` — слот получает свой sub-VM как DataContext. Лента: `v:GalleryStrip DataContext=Gallery`, карточка высотой 96 px.
- `PreviewOverlay` — в Grid размером FrameWidth×FrameHeight внутри `ZoomPanHost` (тот же трансформ), Background=Transparent, DataContext=MainViewModel. Обработанный (Handled) левый MouseDown отменяет панорамирование. Средняя/правая кнопка панорамируют всегда.
- Эффекты: `fx:ClickSpark.IsEnabled` (attached), `Entrance.Play(items)`, `CountUpText{Value, Format}`; палитра — `UI/Effects/Palette` + `Effects.xaml`; общие контролы — `UI/Controls/` (`ZoomPanHost`, `CardHeader`), конвертеры — `UI/Converters.cs`.
- Из csproj убран `InvariantGlobalization` (ронял WPF-UI: «Cannot find non-neutral culture 'en-us'»).
