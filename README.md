# Lumenera INFINITY 4 Viewer

Нативное Windows-приложение (WPF, .NET 8): живой предпросмотр видеопотока с камеры
Lumenera INFINITY 4 и кнопка **Shot**, сохраняющая текущий кадр в PNG / JPEG / TIFF / BMP
с максимальным качеством.

## ВАЖНО: статус P/Invoke-слоя

Код в `Camera/NativeMethods.cs` и `Camera/LuCamCamera.cs` вызывает `lucam.dll` из
официального LuCam SDK. Сигнатуры функций (`LucamCameraOpen`, `LucamGetFormat`,
`LucamStreamVideoControl`, `LucamAddStreamingCallback`, `LucamConvertFrameToRgb24` и т.д.)
восстановлены по публично задокументированному API, который стабилен на протяжении многих
версий SDK. Но у меня нет доступа к реальному заголовку из вашей установки, поэтому перед
первым запуском на камере сверьте следующее с файлом
`lucam.h` (обычно лежит в `C:\Program Files\Lumenera Corporation\LuCam Software\SDK\Inc\lucam.h`):

1. **Порядок и типы полей `LucamFrameFormat`** (`NativeMethods.cs`) — если приложение
   падает при открытии камеры или получает мусорные width/height, скорее всего расходится
   именно эта структура.
2. **Числовые константы** `StreamControlStartStreaming` / `StreamControlStopStreaming`
   (сейчас `1` / `2`) — проверьте значения `enum LUCAM_STREAM_VIDEO_CONTROL` в заголовке.
3. **Калинг-конвенция колбэка** `LucamVideoFrameCallback` — если приложение падает с
   access violation сразу после старта стрима, попробуйте заменить
   `CallingConvention.StdCall` на `CallingConvention.Cdecl`.
4. **Порядок байт цвета** — если превью выглядит с перепутанными красным/синим каналами,
   замените `PixelFormats.Rgb24` на `PixelFormats.Bgr24` в двух местах в
   `MainWindow.xaml.cs` (создание `WriteableBitmap` и `BitmapSource.Create` в `ShotButton_Click`).

Если пришлёте мне реальный `lucam.h` (или хотя бы фрагмент с этими объявлениями) — поправлю
всё точно, без гадания.

## Что установить на компьютер с камерой

1. Драйверы и SDK Lumenera/Teledyne Lumenera для INFINITY 4 (обычно ставится вместе с
   INFINITY Capture / INFINITY Analyze) — после установки в системе должен появиться
   `lucam.dll` (в `System32` или в папке SDK, добавленной в `PATH`).
2. Убедитесь, что `lucam.dll` — 64-битный (проект собирается под x64). Если SDK только
   32-битный, смените `PlatformTarget` в `LumeneraViewer.csproj` на `x86`.
3. [.NET 8 SDK](https://dotnet.microsoft.com/download) — нужен только на машине, где
   собираете приложение, не на машине конечного пользователя (если публикуете
   self-contained exe).

## Сборка и запуск

```powershell
dotnet build
dotnet run --project src/LumeneraViewer
```

## Публикация в один exe-файл

```powershell
dotnet publish src/LumeneraViewer -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Готовый `LumeneraViewer.exe` появится в
`src/LumeneraViewer/bin/Release/net8.0-windows/win-x64/publish/`.
`lucam.dll` при этом всё равно должен быть установлен драйверами Lumenera на целевой
машине — в exe он не встраивается (это отдельный аппаратный драйвер, а не управляемая
библиотека).

## Как это устроено

- `Camera/LuCamCamera.cs` открывает камеру, запрашивает текущий формат кадра
  (`LucamGetFormat` — что вернёт, то и снимается; если камера при включении не в режиме
  полного разрешения без биннинга, здесь стоит добавить явный `LucamSetFormat`) и
  запускает потоковую передачу через колбэк — то есть непрерывный видеопоток, а не
  захват одного кадра по запросу.
- Каждый кадр конвертируется из сырых данных сенсора в RGB24 через
  `LucamConvertFrameToRgb24` (эта функция сама разбирается с Bayer-паттерном и т.п., так
  что нам не нужно знать точные числовые коды pixelFormat).
- `MainWindow.xaml.cs` забирает последний пришедший кадр на каждый тик рендеринга WPF
  (`CompositionTarget.Rendering`) и обновляет `WriteableBitmap` — превью не отстаёт от
  потока и не захлёбывается, даже если камера отдаёт кадры быстрее, чем экран обновляется.
- Кнопка **Shot** берёт этот же последний кадр (то есть ровно то, что видно на экране, в
  полном разрешении потока) и сохраняет его без потерь/с максимальным качеством:
  PNG (без потерь), JPEG (`QualityLevel = 100`), TIFF (без сжатия), BMP (без потерь).
