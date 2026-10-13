# Интерфейсы и правила проекта

## Правила проекта (для каждого исполнителя)

- **Сначала прочитай `CLAUDE.md` в корне** (архитектура, соглашения, подводные камни) и `.autopilot/2026-10-13-series-projects--wip/notes.md` (как устроен код серий сейчас, с номерами строк).
- Стек: WPF .NET 8 x64, TFM `net8.0-windows10.0.19041.0`, WPF-UI 4.3, CommunityToolkit.Mvvm, xUnit. Новые NuGet — только через `BLOCKED`.
- Сборка: `dotnet build LumeneraViewer.sln -c Debug`; тесты: `dotnet test LumeneraViewer.sln` (на старте 370 зелёных, 1 пропущен). Вывод обрезай.
- Тексты UI и комментарии — по-русски; пространства имён = папки.
- **Только папка проекта.** Не трогать «Документы», «Изображения», `%TEMP%`, `%AppData%` (настройки пользователя). Проверочные файлы и копия программы — в `.autopilot/2026-10-13-series-projects--wip/scratch/` (в git не попадает). Ручной запуск — копией из scratch с флагом `--settings <scratch>\settings.json`; после проверки закрыть процессы и окна. Помощник UI Automation: `.autopilot/2026-10-08-zi-scope-iterations/scratch/live/ui.ps1` (можно скопировать).
- Установщик драйверов не запускать; программа в сеть не ходит; камеры нет — демо-режим.
- Не трогать `installer/*.exe`, `.autopilot/` (кроме своей scratch), `CLAUDE.md`, `docs/`. Не делать git commit.

## Договорились с пользователем

- Старые серии (`series.zscope-series.json`, .json у кадров) не поддерживать — «старые серии были только для тестов».
- Метаданные кадров серии — «в серии в .zisc одним списком». Обычные снимки не трогать — «у них уже есть выключатель метаданных».
- Ассоциации в реестре нет — «через открыть проект серии, пока без реестров».
- Имя проекта — по имени серии, без имени `серия.zisc`.

## Границы, решённые в спецификации

| Модуль | Владеет | Выставляет | Прячет |
|---|---|---|---|
| `Export` | запись файлов | `public sealed record FrameMetadata(DateTime Time, string File, ImageFormatKind Format, int Width, int Height, string? CameraModel, string? CameraSerial, bool IsSimulated, float? ExposureMs, float? Gain, ImageAdjustments Adjustments, string? Objective, double? MicronsPerPixel, bool OverlaysBurned, int? SeriesIndex, [property: JsonIgnore(Condition = WhenWritingNull)] IReadOnlyDictionary<string,string>? Extra)` — вместо приватного `MetadataFile`, те же имена и порядок свойств (JSON обычного снимка не меняется) · `Task<FrameMetadata> SaveExactAsync(CameraFrame processed, OverlayState? overlays, CaptureMetadata meta, ExportSettings o, string fullPath, bool overwrite, IReadOnlyDictionary<string,string>? extraMetadata = null)` — пишет только изображение, **никакого .json** (ни писать, ни удалять), `o.WriteMetadata` игнорирует; возвращает метаданные с `File` = имя записанного файла, `SeriesIndex` = null, `Extra` = extraMetadata | энкодеры, `WriteMetadata` |
| `Series` | план, состояние, проект | `StepRecord(StepStatus Status, IReadOnlyList<string> Files, DateTime? When, IReadOnlyList<FrameMetadata>? Frames = null)` (Frames — по одной на файл из Files, тот же порядок, `File` = имя файла) · `SeriesSession(SeriesPlan plan, string folder, string? projectFile = null)`; `string ProjectFile { get; }` — полный путь `.zisc` (по умолчанию `Path.Combine(folder, SeriesStore.ProjectFileName(plan))`) · `void MarkDone(SeriesStepKey k, IReadOnlyList<string> files, IReadOnlyList<FrameMetadata>? frames = null)` · `static class SeriesStore { const string Extension = ".zisc"; const string Format = "zi-scope-series/2"; static string ProjectFileName(SeriesPlan p) /* имя серии, недопустимые символы → "_", пустое → "серия", + ".zisc" */; static void Save(SeriesSession s) /* атомарно в s.ProjectFile; JSON: format, Plan, Current, Reshooting, Extra, Steps (без метаданных), Frames — один список FrameMetadata всех Done-шагов, File относительный */; static SeriesSession Load(string projectFile) /* Folder = папка файла, ProjectFile = этот путь; нет файла → FileNotFoundException; битый/чужой/не /2 → InvalidDataException; Done-шаг с пропавшим файлом → Pending; Frames раскладываются по шагам по имени файла */; static string? FindProject(string folder) /* *.zisc в папке, самый свежий по LastWriteTimeUtc; нет папки/файла → null */ }` — `FileName` (`series.zscope-series.json`) удаляется | DTO |
| `SeriesRunner` | шаги | `delegate Task<FrameMetadata> SaveFrameKind(FrameKind kind, string fullPath, bool overwrite)`; после переноса временных файлов на место — `Session.MarkDone(key, paths, frames с File = Path.GetFileName(итоговый путь))`. Логика парных .json (`JsonOf`, `DeleteWithJson`, удаление .json в `Delete`) убирается — удаляются только изображения | |
| `SeriesPanelViewModel` | UI серии | `OpenSeriesCommand` → `Microsoft.Win32.OpenFileDialog` (фильтр «Проект серии ZI-scope (*.zisc)\|*.zisc», заголовок «Открыть проект серии»), `SeriesStore.Load(path)`; кнопка «Открыть проект серии…». `StartSeries`: проверка `SeriesStore.FindProject(folder)` → вопрос из spec R04.1 → удалить прежний `.zisc` → `Save`. `OfferResumeAsync`: `FindProject(LastSeriesFolder)` → `Load`. `LastSeriesFolder` — как было | |

**Швы для тестов:** `SeriesStore` (временная папка), `SeriesRunner` + `FakeEnv`/`Saver` (делегат теперь возвращает `FrameMetadata`), `FrameExporter.SaveExactAsync` (временная папка). UI — живой проверкой в демо.

## Что построено

<!-- дописывает оркестратор по блоку INTERFACES из отчёта таска -->

## Из таска 01 — проект серии .zisc

- `public sealed record FrameMetadata(...)` в `Export/FrameMetadata.cs` — тот же состав, что у .json обычного снимка; обычный снимок пишет .json из него.
- `Task<FrameMetadata> FrameExporter.SaveExactAsync(...)` — только изображение, .json не пишет и не трогает.
- `StepRecord(..., IReadOnlyList<FrameMetadata>? Frames = null)`, `SeriesSession(plan, folder, projectFile = null)`, `ProjectFile`, `MarkDone(k, files, frames = null)`.
- `SeriesStore`: `Extension=".zisc"`, `Format="zi-scope-series/2"`, `ProjectFileName(p)`, `Save(s)`, `Load(projectFile)`, `FindProject(folder)`; `FileName` удалён.
- `delegate Task<FrameMetadata> SaveFrameKind(...)`; `OpenSeriesCommand` → OpenFileDialog `*.zisc`, кнопка «Открыть проект серии…».
