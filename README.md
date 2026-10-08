# ZI-scope — программа для микроскопа с камерой Lumenera INFINITY 4

Нативная программа для Windows (WPF, .NET 8, x64): живое изображение с камеры микроскопа,
экспозиция и цвет, плашка масштаба, измерения и снимки в нужном формате. Спокойный тёмный
интерфейс в духе Figma / Linear / VS Code (WPF-UI, графитовая палитра, синий акцент).

Программа ставится на любой компьютер без поиска драйверов в интернете: установщик драйверов
лежит в комплекте, в папке `Drivers\`, и программа сама предлагает его запустить.
В сеть программа не ходит.

## Возможности

- **Драйверы из комплекта.** Нет `lucamapi.dll` — окно «Драйверы камеры не найдены»:
  «Установить» (тихая установка с правами администратора, при неудаче — обычный мастер),
  «Демо-режим», «Позже». После установки камера подключается без перезапуска.
  Переустановка — меню «⋯» → «Установить/переустановить драйверы».
- **Живое изображение** с зумом и перемещением, пауза, FPS и разрешение в статусе.
- **Камера:** экспозиция и усиление (аппаратно), автоэкспозиция, индикатор фокуса.
- **Цвет:** яркость, контраст, гамма, насыщенность, баланс белого, Ч/Б, негатив, пресеты.
  Одна и та же обработка для экрана и файла.
- **Масштаб:** профили объективов, калибровка по известной длине, плашка масштаба.
- **Измерения:** линейка, сетка, перекрестие.
- **Снимки:** PNG / JPEG / TIFF / BMP / JPEG XR, впечатывание плашки, серия по таймеру,
  лента последних снимков.
- **Демо-режим:** симулятор «микропрепарата» — всё можно попробовать без камеры.
- Настройки сохраняются в `%AppData%\ZI-scope\settings.json` (при первом запуске копируются из прежнего `%AppData%\LumeneraViewer\`).

## Быстрый старт (разработчик)

```powershell
dotnet build LumeneraViewer.sln -c Debug
dotnet test  LumeneraViewer.sln
dotnet run --project src/LumeneraViewer
```

Без камеры и драйверов программа покажет окно установки драйверов — выберите «Демо-режим».

## Готовая папка для переноса: `publish.ps1`

```powershell
.\publish.ps1                                   # → dist\ZI-scope\
.\publish.ps1 -Output D:\usb\ZI-scope           # в другое место
```

Собирает self-contained single-file `ZI-scope.exe` (win-x64, Release) и кладёт рядом:

```
dist\ZI-scope\
  ZI-scope.exe            .NET на целевом компьютере не нужен
  Drivers\infinity_analyze_and_capture_for_windows_v_6_5_6.exe
  README.txt              инструкция для пользователя (из installer\README-УСТАНОВКА.txt)
```

Скрипт остановится с ошибкой, если в `installer\` нет установщика `infinity*.exe`.
Установщик драйверов (≈39 МБ, проприетарный, Teledyne Lumenera) **не хранится в git**
(`.gitignore: installer/*.exe`) — положите его в `installer\` локально. При обычной сборке
он тоже копируется в `bin\...\Drivers\` (если его нет, сборка не падает).

Как программа ставит драйверы: «драйверы есть» = `NativeLibrary.TryLoad("lucamapi.dll")`.
Установщик — первый `*.exe` в `<папка программы>\Drivers\`, запускается с `Verb=runas`
(UAC), сначала тихо: `/S /v"/qn REBOOT=ReallySuppress"`, таймаут 10 минут. Код 0 — успех,
3010 — успех с рекомендацией перезагрузки; после успеха DLL перепроверяется. Код
`src/LumeneraViewer/Drivers/`.

## Окружение разработчика

| Компонент | Нужен | Проверка | Установка |
|---|---|---|---|
| .NET 8 SDK (8.0.x) | **обязательно** | `dotnet --list-sdks` | `winget install Microsoft.DotNet.SDK.8` |
| Git | рекомендуется | `git --version` | `winget install Git.Git` |
| VS Code | рекомендуется | `code --version` | `winget install Microsoft.VisualStudioCode` |
| C# Dev Kit для VS Code | рекомендуется | `code --list-extensions` (есть `ms-dotnettools.csdevkit`) | `code --install-extension ms-dotnettools.csdevkit` |
| Visual Studio 2022 Community (дизайнер XAML, отладчик) | по желанию | `winget list Microsoft.VisualStudio.2022.Community` | `winget install Microsoft.VisualStudio.2022.Community --override "--add Microsoft.VisualStudio.Workload.ManagedDesktop --passive"` |
| Inno Setup 6 (если захочется установщик вместо папки) | по желанию | `winget list JRSoftware.InnoSetup` | `winget install JRSoftware.InnoSetup` |

Для сборки exe хватает .NET 8 SDK: `.\publish.ps1` делает всё остальное. NuGet-пакеты
(WPF-UI, CommunityToolkit.Mvvm, xUnit) скачиваются автоматически при первой сборке.

## Структура

```
src/LumeneraViewer/
  Camera/      ICamera, LuCamCamera (P/Invoke lucamapi.dll), SimulatedCamera, CameraFactory
  Drivers/     DriverService (поиск и запуск установщика), DriverStartupFlow (диалоги)
  Imaging/     коррекция цвета, гистограмма, резкость
  Overlays/    плашка масштаба, калибровка, измерения
  Export/      запись снимков
  Settings/    AppSettings, SettingsStore
  ViewModels/  MainViewModel и модели карточек
  Views/, UI/  окно, карточки, эффекты
tests/LumeneraViewer.Tests/   xUnit, чистая логика
installer/   установщик драйверов (локально) и README-УСТАНОВКА.txt
publish.ps1  сборка папки для переноса
```

## Сверка P/Invoke с живой камерой

Камеры на машине разработки не было, поэтому слой `Camera/NativeMethods.cs` и
`Camera/LuCamCamera.cs` собран по публичному LuCam SDK. Библиотека — **`lucamapi.dll`**
(так она называется в пакете драйверов INFINITY ANALYZE/CAPTURE 6.5.6), x64. При первом
запуске на микроскопе проверьте:

1. **Камера открывается и идёт поток.** Если падает при открытии или размеры кадра
   странные — сверьте структуру `LUCAM_FRAME_FORMAT` (28 байт: `xOffset, yOffset, width,
   height, pixelFormat` — ULONG; `subSampleX, flagsX, subSampleY, flagsY` — USHORT) с
   `lucam.h` из SDK.
2. **Константы потока:** `LucamStreamVideoControl` — STOP=0, START_STREAMING=1.
3. **Цвета.** Перепутаны красный и синий — меню «⋯» → «Дополнительно» → «Поменять местами
   R и B» (пересборка не нужна). Там же метод дебайеринга.
4. **Свойства:** EXPOSURE=20 (мс), GAIN=40 — проверьте диапазоны в карточке «Камера».
5. **Версия:** серийный номер в статусе берётся из `LucamQueryVersion`.

Если что-то расходится, пришлите фрагмент `lucam.h` (обычно
`C:\Program Files\Lumenera Corporation\LuCam Software\SDK\Inc\lucam.h`) — правка точечная.
