# Заметки по коду серий (как есть на bcebcf3)

`src` = src/LumeneraViewer.

## Series
- `SeriesPlan.cs`: `SeriesOrder`, `FrameDecoration{Clean,ScaleBar,ScaleBarAndMeasurements}`, `FrameKind(Number,Name,Decoration)`, `SeriesStepKey(Group,Sample,Magnification)`, `SeriesPlan(Name,Groups,SamplesPerGroup,Magnifications,Kinds,Order,FileNameTemplate="{group}_{sample}_{mag}_{kind}")`.
- `SeriesSession.cs`: `StepStatus{Pending,Done,Skipped}`; `StepRecord(Status, IReadOnlyList<string> Files /*полные пути*/, DateTime? When)`; `SeriesSession(plan, folder)`: Plan, Folder, Current, Reshooting, Records, ExtraSteps, DoneCount, IsComplete, MarkDone(k, files) (стр.81, When=Now), MarkSkipped, Reset, SetCurrent, BeginReshoot, internal Restore(records, extra, current, reshooting) (стр.133). Метаданных кадров нет.
- `SeriesNaming.FileName(plan,key,kind,ext)` — токены, недопустимые символы → `_`, пустое → `_`.
- `SeriesStore.cs`: `FileName="series.zscope-series.json"`, `Format="zi-scope-series/1"`; Json (WriteIndented, UnsafeRelaxedJsonEscaping, JsonStringEnumConverter). `Save` пишет `FileDto(format,Plan,Current,Reshooting,Extra,Steps)` через `Settings.AtomicFile.WriteAllText`; пути относительные (`ToStored`/`FromStored`). `Load(folder)`: нет файла → FileNotFoundException; битый/чужой → InvalidDataException; Done с пропавшим файлом → в lost (исключается и из Extra).
- Вызовы Save: `SeriesPanelViewModel.StartSeries` (~278), `Env.Persist` (~763). Load: `OpenSeries` (~311), `OfferResumeAsync` (~331).

## SeriesRunner.cs
- `ISeriesEnvironment` (стр.10): CurrentMagnification, IsCalibrated, FileExtension, FileExists, DeleteFile, MoveFile, DeleteEmptyFolder, ConfirmOverwriteAsync, Persist (коммент. стр.27 про series.zscope-series.json). Реализация — вложенный `Env` в SeriesPanelViewModel (~717–766).
- `delegate Task<string> SaveFrameKind(FrameKind kind, string fullPath, bool overwrite)` (стр.48).
- `CaptureAsync(save, cleanOnly)` (стр.120): калибровка → `PathsFor` → overwrite=Reshooting → подтверждение → запись всех видов во временные `<папка>\.zs-new\<имя>` (overwrite=false) → сбой: `DeleteWithJson` временных → `Commit(paths,temps)` (стр.252: старые и их .json `JsonOf` в `.zs-new\old`, временные и их .json на место, откат + `SeriesFileBusyException`) → удаление лишних файлов прежней съёмки (`DeleteWithJson`, 174–175) → `Session.MarkDone(key, paths)` (177) → `_env.Persist` (181).
- `Delete(k)` (стр.202): удаляет файлы и `ChangeExtension(f,".json")`, Reset, Persist. Reshoot/Skip/MakeCurrent/Move → AfterMove → Persist.
- Парные .json: `DeleteWithJson` (295), `JsonOf` (245), `Commit` (259–278), `Delete` (207), комментарии 150, 237, 249.

## SeriesPanelViewModel.cs / SeriesPanel.xaml
- `CaptureCoreAsync` (~452): frame, `BuildOverlayState()`, `BuildMetadata(frame)` (~527, `CaptureMetadata`) → `r.CaptureAsync((kind,path,overwrite)=>SaveKindAsync(...))`. `SaveKindAsync` (~500): extra {Series, Group, Sample, Magnification, Kind, KindName, Decoration} → `_exporter.SaveExactAsync(frame, overlay, meta, S.Export ??= new(), path, overwrite, extra)` (~523).
- `StartSeries` (~248): проверка `File.Exists(Path.Combine(folder, SeriesStore.FileName))` (~268) → ConfirmAsync «Начать новую серию в этой папке? …Чтобы продолжить прежнюю, выберите «Открыть серию…».» → `new SeriesSession` → `SeriesStore.Save` → `Activate`.
- `OpenSeries` (~302): `OpenFolderDialog` «Папка серии (с файлом series.zscope-series.json)», начальная — родитель `S.LastSeriesFolder` или `S.Export.Folder`; `Activate(SeriesStore.Load(folder))`; FileNotFound → Notify «В папке нет файла серии…» (~315); прочее → Notify Error.
- `OfferResumeAsync` (~324, из `Owner.ContentRendered`): `S.LastSeriesFolder` + файл (~330) → Load → не IsFinished → ConfirmAsync «Продолжить серию «{Name}» ({Done} / {Steps})?» → Activate.
- `Activate` (~348): Runner, `S.LastSeriesFolder = session.Folder`, SaveSettingsSoon, BuildMap, IsActive. `FinishSeries` (~364) обнуляет LastSeriesFolder.
- Текст `DeleteStep` (~572) упоминает «(и .json)».
- `SeriesPanel.xaml`: стр.132–133 `<ui:Button … Content="Открыть серию…" Icon="{ui:SymbolIcon Folder24}" Command="{Binding OpenSeriesCommand}"/>`.

## Export
- `CaptureMetadata(Time, CameraModel, CameraSerial, IsSimulated, ExposureMs, Gain, Adjustments, Objective, MicronsPerPixel)`.
- `FrameExporter.Save.cs`: `SaveExactAsync` (стр.55) → `SaveExact` (72): IOException если !overwrite и файл есть; `WriteAtomic`; при `o.WriteMetadata` → `WriteMetadata(json, …, extra)`, иначе при overwrite удаляет старый .json. `WriteMetadata` (~167) пишет приватный `MetadataFile` (~177): Time, File, Format, Width, Height, CameraModel, CameraSerial, IsSimulated, ExposureMs, Gain, Adjustments, Objective, MicronsPerPixel, OverlaysBurned, SeriesIndex, Extra (WhenWritingNull). Обычный `Save` (99) / `SaveAsync` (36) — не трогать.

## Упоминания
- `SeriesStore.FileName`/`zscope-series`: SeriesStore.cs 9,14,36,45; SeriesPanelViewModel 268,305,315,330; SeriesRunner 27. CLAUDE.md 70,72; docs/adr/0009.

## Тесты
- `SeriesStoreTests.cs`: временная папка, план «Опыт 8 окт»; RoundTrip, DoneStepWithMissingFiles, BrokenFile, Overwrites, NoFolderOrNoFile, ForeignFormat, MovedFolder — все через `Load(папка)` и `SeriesStore.FileName`.
- `SeriesRunnerTests.cs`: `FakeEnv` (14–55), `Saver` (58–76, пишет и .json — стр.73). Проверки .json: стр.96, 256, 291–300.
- `FrameExporterTests.cs`: SaveExactAsync_* (183–267) — ожидают .json/Extra, OverwriteWithoutMetadata_DeletesStaleJson, StaleJsonLocked_…; `OverlayExportTests.cs:60,62`.
