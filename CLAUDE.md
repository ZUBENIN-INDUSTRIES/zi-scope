<!-- autopilot:start -->
# Lumenera INFINITY 4 Viewer

WPF-программа (.NET 8, x64) для микроскопной камеры Lumenera INFINITY 4: живое превью, коррекция цвета, масштаб и измерения в микронах, снимки и тайм-лапс. Для лаборанта за микроскопом, без интернета; без камеры работает демо-режим.

## Команды

| Команда | Что делает |
|---------|------------|
| `dotnet build LumeneraViewer.sln -c Debug` | Собрать приложение и тесты (в sln только x64) |
| `dotnet run --project src/LumeneraViewer` | Запустить; без драйвера/камеры — кнопка «Демо» |
| `dotnet test LumeneraViewer.sln` | Все тесты (xUnit, сейчас 117 зелёных) |
| `dotnet test LumeneraViewer.sln --filter "FullyQualifiedName~ScaleBarMathTests"` | Один класс тестов |
| `powershell -ExecutionPolicy Bypass -File .\publish.ps1` | Папка для переноса `dist\LumeneraViewer\` (single-file exe + `Drivers\` + `README.txt`); `-Output D:\path` — другая папка |

Вывод Debug: `src/LumeneraViewer/bin/x64/Debug/net8.0-windows/`.

## Структура

```
src/LumeneraViewer/
  Camera/       ICamera, CameraFrame (BGR24), LuCamCamera (P/Invoke lucamapi.dll), SimulatedCamera, CameraFactory
  Drivers/      DriverService (поиск и запуск комплектного установщика), DriverStartupFlow (диалоги при старте)
  Imaging/      ImageAdjustments + ImageProcessor (коррекция пикселей), FrameAnalysis (гистограмма, резкость, яркость)
  Overlays/     OverlayState + OverlayRenderer (один код рисования для превью и файла), ScaleBarMath, Calibration, Measurement
  Export/       FrameExporter (5 форматов, JSON-метаданные, атомарная запись), StaWorker (фоновый STA-поток)
  Settings/     AppSettings (вся модель настроек), SettingsStore (JSON, отложенное сохранение)
  ViewModels/   MainViewModel (хост и конвейер кадров), *PanelViewModel (карточки), FrameBufferPool
  Views/        UserControl карточек, GalleryStrip, PreviewOverlay, AdvancedSettingsView
  UI/           Dialogs.cs, Converters.cs, Controls/ (общие контролы), Effects/ (палитра, анимации, Effects.xaml)
tests/LumeneraViewer.Tests/   xUnit по чистой логике и швам
installer/      README-УСТАНОВКА.txt в git; infinity*.exe (драйверы, 39 МБ) — только локально
docs/adr/       решения 0001–0006
publish.ps1     сборка папки для переноса
```

## Ключевые файлы

- `src/LumeneraViewer/ViewModels/MainViewModel.cs` — хост: подключение камеры, поток обработки, события кадров, Notify, Settings, все sub-VM.
- `src/LumeneraViewer/MainWindow.xaml` — разметка: статус-панель, превью (`ZoomPanHost` + `PreviewOverlay`), правая колонка карточек `x:Name="Cards"`, лента.
- `src/LumeneraViewer/Camera/CameraContracts.cs` — `ICamera`, `CameraFrame`, `CameraProperty`, `CameraInfo`.
- `src/LumeneraViewer/Camera/NativeMethods.cs` — сигнатуры `lucamapi.dll`; менять только с оглядкой на `docs/adr/0001-lucamapi-dll-signatures.md`.
- `src/LumeneraViewer/Settings/AppSettings.cs` — полный состав настроек, включая `Extensions`.
- `src/LumeneraViewer/Overlays/OverlayRenderer.cs` — рисование плашки, сетки, перекрестия, измерений.
- `src/LumeneraViewer/Export/FrameExporter.Save.cs` — энкодеры, впечатывание оверлея, запись файлов.
- `src/LumeneraViewer/UI/Dialogs.cs` — единственный способ показать диалог.
- `src/LumeneraViewer/UI/Effects/Effects.xaml`, `Palette.cs` — стили и цвета (в т. ч. `StatusPanel`, шаблон `StatusSegment`).

## Архитектура

- Камера (`LuCamCamera` или `SimulatedCamera`) поднимает `FrameReceived` в своём фоновом потоке; `CameraFactory` выбирает реальную/демо.
- `MainViewModel.OnCameraFrame`: сразу `RawFrameReady`, затем кладёт кадр в слот «последний» (старые выбрасываются) и будит поток «Обработка кадров».
- Поток обработки: `ImageProcessor.Apply` с `Adjustments` в буфер из `FrameBufferPool` → `LatestProcessedFrame` → `ProcessedFrameReady` → отрисовка в `WriteableBitmap` на `CompositionTarget.Rendering`.
- При `Adjustments.IsIdentity` обработанный кадр — тот же объект, что raw (не из пула). Кадры не мутировать.
- Пауза (`IsFrozen`) замораживает `LatestRawFrame/LatestProcessedFrame`; `RawFrameReady` продолжает идти.
- Карточки: `MainViewModel` в конструкторе создаёт `CameraPanel, ColorPanel, ScalePanel, MeasurePanel, CapturePanel, Gallery` — каждому передаёт `this`; в XAML каждая карточка получает свой sub-VM как DataContext.
- Sub-VM общаются через хост: читают `host.Settings`, пишут `host.Adjustments`, зовут `host.SaveSettingsSoon()`/`host.Notify()`; друг на друга ссылаются через `host.XxxPanel`.
- Оверлей: `ScalePanel.BuildOverlayState()` → неизменяемый `OverlayState` → `OverlayRenderer.Draw`; превью перерисовывается по `ScalePanel.Changed`, масштаб на превью = 1/зум, в файле = `ExportScale(W)` = W/1600.
- Экспорт: `CapturePanel` берёт `LatestProcessedFrame` + `BuildOverlayState()` в UI-потоке → `FrameExporter.SaveAsync` → `StaWorker` пишет файл, `_clean`-копию и `.json`.
- Драйверы: `DriverStartupFlow.EnsureDriversAsync` при старте; установщик ищется в `<папка exe>\Drivers\`.
- Камера → Imaging/Overlays/Export ничего не знают о UI; WPF-типы есть только в Overlays/Export (DrawingContext, энкодеры), ViewModels/Views/UI — только они знают хост.

## Соглашения

- Из обработчиков `FrameReceived`/`RawFrameReady`/`ProcessedFrameReady` в UI — только `Dispatcher.BeginInvoke`, никогда `Invoke`: `Stop()` из UI-потока ждёт поток конвертации и зависнет.
- Кадр из `ProcessedFrameReady` валиден только внутри обработчика; дольше — `using var lease = host.RetainFrame(frame)` (взять внутри обработчика, Dispose когда закончил). `LatestProcessedFrame` можно держать сколько угодно; raw-кадры не пулятся.
- Пиксели всегда BGR24, stride = Width·3.
- Диалоги — только `UI.Dialogs` (`ConfirmAsync`, `AskTextAsync`, `InfoAsync`, `ShowAsync`); нет хоста → вернёт None/false/null, это норма. Хост показывает один диалог за раз.
- Уведомления — `host.Notify(title, text, NotifyKind)`, из любого потока.
- Настройки меняем в `host.Settings`, потом `host.SaveSettingsSoon()` (троттлинг 1 с, любой поток). Кто кэширует настройки в VM — подписывается на `host.SettingsReplaced` и перечитывает.
- Иконки — `SymbolRegular` из WPF-UI 4.3; перед использованием проверь, что имя существует (например, `Camera24`, `Ruler24`, `Scales24` уже используются).
- Все тексты UI и комментарии — на русском. Пространства имён = папки (`LumeneraViewer.ViewModels` и т. д.).
- MVVM на CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`, классы `partial`.
- Слайдеры — `UI.Controls.LabeledSlider` (Label, Value, Minimum, Maximum, DefaultValue — сброс двойным кликом, Step, Format, Suffix).
- Новые NuGet-пакеты не добавлять без согласия пользователя; программа не ходит в сеть.

## Окружение

- Реальная камера требует драйвер Lumenera (`lucamapi.dll` в системе); проверка — `CameraFactory.IsDriverAvailable()`.
- Комплектный установщик: положить `installer\infinity_analyze_and_capture_for_windows_v_*.exe`; csproj копирует его в `Drivers\` вывода, `publish.ps1` без него падает. В git не попадает (`.gitignore`).
- Настройки: `%AppData%\LumeneraViewer\settings.json`; битый файл переименовывается в `.bak` и берутся умолчания.
- Снимки по умолчанию: `<Изображения>\LumeneraCaptures`.
- Камеры на машине разработки нет: всё проверяется в демо (`SimulatedCamera`, есть имитация фокуса) и тестами.

## Тесты

- Лежат в `tests/LumeneraViewer.Tests/`, один файл — один класс; `InternalsVisibleTo` открывает internal-швы.
- Покрыто: `ImageProcessor`, `FrameAnalysis`, `AutoExposure`, `ScaleBarMath`, `Calibration`, `OverlayState`, `FrameExporter` (имена и запись во временную папку), `SettingsStore` (временный путь, восстановление), `DriverService` (через internal-конструктор с подменой установщика), `SimulatedCamera`, `FrameBufferPool` и троттлинг сохранения (`ReliabilityTests`), размер структуры LuCam (`LuCamInteropTests`).
- Не покрыто: UI, `MainViewModel` целиком, работа с живой камерой.

## Подводные камни

- `InvariantGlobalization` в csproj роняет WPF-UI при старте («Cannot find non-neutral culture 'en-us'») — не возвращать.
- Несуществующий `SymbolRegular` (было `WandSparkle24`) роняет XAML при запуске, а сборка проходит.
- `LuCamCamera.Stop()` ждёт поток конвертации без таймаута — отсюда правило про `BeginInvoke`.
- Запущенное приложение или параллельная сборка/тесты блокируют файлы в `bin/`/`obj/` — закрой программу и собирай последовательно.
- P/Invoke к `lucamapi.dll` не проверен на живой камере — см. `docs/adr/0001-lucamapi-dll-signatures.md`; при первой проверке на железе смотреть сюда.
- Порядок создания sub-VM в конструкторе `MainViewModel` важен: `MeasurePanel` подписывается на `host.ScalePanel` и создаётся после него.
- `OverlayState.IsEmpty` решает, впечатывать ли оверлей: новый рисуемый элемент без правки `IsEmpty` в файл не попадёт.
- Закрытие окна: `CapturePanel` откладывает его до дописывания снимков (до 10 с), `Shutdown` закрывает камеру в фоне (до 3 с).

## Как добавить свою функцию

**Новая карточка в правой панели**
1. `src/LumeneraViewer/ViewModels/MyPanelViewModel.cs`: `public partial class MyPanelViewModel : ObservableObject` с конструктором `(MainViewModel host)`, хранить `Host`.
2. В `MainViewModel.cs` добавить свойство `public MyPanelViewModel MyPanel { get; }` и `MyPanel = new MyPanelViewModel(this);` рядом с остальными (после тех панелей, от которых зависит).
3. `src/LumeneraViewer/Views/MyPanel.xaml` + `.xaml.cs` (UserControl, namespace `LumeneraViewer.Views`, конструктор `InitializeComponent()`), образец — `Views/MeasurePanel.xaml`.
4. В `MainWindow.xaml` внутрь `StackPanel x:Name="Cards"`:
   `<fx:SpotlightCard Margin="0,0,0,10"><StackPanel><c:CardHeader Title="Моя функция" Icon="Beaker24"/><v:MyPanel DataContext="{Binding MyPanel}"/></StackPanel></fx:SpotlightCard>` — имя иконки сверь с `SymbolRegular`.

**Доступ к кадрам**
- Каждый кадр с камеры: `host.RawFrameReady += f => ...` (фоновый поток, raw, без коррекции, не пулится).
- Кадр после коррекции: `host.ProcessedFrameReady += f => ...` (фоновый поток); тяжёлую работу — так: `var lease = host.RetainFrame(f); Task.Run(() => { try { Work(f); } finally { lease.Dispose(); } });`.
- Результат в UI — `Application.Current.Dispatcher.BeginInvoke(() => Value = r)`.
- Разовый снимок того, что на экране: `host.LatestProcessedFrame` (можно хранить).
- Готовые анализаторы: `FrameAnalysis.ComputeHistogram/ComputeSharpness/MeanLuma`.
- Управление камерой: `host.Camera?.Set(CameraProperty.Exposure, ms)`; смену камеры ловить через `host.CameraChanged`.

**Свои настройки**
- Не трогая `AppSettings`: ключ в `host.Settings.Extensions` (`Dictionary<string, JsonElement>`).
- Чтение: `host.Settings.Extensions.TryGetValue("myFeature", out var e) ? e.Deserialize<MyOptions>() : new MyOptions()`.
- Запись: `host.Settings.Extensions["myFeature"] = JsonSerializer.SerializeToElement(opts); host.SaveSettingsSoon();`.
- Подписаться на `host.SettingsReplaced` и перечитать (после «Сбросить настройки» словарь пустой).

**Свой оверлей (на превью и в файле)**
1. Добавить `init`-свойство в `Overlays/OverlayState.cs`, заполнить его в `FromSettings` (или через `with` в `ScalePanelViewModel.BuildOverlayState`), учесть в `IsEmpty`.
2. В `OverlayRenderer.Draw` нарисовать в координатах кадра; толщины и шрифт умножать на `scale`.
3. Для перерисовки превью вызвать `host.ScalePanel.RaiseChanged(save: false)`.
- Если оверлей нужен только на экране — свой `FrameworkElement` в `Grid` рядом с `v:PreviewOverlay` в `MainWindow.xaml` (размер = кадр, координаты = пиксели кадра, зум — `host.PreviewZoom`).

**Свой формат экспорта**
1. Значение в `Export/ImageFormatKind.cs`.
2. Расширение в `FrameExporter.Extension` и `SupportedExtensions` (`Export/FrameExporter.cs`) — иначе лента не покажет файл.
3. Энкодер в `CreateEncoder` (`Export/FrameExporter.Save.cs`); нестандартную запись — там же, в `Save`/`WriteAtomic` (выполняется на `StaWorker`).
4. Пункт в `CapturePanelViewModel.AllFormats` (`FormatOption(Kind, "Название")`).
5. Тест в `tests/LumeneraViewer.Tests/FrameExporterTests.cs`.

## Как здесь работает Autopilot

Сборка ведётся навыком `/autopilot`. Требования, спецификация и таски — в `.autopilot/`.
Прогресс — `.autopilot/dashboard.html`. Правило: требование из `manifest.md`
может снять только пользователь.

Если работа продолжается — скажи «продолжи автопилот»: состояние поднимется
из `.autopilot/state.js`, переспрашивать ничего не нужно.
<!-- autopilot:end -->
