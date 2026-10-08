<!-- autopilot:start -->
# ZI-scope (Lumenera INFINITY 4)

WPF-программа (.NET 8, x64; exe — `ZI-scope.exe`, пространства имён `LumeneraViewer.*`) для микроскопной камеры Lumenera INFINITY 4: живое превью, глубокая коррекция цвета, увеличения с профилями, масштаб и измерения в микронах, калибровка по снимку, снимки и серии. Для лаборанта за микроскопом, без интернета; без камеры работает демо-режим.

## Команды

| Команда | Что делает |
|---------|------------|
| `dotnet build LumeneraViewer.sln -c Debug` | Собрать приложение и тесты (в sln только x64) |
| `dotnet run --project src/LumeneraViewer` | Запустить; без драйвера/камеры — кнопка «Демо» |
| `dotnet run --project src/LumeneraViewer -- --settings <путь>\settings.json` | Запуск с отдельным файлом настроек (для проверок; `%AppData%\ZI-scope\settings.json` не трогается) |
| `dotnet test LumeneraViewer.sln` | Все тесты (xUnit: 311 зелёных, 1 пропущен — замер скорости) |
| `dotnet test LumeneraViewer.sln --filter "FullyQualifiedName~ScaleBarMathTests"` | Один класс тестов |
| `$env:LV_PERF=1; dotnet test LumeneraViewer.sln -c Release --filter "Category=Perf"` | Замер скорости `ImageProcessor` с глубоким цветом (PowerShell) |
| `powershell -ExecutionPolicy Bypass -File .\publish.ps1` | Папка для переноса `dist\ZI-scope\` (self-contained single-file `ZI-scope.exe` + `Drivers\` + `README.txt`); `-Output D:\path` — другая папка |

Вывод Debug: `src/LumeneraViewer/bin/x64/Debug/net8.0-windows10.0.19041.0/ZI-scope.exe`.

## Структура

```
src/LumeneraViewer/
  Camera/       ICamera, CameraFrame (BGR24), LuCamCamera (P/Invoke lucamapi.dll), SimulatedCamera, CameraFactory
  Drivers/      DriverService (поиск и запуск комплектного установщика), DriverStartupFlow (диалоги при старте)
  Imaging/      ImageAdjustments + DeepColor (уровни, кривые, HSL, локальные операции), ImageProcessor(.Local), FrameAnalysis
  Calibration/  namespace LumeneraViewer.ImageCalibration: ScaleLineDetector, ScaleTextParser, ScaleTextRecognizer (офлайн-OCR Windows.Media.Ocr), PixelSizeTable
  Overlays/     OverlayState + OverlayRenderer (один код рисования для превью и файла), ScaleBarMath, Calibration (объективы), Measurement
  Series/       SeriesPlan, SeriesSession, SeriesNaming, SeriesStore — модель серии без UI
  Export/       FrameExporter (5 форматов, JSON-метаданные, атомарная запись, SaveExactAsync для серий), StaWorker
  Settings/     AppSettings, SettingsStore (JSON, миграции), MagnificationSettings, Profiles (профили), ProfileFiles (.zcolor/.zscope)
  ViewModels/   MainViewModel (хост и конвейер кадров), *PanelViewModel (вкладки), SeriesRunner, NotificationLog, FrameBufferPool
  Views/        UserControl вкладок, окна (DeepColor, CalibrationFromImage, ConfigProfiles, Notifications), MagnificationBar, SeriesMap, GalleryStrip, PreviewOverlay
  UI/           Dialogs, Converters, ProfileFileFilters, Controls/ (LabeledSlider, ToneCurveEditor, ZoomPanHost…), Effects/ (Palette, Theme, Pill, Effects.xaml)
tests/LumeneraViewer.Tests/   xUnit по чистой логике и швам
installer/      README-УСТАНОВКА.txt в git; infinity*.exe (драйверы, 39 МБ) — только локально
docs/adr/       решения 0001–0006
publish.ps1     сборка папки для переноса
```

## Ключевые файлы

- `src/LumeneraViewer/ViewModels/MainViewModel.cs` — хост: камера, поток обработки, события кадров, `Notify`, `Settings`, увеличения, `PrimaryCaptureCommand`, все sub-VM.
- `src/LumeneraViewer/MainWindow.xaml` (+ `.xaml.cs`) — шапка с `MagnificationSlot`, превью (`ZoomPanHost` + `PreviewOverlay`), низ «Лента / Карта серии», панель вкладок: `TabBar` слева, страницы в `Grid x:Name="Pages"`, главная кнопка «Снимок»; горячие клавиши в `OnPreviewKeyDown`.
- `src/LumeneraViewer/Camera/CameraContracts.cs` — `ICamera`, `CameraFrame`, `CameraProperty`, `CameraInfo`.
- `src/LumeneraViewer/Camera/NativeMethods.cs` — сигнатуры `lucamapi.dll`; менять только с оглядкой на `docs/adr/0001-lucamapi-dll-signatures.md`.
- `src/LumeneraViewer/Settings/AppSettings.cs` — полный состав настроек, включая `Magnifications`, `ColorProfiles`, `SeriesDefaults`, `Extensions`.
- `src/LumeneraViewer/Settings/MagnificationSettings.cs` — живые значения ↔ профиль текущего увеличения (`StoreLive`/`LoadLive`, мкм/px).
- `src/LumeneraViewer/Imaging/DeepColor.cs`, `ImageProcessor.cs`, `ImageProcessor.Local.cs` — модель и применение коррекции.
- `src/LumeneraViewer/Overlays/OverlayState.cs`, `OverlayRenderer.cs` — что и как рисуется поверх кадра.
- `src/LumeneraViewer/Export/FrameExporter.Save.cs` — энкодеры, впечатывание оверлея, запись файлов.
- `src/LumeneraViewer/ViewModels/SeriesPanelViewModel.cs`, `SeriesRunner.cs` — конструктор и прогон серии.
- `src/LumeneraViewer/UI/Dialogs.cs` — единственный способ показать диалог.
- `src/LumeneraViewer/UI/Effects/Palette.cs`, `Theme.cs`, `Effects.xaml` — цвета, перекраска WPF-UI, ресурсы `Zs.*` и стили.

## Архитектура

- Камера (`LuCamCamera` или `SimulatedCamera`) поднимает `FrameReceived` в своём фоновом потоке; `CameraFactory` выбирает реальную/демо.
- `MainViewModel.OnCameraFrame`: сразу `RawFrameReady`, затем кадр в слот «последний» (старые выбрасываются) и будит поток «Обработка кадров».
- Поток обработки: `ImageProcessor.Apply` с `Adjustments` (включая `Adjustments.Deep`) в буфер из `FrameBufferPool` → `LatestProcessedFrame` → `ProcessedFrameReady` → `WriteableBitmap` на `CompositionTarget.Rendering`.
- При `Adjustments.IsIdentity` обработанный кадр — тот же объект, что raw (не из пула). Кадры не мутировать.
- Пауза (`IsFrozen`) замораживает `LatestRawFrame/LatestProcessedFrame`; `RawFrameReady` продолжает идти; смена `Adjustments` на паузе переобрабатывает замороженный кадр.
- Sub-VM создаются в конструкторе `MainViewModel` в порядке `CameraPanel, ColorPanel, ScalePanel, MeasurePanel, CapturePanel, Gallery, SeriesPanel`, каждому передаётся `this`; общаются через хост (`host.Settings`, `host.Adjustments`, `host.XxxPanel`).
- Вкладки: RadioButton со стилем `ZsActivityTab` и `Tag` (`camera|color|scale|series|capture`) в `TabBar`; страница с тем же `Tag` в `Grid Pages`; выбранная — в `Settings.Extensions["ui.rightTab"]`; клик по открытой вкладке сворачивает панель.
- Увеличения: у каждого ×N свой `MagnificationProfile` (цвет, экспозиция, усиление, мкм/px, длина плашки). `host.CurrentMagnification = n` → `StoreLive` → `LoadLive` → применить; `SaveSettingsSoon` сам пишет живые значения в профиль текущего увеличения.
- Профили: `ColorProfile` в `Settings.ColorProfiles` (`host.ApplyColorProfile`), файлы `.zcolor`/`.zscope` через `ProfileFiles`; `host.LoadConfiguration(s)` заменяет все настройки без перезапуска (через `SettingsReplaced`).
- Оверлей: `ScalePanel.BuildOverlayState()` → неизменяемый `OverlayState` → `OverlayRenderer.Draw`; превью перерисовывается по `ScalePanel.Changed`, масштаб на превью = 1/зум, в файле = `OverlayRenderer.ExportScale(W)` = W/1600.
- В файл оверлей идёт только через `OverlayState.ForExport()` (без сетки, перекрестия, недорисованной линии; null — нечего впечатывать).
- Экспорт: `CapturePanel` берёт `LatestProcessedFrame` + `BuildOverlayState()` в UI-потоке → `FrameExporter.SaveAsync` → `StaWorker` пишет файл, `_clean`-копию и `.json`. Серия — `SaveExactAsync` (ровно заданный путь, без `_2`/`_clean`, доп. поля в `"Extra"` .json).
- Главный снимок (Пробел и кнопка) — `host.PrimaryCaptureCommand`: при `SeriesPanel.IsActive` снимает шаг серии, иначе `CapturePanel.CaptureCommand`.
- Серия: `SeriesRunner(SeriesSession, ISeriesEnvironment)` ведёт шаги; состояние — `series.zscope-series.json` в папке серии (`SeriesStore`); при активной серии внизу показывается `SeriesMap`.
- Калибровка по снимку: `ScaleLineDetector` ищет линию (5 цветов) → `ScaleTextRecognizer` читает подпись офлайн-OCR → `ScaleTextParser` → `ScalePanel.SaveCalibration(mag, k)`; таблица мкм/px — `PixelSizeTable`.
- Драйверы: `DriverStartupFlow.EnsureDriversAsync` при старте; установщик ищется в `<папка exe>\Drivers\`.
- Camera/Imaging/Overlays/Export/Series/Settings не знают хост; WPF-типы — в Overlays/Export/Calibration (DrawingContext, энкодеры, BitmapSource); хост знают только ViewModels/Views/UI/Drivers.

## Соглашения

- Из обработчиков `FrameReceived`/`RawFrameReady`/`ProcessedFrameReady` в UI — только `Dispatcher.BeginInvoke`, никогда `Invoke`: `Stop()` из UI-потока ждёт поток конвертации и зависнет.
- Кадр из `ProcessedFrameReady` валиден только внутри обработчика; дольше — `using var lease = host.RetainFrame(frame)` (взять внутри обработчика). `LatestProcessedFrame` можно держать сколько угодно; raw-кадры не пулятся.
- Пиксели всегда BGR24, stride = Width·3.
- Диалоги — только `UI.Dialogs` (`ConfirmAsync`, `AskTextAsync`, `InfoAsync`, `ShowAsync`, первым аргументом `host.Owner`); нет хоста → None/false/null, это норма.
- Уведомления — `host.Notify(title, text, NotifyKind)` из любого потока; не всплывают, а пишутся в `host.Notifications` (`NotificationLog`, до 200) → окно «Уведомления» и колокольчик в шапке со счётчиком непрочитанных.
- Настройки меняем в `host.Settings`, потом `host.SaveSettingsSoon()` (троттлинг 1 с, любой поток); то, что нельзя потерять, — `host.SaveSettingsNow()`. Кто кэширует настройки в VM — подписывается на `host.SettingsReplaced` и перечитывает.
- `ImageAdjustments.Deep` — init-свойство: менять через `host.Adjustments = adj with { Deep = ... }`.
- Цвета — только `Palette` (Color / заморожённые `…Brush`) в коде и ресурсы `Zs.*` в XAML (`{StaticResource Zs.Text}`, `Zs.Accent`, `Zs.Border`…); стили `ZsPanel, ZsGroup, ZsDivider, ZsSectionLabel, MutedText, ZsPrimaryButton, ZsSegment`. Цвета каналов — `Palette.Channel*`/`Palette.HslSectors`; `Palette.Cyan` — только линии измерений.
- Скругления — `Zs.RadiusControl` (8), `Zs.RadiusGroup` (12), `Zs.RadiusPanel` (16); кнопки по умолчанию «таблетки» (`fx:Pill.IsPill`).
- Иконки — `SymbolRegular` из WPF-UI 4.3; имя проверяй, что существует (уже используются `Camera24`, `ColorLine24`, `Ruler24`, `TableSimple24`, `Save24`).
- Все тексты UI и комментарии — на русском. Пространства имён = папки (`LumeneraViewer.ViewModels`), кроме `Calibration/` → `LumeneraViewer.ImageCalibration`.
- MVVM на CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`, классы `partial`.
- Слайдеры — `UI.Controls.LabeledSlider` (Label, Value, Minimum, Maximum, DefaultValue — сброс двойным кликом, Step, Format, Suffix).
- Новые NuGet-пакеты не добавлять без согласия пользователя; программа не ходит в сеть.

## Окружение

- Реальная камера требует драйвер Lumenera (`lucamapi.dll` в системе); проверка — `CameraFactory.IsDriverAvailable()`.
- Комплектный установщик: положить `installer\infinity_analyze_and_capture_for_windows_v_*.exe`; csproj копирует его в `Drivers\` вывода, `publish.ps1` без него падает. В git не попадает.
- Настройки: `%AppData%\ZI-scope\settings.json`; если его нет, а есть `%AppData%\LumeneraViewer\settings.json` — копируется (старый не трогается). Битый файл → `.bak` и умолчания.
- Профили конфигурации: `%AppData%\ZI-scope\profiles` (и при `--settings` тоже).
- Снимки по умолчанию: `<Изображения>\ZI-scope`.
- OCR требует TFM `net8.0-windows10.0.19041.0` (приложение и тесты) и язык Windows с поддержкой OCR; без него калибровка по снимку просит ввести число вручную.
- Камеры на машине разработки нет: всё проверяется в демо (`SimulatedCamera`, есть имитация фокуса) и тестами.

## Тесты

- Лежат в `tests/LumeneraViewer.Tests/`, один файл — один класс; `InternalsVisibleTo` открывает internal-швы.
- Покрыто: `ImageProcessor`, `DeepColor`, `FrameAnalysis`, `AutoExposure`, `ScaleBarMath`, `Calibration`, `OverlayState` и `ForExport`, `FrameExporter`, `SettingsStore` и миграции, `MagnificationSettings`, `ProfileFiles`, серия (`SeriesPlan/Session/Naming/Store/Runner`), калибровка по снимку (`ScaleLineDetector`, `ScaleTextParser`, `PixelSizeTable`), `CurveEditing`, `NotificationLog`, `Palette`, `DriverService`, `SimulatedCamera`, `FrameBufferPool` и троттлинг (`ReliabilityTests`), `LuCamInteropTests`.
- Шов серии для тестов — `ISeriesEnvironment` (увеличение, калибровка, файлы, подтверждение перезаписи).
- Не покрыто: UI, `MainViewModel` целиком, OCR на живых снимках, работа с живой камерой.

## Подводные камни

- `AssemblyName = ZI-scope`: pack-URI `/LumeneraViewer;component/…` не работает — пиши относительные пути или `/ZI-scope;component/…`.
- `InvariantGlobalization` в csproj роняет WPF-UI при старте («Cannot find non-neutral culture 'en-us'») — не возвращать.
- Несуществующий `SymbolRegular` роняет XAML при запуске, а сборка проходит.
- FluentWindow без подложки: у каждого окна фон `Zs.Window` на корневом Grid, иначе системный #202020.
- Цвета трека HSL в `DeepColorWindow.xaml` — переопределённые ключи WPF-UI внутри `LabeledSlider` (`SliderTrackFill…`, `AccentFillColorDefaultBrush`); при смене палитры проверять.
- `LuCamCamera.Stop()` ждёт поток конвертации без таймаута — отсюда правило про `BeginInvoke`.
- Запущенное приложение или параллельная сборка/тесты блокируют файлы в `bin/`/`obj/` — закрой программу и собирай последовательно.
- P/Invoke к `lucamapi.dll` не проверен на живой камере — см. `docs/adr/0001-lucamapi-dll-signatures.md`.
- Порядок создания sub-VM в конструкторе `MainViewModel` важен: `MeasurePanel` подписывается на `host.ScalePanel` и создаётся после него.
- `OverlayState.IsEmpty` решает, впечатывать ли оверлей: новый элемент без правки `IsEmpty` (и `ForExport`, если он только экранный) в файл попадёт неправильно.
- Калибровка и длина плашки старого формата (объективы) при миграции не переносятся в увеличения — только вручную.
- Закрытие окна: `CapturePanel` и серия ждут дописывания снимков (до 10 с), `Shutdown` закрывает камеру в фоне (до 3 с).

## Как добавить свою функцию

**Новая вкладка панели (слева)**
1. `src/LumeneraViewer/ViewModels/MyPanelViewModel.cs`: `public partial class MyPanelViewModel : ObservableObject`, конструктор `(MainViewModel host)`, хранить `Host`.
2. В `MainViewModel.cs`: `public MyPanelViewModel MyPanel { get; }` и `MyPanel = new MyPanelViewModel(this);` после панелей, от которых зависит.
3. `src/LumeneraViewer/Views/MyPanel.xaml` + `.xaml.cs` (UserControl, namespace `LumeneraViewer.Views`), образец — `Views/MeasurePanel.xaml`; цвета — `Zs.*`, группы — `ZsGroup`, подписи — `ZsSectionLabel`.
4. В `MainWindow.xaml` в `Grid x:Name="Pages"`: `<StackPanel x:Name="PageMy" Tag="my" Visibility="Collapsed"><v:MyPanel Margin="0,12,0,0" DataContext="{Binding MyPanel}"/></StackPanel>`.
5. В `StackPanel x:Name="TabBar"`: RadioButton как у соседей (`Style="{StaticResource ZsActivityTab}" GroupName="Tabs" Tag="my" ToolTip="Моя функция"`, `Checked="Tab_Checked" PreviewMouseLeftButtonDown="Tab_PreviewDown"`, внутри `ui:SymbolIcon`); ToolTip становится заголовком страницы.
- Секция внутри существующей вкладки — `ZsDivider` + `c:CardHeader Title="…" Icon="…"` + свой UserControl (как «Измерения» на `PageScale`).

**Доступ к кадрам**
- Каждый кадр: `host.RawFrameReady += f => ...` (фоновый поток, raw, не пулится).
- После коррекции: `host.ProcessedFrameReady += f => ...`; тяжёлое — `var lease = host.RetainFrame(f); Task.Run(() => { try { Work(f); } finally { lease.Dispose(); } });`.
- Результат в UI — `Application.Current.Dispatcher.BeginInvoke(() => Value = r)`.
- Разовый снимок экрана: `host.LatestProcessedFrame` (можно хранить).
- Анализаторы: `FrameAnalysis.ComputeHistogram/ComputeSharpness/MeanLuma`.
- Камера: `host.Camera?.Set(CameraProperty.Exposure, ms)`; смена камеры — `host.CameraChanged`; смена увеличения — `host.MagnificationChanged` (UI-поток).
- Сообщить пользователю — `host.Notify("Заголовок", "текст", NotifyKind.Warning)` (появится в журнале).

**Свои настройки**
- Не трогая `AppSettings`: ключ в `host.Settings.Extensions` (`Dictionary<string, JsonElement>`).
- Чтение: `host.Settings.Extensions.TryGetValue("myFeature", out var e) ? e.Deserialize<MyOptions>() : new MyOptions()`.
- Запись: `host.Settings.Extensions["myFeature"] = JsonSerializer.SerializeToElement(opts); host.SaveSettingsSoon();`.
- Подписаться на `host.SettingsReplaced` и перечитать (сброс настроек и `LoadConfiguration`).
- Значение «своё для каждого увеличения» — поле в `MagnificationProfile` (`Settings/Profiles.cs`) + перенос в `MagnificationSettings.StoreLive/LoadLive`.

**Свой оверлей (на превью и в файле)**
1. `init`-свойство в `Overlays/OverlayState.cs`, заполнить в `FromSettings` (или `with` в `ScalePanelViewModel.BuildOverlayState`), учесть в `IsEmpty`; только экранное — обнулить в `ForExport`.
2. В `OverlayRenderer.Draw` рисовать в координатах кадра; толщины и шрифт умножать на `scale`; цвет — из `Palette`.
3. Перерисовка превью — `host.ScalePanel.RaiseChanged(save: false)`.
- Только на экране — свой `FrameworkElement` в `Grid` рядом с `v:PreviewOverlay` в `MainWindow.xaml` (размер = кадр, координаты = пиксели кадра, зум — `host.PreviewZoom`).

**Свой формат экспорта**
1. Значение в `Export/ImageFormatKind.cs`.
2. Расширение в `FrameExporter.Extension` и `SupportedExtensions` (`Export/FrameExporter.cs`) — иначе лента не покажет файл.
3. Энкодер в `CreateEncoder` (`Export/FrameExporter.Save.cs`), запись выполняется на `StaWorker`.
4. Пункт в `CapturePanelViewModel.AllFormats` (`FormatOption(Kind, "Название")`) — серия берёт тот же список.
5. Тест в `tests/LumeneraViewer.Tests/FrameExporterTests.cs`.

## Как здесь работает Autopilot

Сборка ведётся навыком `/autopilot`. Требования, спецификация и таски — в `.autopilot/`.
Прогресс — `.autopilot/dashboard.html`. Правило: требование из `manifest.md`
может снять только пользователь.

Если работа продолжается — скажи «продолжи автопилот»: состояние поднимется
из `.autopilot/state.js`, переспрашивать ничего не нужно.
<!-- autopilot:end -->
