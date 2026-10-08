# Интерфейсы и правила проекта

## Правила проекта (для каждого исполнителя)

- **Сначала прочитай `CLAUDE.md` в корне**: там архитектура, соглашения, подводные камни и «Как добавить свою функцию» для текущего кода. Это обязательный контекст, не изобретай заново то, что там описано (Dialogs, RetainFrame, BeginInvoke, SettingsReplaced, LabeledSlider, проверка SymbolRegular и т. д.).
- Стек: WPF, .NET 8 (`net8.0-windows`, x64), WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, xUnit. Новые NuGet — только через `BLOCKED`.
- Сборка: `dotnet build LumeneraViewer.sln -c Debug`. Тесты: `dotnet test LumeneraViewer.sln` (на старте 117 зелёных). Один класс — `--filter "FullyQualifiedName~Имя"`. Вывод обрезай.
- Пространства имён остаются `LumeneraViewer.*`; новые папки = новые пространства (`LumeneraViewer.Series`).
- Тексты интерфейса и комментарии — по-русски.
- Камеры и драйвера на машине нет: проверка в демо-режиме и тестами. **Установщик драйверов никогда не запускать.**
- Ручной запуск — НЕ из bin: скопировать вывод в `$env:TEMP\zs-<номер таска>` и запускать оттуда; после проверки закрыть процессы и все окна (включая окна ошибок).
- Пользовательские настройки (`%AppData%\LumeneraViewer\settings.json` и, после таска 03, `%AppData%\ZI-scope\…`) — не менять и не удалять; для проверок — резервная копия и восстановление байт в байт.
- Параллельно могут работать другие исполнители в других зонах: при блокировке файлов (MSB3026/MSB3027/CS2012) подожди 20–40 с и повтори; ошибки компиляции в чужих файлах не чини — подожди; держится > 10 мин → CONCERNS.
- Не трогать `installer/*.exe` и `.autopilot/` (кроме `screens/`). `interfaces.md` дополняет только оркестратор по блоку INTERFACES из отчёта. Не делать git commit.

## Границы, решённые в спецификации

| Модуль | Владеет | Выставляет | Прячет |
|---|---|---|---|
| `Imaging` | пиксели | `sealed record DeepColor { double RedGain=1, GreenGain=1, BlueGain=1; ChannelLevels Master, Red, Green, Blue; double ExposureEv=0, Highlights=0, Shadows=0, Whites=0, Blacks=0; double Clarity=0, Vibrance=0, Dehaze=0; IReadOnlyList<HslAdjust> Hsl (ровно 8: Red, Orange, Yellow, Green, Aqua, Blue, Purple, Magenta); double SharpenAmount=0, SharpenRadius=1.0; ToneCurve CurveMaster, CurveRed, CurveGreen, CurveBlue; static DeepColor Neutral; bool IsNeutral; }` · `sealed record ChannelLevels(double Black=0, double White=255, double Gamma=1)` · `sealed record HslAdjust(double Hue=0, double Saturation=0, double Luminance=0)` · `sealed record ToneCurve(IReadOnlyList<CurvePoint> Points) { static ToneCurve Identity; bool IsIdentity; byte[] BuildLut(); }` · `readonly record struct CurvePoint(double X, double Y)` (0..255) · `ImageAdjustments { …существующие поля…; DeepColor Deep = DeepColor.Neutral }`, `IsIdentity` учитывает Deep · `ImageProcessor.Apply(...)` — прежняя сигнатура, учитывает Deep (порядок — спецификация, Решения §4) | LUT, маски, размытие, буферы |
| `Settings` | постоянные настройки | `AppSettings { …существующие…; int CurrentMagnification=100; List<MagnificationProfile> Magnifications (по умолчанию ×100, ×200); List<ColorProfile> ColorProfiles; SeriesDefaults SeriesDefaults; string? LastSeriesFolder; bool LegacyCalibrationDismissed; }` · `sealed class MagnificationProfile { int Magnification; double? MicronsPerPixel; double? ScaleBarMicrons /*null — Авто*/; ImageAdjustments Adjustments; float? ExposureMs; float? Gain; }` · `sealed class ColorProfile { string Name; ImageAdjustments Adjustments; bool IncludesExposure; float? ExposureMs; float? Gain; }` · `sealed class SeriesDefaults { int Groups=16; int SamplesPerGroup=4; List<int> Magnifications=[100,200]; List<FrameKind> Kinds = SeriesPlan.DefaultKinds; SeriesOrder Order=SampleBySample; string FileNameTemplate="{group}_{sample}_{mag}_{kind}"; }` · `SettingsStore` — путь по умолчанию `%AppData%\ZI-scope\settings.json`; если его нет, а есть `%AppData%\LumeneraViewer\settings.json` — копирует (старый не трогает); при загрузке старые `Presets` один раз переносятся в `ColorProfiles` (Presets очищается); `ScaleBar.FixedMicrons` и калибровка текущего объектива **не** переносятся автоматически · `static class MagnificationSettings { MagnificationProfile GetOrCreate(AppSettings s, int mag); void StoreLive(AppSettings s) /* живые Adjustments/ExposureMs/Gain/ScaleBar.FixedMicrons → профиль CurrentMagnification */; void LoadLive(AppSettings s, int mag) /* профиль mag → живые значения, CurrentMagnification = mag */; double? CurrentMicronsPerPixel(AppSettings s); IReadOnlyList<ObjectiveProfile> LegacyCalibrations(AppSettings s) /* старые объективы с мкм/px */; }` · `static class ProfileFiles { ColorProfile ReadColor(string path); void WriteColor(string path, ColorProfile p); AppSettings ReadConfig(string path); void WriteConfig(string path, AppSettings s); string ConfigProfilesFolder /* %AppData%\ZI-scope\profiles */; IReadOnlyList<string> ListConfigProfiles(); }` — `.zcolor` с `"format":"zi-scope-color/1"`, `.zscope` с `"format":"zi-scope-config/1"`; неверный формат → `InvalidDataException("Файл не является профилем ZI-scope")` | JSON, миграции |
| `Series` (новый) | план и состояние серии | `enum SeriesOrder { SampleBySample, ByMagnification }` · `enum FrameDecoration { Clean, ScaleBar, ScaleBarAndMeasurements }` · `sealed record FrameKind(int Number, string Name, FrameDecoration Decoration)` · `sealed record SeriesPlan(string Name, int Groups, int SamplesPerGroup, IReadOnlyList<int> Magnifications, IReadOnlyList<FrameKind> Kinds, SeriesOrder Order, string FileNameTemplate = "{group}_{sample}_{mag}_{kind}") { IReadOnlyList<SeriesStepKey> EnumerateSteps(); int StepCount; int FileCount; static IReadOnlyList<FrameKind> DefaultKinds /* (1,"Без масштаба",Clean), (2,"С масштабом",ScaleBar) */; }` · `readonly record struct SeriesStepKey(int Group, int Sample, int Magnification)` · `enum StepStatus { Pending, Done, Skipped }` · `sealed record StepRecord(StepStatus Status, IReadOnlyList<string> Files, DateTime? When)` · `sealed class SeriesSession { SeriesSession(SeriesPlan plan, string folder); SeriesPlan Plan; string Folder; SeriesStepKey Current; bool Reshooting; IReadOnlyDictionary<SeriesStepKey, StepRecord> Records; IReadOnlyList<SeriesStepKey> ExtraSteps; int DoneCount; bool IsComplete; StepRecord RecordOf(SeriesStepKey k); SeriesStepKey? NextPending(SeriesStepKey? after = null); void MarkDone(SeriesStepKey k, IReadOnlyList<string> files) /* вне плана → в ExtraSteps */; void MarkSkipped(SeriesStepKey k); void Reset(SeriesStepKey k); void SetCurrent(SeriesStepKey k); void BeginReshoot(SeriesStepKey k); }` · `static class SeriesNaming { string FileName(SeriesPlan p, SeriesStepKey k, FrameKind kind, string extension); }` (токены `{group} {sample} {mag} {kind} {series}`, недопустимые символы → `_`) · `static class SeriesStore { const string FileName = "series.zscope-series.json"; void Save(SeriesSession s) /* атомарно */; SeriesSession Load(string folder) /* Done-шаги без файлов → Pending */; }` | порядок обхода, сериализация |
| `Export` | запись | существующий `FrameExporter.SaveAsync` + `Task<string> SaveExactAsync(CameraFrame processed, OverlayState? overlays, CaptureMetadata meta, ExportSettings o, string fullPath, bool overwrite, IReadOnlyDictionary<string,string>? extraMetadata = null)` — ровно этот путь, без `_2`/`_clean`; `overwrite=false` и файл есть → `IOException` | энкодеры |

### Хост `MainViewModel` — новое (добавляет таск 04; таск 01 создаёт заглушки)

```csharp
int CurrentMagnification { get; set; }          // observable; set → MagnificationSettings.StoreLive + LoadLive, применяет Adjustments/экспозицию/плашку, SaveSettingsSoon
event Action<int>? MagnificationChanged;        // UI-поток, после применения
IReadOnlyList<int> AvailableMagnifications { get; } // observable-коллекция профилей
void AddMagnification(int mag); bool RemoveMagnification(int mag);
void ApplyColorProfile(ColorProfile p);         // к текущему увеличению
void LoadConfiguration(AppSettings s);          // заменить все настройки (через SettingsReplaced), без перезапуска
IRelayCommand PrimaryCaptureCommand { get; }    // Пробел и главная кнопка: если SeriesPanel.IsActive → SeriesPanel.CaptureStepCommand, иначе CapturePanel.CaptureCommand
SeriesPanelViewModel SeriesPanel { get; }       // таск 06 (заглушка — таск 01): bool IsActive; IAsyncRelayCommand CaptureStepCommand
```

### Слоты UI (создаёт таск 01)

- Правая панель — вкладки-иконки (как activity bar): «Камера» (CameraPanel), «Цвет» (ColorPanel), «Масштаб» (ScalePanel + MeasurePanel), «Серия» (`Views/SeriesPanel` — заглушка), «Сохранение» (CapturePanel). Одна вкладка видна целиком с прокруткой.
- Шапка: слот переключателя увеличений `Views/MagnificationBar` (заглушка, DataContext=MainViewModel) — наполняет таск 04.
- Низ: лента снимков (`GalleryStrip`) и слот карты серии `Views/SeriesMap` (заглушка, DataContext = SeriesPanel) — переключение по `SeriesPanel.IsActive` (или вручную).
- Пробел и главная кнопка «Снимок» привязаны к `PrimaryCaptureCommand` (заглушка таска 01 просто вызывает `CapturePanel.CaptureCommand`).

**Швы для тестов:** см. спецификацию, «Границы и швы».

## Что построено

<!-- дописывает оркестратор по блоку INTERFACES из отчёта таска -->

## Из таска 02 — движок глубокого цвета

- `sealed record DeepColor` (init-свойства, поля по контракту) + `const int HslCount = 8`, `DeepColor WithHsl(int index, HslAdjust value)`; `Hsl` — `IReadOnlyList<HslAdjust>`, при присваивании дополняется/обрезается до 8; null у уровней/кривых → нейтраль; `IsNeutral` — `[JsonIgnore]`.
- `ChannelLevels` и `HslAdjust` получили `IsNeutral` (у ChannelLevels ещё `static Neutral`). `ToneCurve(IReadOnlyList<CurvePoint> Points)`: `Identity`, `IsIdentity` (все точки на диагонали), `BuildLut()` (Фрич — Карлсон, сортировка, кламп концов); пустой/null → Identity.
- `ImageAdjustments.Deep { get; init; } = DeepColor.Neutral` — **init-свойство**, позиционный конструктор прежний; менять через `with { Deep = ... }`. Null в JSON → нейтраль. Равенство DeepColor/ToneCurve поэлементное (`==` работает после JSON).
- `ImageProcessor` — `static partial` (локальные операции в `ImageProcessor.Local.cs`), сигнатура `Apply` прежняя. Рабочие буферы размытия: 4 массива float по w·h, создаются один раз и переиспользуются.
- Замер скорости: `$env:LV_PERF=1; dotnet test LumeneraViewer.sln -c Release --filter "Category=Perf"` (1920×1440, всё включено: Release ~54 мс, Debug ~76 мс).

## Из таска 03 — модель, серия, профили

- `SettingsStore(string? path = null, string? legacyPath = null)`: при path=null legacyPath = `%AppData%\LumeneraViewer\settings.json`; явный path без legacyPath — без миграции. `string? LegacyPath`, `static string AppDataFolder` (`%AppData%\ZI-scope`).
- Первый переход на новый формат (в JSON нет `Magnifications`): живые Adjustments/ExposureMs/Gain становятся профилем `CurrentMagnification`, `ScaleBar.FixedMicrons` сбрасывается. `Presets` → `ColorProfiles` (IncludesExposure = есть ExposureMs/Gain), Presets очищается — **UI пресетов в ColorPanel теперь пуст: таск 04 переводит его на ColorProfiles**.
- `MagnificationSettings.LoadLive`: если в профиле ExposureMs/Gain null — живые не меняются. `GetOrCreate` держит список отсортированным.
- `ProfileFiles`: константы `ColorFormat`, `ConfigFormat`, `ColorExtension` (`.zcolor`), `ConfigExtension` (`.zscope`), `NotAProfileMessage`; принимается любая версия своего вида; `ReadConfig` проходит те же миграции, что settings.json.
- `SeriesPlan.DefaultTemplate`. `SeriesNaming.FileName`: расширение с точкой или без; пустой шаблон → по умолчанию.
- `SeriesSession`: `bool IsInPlan(k)`; `StepRecord.Pending`; MarkDone/MarkSkipped двигают Current, только если k == Current (к следующему Pending; после пересъёмки/вне плана — к первому Pending; ничего не осталось — Current стоит). `DoneCount` — только шаги плана; `IsComplete` — нет Pending в плане (Skipped считаются пройденными). Reset вне плана удаляет из ExtraSteps.
- `SeriesStore`: `Format = "zi-scope-series/1"`, пути файлов относительные; нет файла → FileNotFoundException, битый → InvalidDataException; Done с пропавшим файлом → Pending (вне плана — удаляется).
- `FrameExporter.SaveExactAsync`: непустой overlays впечатывается всегда (BurnOverlays/AlsoSaveClean игнорируются); extraMetadata → поле `"Extra"` в .json.

## Из таска 01 — тема, переименование, вкладки

- Ресурсы (Brush): `Zs.Window/Panel/Raised/RaisedHover/Input/Border/Text/TextSecondary/TextDisabled/Accent/AccentHover/AccentPressed/AccentSelection/Success/Warning/Error`; `Zs.Font`, `Zs.RadiusControl/RadiusPanel`. Стили: `ZsPanel, ZsGroup, ZsDivider, ZsSectionLabel, MutedText, ZsPrimaryButton, ZsKeyHint, ZsActivityTab, ZsSegment, ZsSegmentBar, StatusPanel/StatusDivider/StatusValue/StatusTag`. `Palette.*` (Color) и `Palette.*Brush` (заморожены); `Theme.Apply(Application)` в `App.OnStartup`. Эффекты Aurora/ShinyText/ClickSpark/GlowButton/SpotlightCard **удалены**; `Entrance`, `CountUpText` остались. `Palette.Cyan` — только для линий измерений.
- Слоты: `v:MagnificationBar x:Name=MagnificationSlot` в шапке (DataContext = MainViewModel); вкладки — RadioButton с Tag `camera|color|scale|series|capture`, страницы `PageCamera…PageCapture` в `Grid Pages`; низ — RadioButton `BottomGallery/BottomSeries`, при `SeriesPanel.IsActive=true` включается `BottomSeries`. Рецепт новой вкладки: страница в `Grid Pages` + RadioButton в TabBar.
- `MainViewModel.SeriesPanel` (`SeriesPanelViewModel`: `[ObservableProperty] bool IsActive`, `IAsyncRelayCommand CaptureStepCommand`, `MainViewModel Host`); `IRelayCommand PrimaryCaptureCommand` (IsActive ? CaptureStep : CapturePanel.Capture, CanExecute пробрасывается) — Пробел и «Снимок» идут через него.
- Выбранная вкладка — `Settings.Extensions["ui.rightTab"]`.
- **AssemblyName = ZI-scope ломает pack-URI `/LumeneraViewer;component/…`**: пиши относительные пути или `/ZI-scope;component/…`.
