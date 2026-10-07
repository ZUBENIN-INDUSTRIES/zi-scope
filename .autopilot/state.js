window.STATE =
{
  "slug": "microscope-viewer",
  "dir": "2026-10-07-microscope-viewer--wip",
  "title": "Программа для микроскопа Lumenera INFINITY 4",
  "mode": "semi",
  "depth": "normal",
  "polish": null,
  "tier": "T2",
  "briefFile": "2026-10-07-brief.md",
  "memoryFile": "CLAUDE.md",
  "skillDir": "C:/Users/Ярослав/.agents/skills/autopilot",
  "startedAt": "2026-10-07T18:45:30+05:00",
  "updatedAt": "2026-10-07T22:55:53+05:00",
  "finishedAt": null,
  "stages": [
    {
      "id": "preflight",
      "status": "done",
      "startedAt": "2026-10-07T18:45:30+05:00",
      "finishedAt": "2026-10-07T18:53:30+05:00"
    },
    {
      "id": "manifest",
      "status": "done",
      "startedAt": "2026-10-07T18:47:00+05:00",
      "finishedAt": "2026-10-07T18:47:40+05:00"
    },
    {
      "id": "briefing",
      "status": "done",
      "startedAt": "2026-10-07T18:47:40+05:00",
      "finishedAt": "2026-10-07T18:49:20+05:00"
    },
    {
      "id": "spec",
      "status": "done",
      "startedAt": "2026-10-07T18:49:20+05:00",
      "finishedAt": "2026-10-07T18:53:30+05:00"
    },
    {
      "id": "plan",
      "status": "done",
      "startedAt": "2026-10-07T18:53:30+05:00",
      "note": "6 тасков, ярус T2, 4 волны",
      "finishedAt": "2026-10-07T18:58:38+05:00"
    },
    {
      "id": "build",
      "status": "active",
      "startedAt": "2026-10-07T18:58:38+05:00",
      "note": "6 из 7 тасков готовы"
    },
    {
      "id": "review",
      "status": "active",
      "startedAt": "2026-10-07T19:08:00+05:00",
      "note": "проверено 6 из 7"
    },
    {
      "id": "final",
      "status": "pending"
    }
  ],
  "requirements": {
    "total": 19,
    "done": 0,
    "inTicket": 18,
    "inSpec": 1,
    "placeholder": 0,
    "deferred": 0,
    "dropped": 0
  },
  "tickets": [
    {
      "id": "01",
      "title": "Ядро: камера, настройки, обработка, тесты",
      "requirements": [
        "D01",
        "R03",
        "R04",
        "R07",
        "R08",
        "R09",
        "R10",
        "R10.3",
        "R17i.1",
        "R03.2"
      ],
      "blockedBy": [],
      "wave": 1,
      "zone": [
        "src/LumeneraViewer/Camera/",
        "Settings/",
        "Imaging/",
        "csproj",
        "tests/"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-07T18:58:38+05:00",
      "repairFindings": [
        "SettingsStore: исключение в отложенном сохранении роняет процесс; сериализация живого объекта",
        "LuCamCamera: защита от короткого буфера и закрытия хэндла во время конвертации (решение оркестратора — не воспроизводится без камеры)"
      ],
      "finishedAt": "2026-10-07T19:11:41+05:00",
      "commit": "deee21b",
      "tests": {
        "passed": 22,
        "failed": 0
      },
      "files": [
        "Camera/",
        "Settings/",
        "Imaging/",
        "tests/"
      ]
    },
    {
      "id": "02",
      "title": "Оболочка в стиле reactbits, превью, демо-режим",
      "requirements": [
        "R03",
        "R03.1",
        "R03.2",
        "R03.3",
        "R04",
        "R10.4",
        "R11",
        "R11.1",
        "R12",
        "R17i",
        "R17i.1",
        "R17i.2",
        "A05"
      ],
      "blockedBy": [
        "01"
      ],
      "wave": 2,
      "zone": [
        "App",
        "MainWindow",
        "ViewModels/",
        "Views/",
        "UI/"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-07T19:11:41+05:00",
      "finishedAt": "2026-10-07T19:28:03+05:00",
      "commit": "ffb107c",
      "tests": {
        "passed": 22,
        "failed": 0
      },
      "files": [
        "App",
        "MainWindow",
        "ViewModels/",
        "Views/",
        "UI/"
      ]
    },
    {
      "id": "03",
      "title": "Драйверы из комплекта и папка для переноса",
      "requirements": [
        "R01",
        "R02",
        "R02.1",
        "R02.2",
        "R02.3",
        "R02.4",
        "R02.5",
        "R02.6",
        "R16i",
        "R11.2"
      ],
      "blockedBy": [
        "02"
      ],
      "wave": 3,
      "zone": [
        "Drivers/",
        "publish.ps1",
        "installer/",
        "README.md"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-07T19:28:03+05:00",
      "finishedAt": "2026-10-07T19:39:51+05:00",
      "commit": "dca33ac",
      "tests": {
        "passed": 73,
        "failed": 0
      }
    },
    {
      "id": "04",
      "title": "Экспозиция, цвет, гистограмма, фокус, пресеты",
      "requirements": [
        "R07",
        "R07.1",
        "R07.2",
        "R08",
        "R09",
        "R10",
        "R10.1",
        "R10.2",
        "A01",
        "A02",
        "A08"
      ],
      "blockedBy": [
        "02"
      ],
      "wave": 3,
      "zone": [
        "CameraPanel",
        "ColorPanel",
        "UI/Controls/"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-07T19:28:03+05:00",
      "finishedAt": "2026-10-07T19:39:51+05:00",
      "commit": "b74a99c",
      "tests": {
        "passed": 73,
        "failed": 0
      }
    },
    {
      "id": "05",
      "title": "Плашка масштаба, калибровка, линейка, сетка",
      "requirements": [
        "R06",
        "R06.1",
        "R06.2",
        "R15i",
        "R15i.1",
        "R15i.2",
        "A03",
        "A04"
      ],
      "blockedBy": [
        "02"
      ],
      "wave": 3,
      "zone": [
        "Overlays/",
        "ScalePanel",
        "MeasurePanel",
        "PreviewOverlay"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-07T19:28:03+05:00",
      "repairFindings": [
        "подпись плашки «0 мм» при малых длинах (плашка врёт — R15i.2)",
        "калибровка пишется в объектив, выбранный после диалога"
      ],
      "finishedAt": "2026-10-07T19:45:33+05:00",
      "commit": "496f988",
      "tests": {
        "passed": 88,
        "failed": 0
      }
    },
    {
      "id": "06",
      "title": "Сохранение, тайм-лапс, лента снимков",
      "requirements": [
        "R05",
        "R05.1",
        "R05.2",
        "R05.3",
        "R05.4",
        "A06",
        "A07"
      ],
      "blockedBy": [
        "05"
      ],
      "wave": 4,
      "zone": [
        "Export/",
        "CapturePanel",
        "GalleryStrip"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-07T19:43:24+05:00",
      "finishedAt": "2026-10-07T22:55:53+05:00",
      "commit": "a55a6a3",
      "tests": {
        "passed": 108,
        "failed": 0
      }
    },
    {
      "id": "07",
      "title": "Шапка: панель статуса вместо капсул",
      "requirements": [
        "G01",
        "R11",
        "R03.1"
      ],
      "blockedBy": [
        "02"
      ],
      "wave": 4,
      "zone": [
        "MainWindow.xaml (шапка)",
        "UI/Controls/"
      ],
      "status": "in-progress",
      "startedAt": "2026-10-07T19:55:02+05:00",
      "retries": 1,
      "repairs": 0,
      "handoffs": 0
    }
  ],
  "singlePass": null,
  "tests": {
    "passed": 108,
    "failed": 0
  },
  "debt": {
    "placeholders": [
      "R15i — мкм/пиксель объективов: калибрует пользователь в программе"
    ],
    "assumptions": [
      "Коррекция цвета программная (одинакова для камеры и демо); экспозиция и усиление — аппаратные",
      "Установщик драйверов не коммитится в git (проприетарный, 39 МБ)"
    ],
    "emptyEnv": []
  },
  "additions": [
    "A01 гистограмма → R07",
    "A02 фокус-ассистент → R03",
    "A03 линейка → R06",
    "A04 сетка/перекрестие → R03",
    "A05 пауза кадра → R03",
    "A06 тайм-лапс → R05",
    "A07 лента снимков → R05",
    "A08 пресеты → R07"
  ],
  "coverage": {
    "findings": 8,
    "missing": 2,
    "half": 6,
    "fixed": 8,
    "note": "дописаны: окружение разработчика (ист. 49), формулы коррекции, цель АЭ, шрифт плашки, шаг сетки, параметры эффектов, раздел «Дополнительно» (ист. 48); сверх брифа — только R##.n/R##i и A01–A08 → R13"
  },
  "concerns": [
    "Camera/LuCamCamera.cs — сигнатуры LuCam не проверены на живой камере (нет устройства)",
    "Imaging/ImageProcessor.cs:208 — GainsToTemperatureTint нормирует на (r+b)/2 перед формулой (законная трактовка спеки)",
    "Camera/LuCamCamera.cs:93 — Stop ждёт поток конвертации без таймаута; UI обязан использовать только BeginInvoke",
    "ViewModels/MainViewModel.cs:482 — 8 МБ LOH на кадр при не-identity коррекции (~200 МБ/с), нужен пул/ротация буферов",
    "ViewModels/MainViewModel.cs:559 — Shutdown закрывает камеру синхронно в UI-потоке (Join без таймаута) — риск зависания при закрытии",
    "ViewModels/MainViewModel.cs:298 — закрытие окна во время Open/Start оставляет камеру открытой",
    "ViewModels/MainViewModel.cs:39 — обработчики событий камеры в общих полях, а не привязаны к экземпляру",
    "ViewModels/MainViewModel.cs:80 — CompositionTarget.Rendering подписан всегда",
    "ViewModels/MainViewModel.cs:547 — ResetSettings копирует свойства рефлексией",
    "UI/Controls/ZoomPanHost.cs:84 — BeginInvoke на каждом ArrangeOverride",
    "UI/Effects/SpotlightCard.cs:403 — новые кисти на каждое движение мыши",
    "ViewModels/MainViewModel.cs:287 — «Повторить» из демо без камеры закрывает работающий демо-режим",
    "R04 — раздел «Как добавить свою функцию» в CLAUDE.md не был в тасках: пишет оркестратор в Phase 8",
    "ViewModels/CameraPanelViewModel.cs:209 — MeanLuma автоэкспозиции считается в потоке камеры; таймер ожидания не отменяется; RunAuto не отменяется при смене камеры",
    "ViewModels/ColorPanelViewModel.cs:66 — Presets не перечитываются после «Сбросить настройки» → удалённые пресеты возвращаются",
    "ViewModels/ColorPanelViewModel.cs:89 — SaveSettingsSoon сериализует AppSettings на каждый тик слайдера в UI-потоке",
    "ViewModels/ColorPanelViewModel.cs:197 — ContentDialogHost без проверки на null (в отличие от других мест)",
    "Drivers/DriverService.cs:88 — по таймауту процесс установщика не завершается, а мастер предлагается сразу (риск MSI 1618)",
    "Drivers/DriverStartupFlow.cs:60 — обещание «камера будет отключена» не выполняется: камера не закрывается перед установкой",
    "Drivers/DriverStartupFlow.cs:143 — гонка: быстрый отказ UAC может оставить диалог прогресса без кнопок",
    "Drivers/DriverService.cs:83 — повторная проверка DLL видит PATH на момент старта процесса",
    "UI/Controls/HistogramView.cs:302 — геометрия Luma строится дважды",
    "Overlays — WYSIWYG: размер плашки в файле (W/1600) меньше, чем на экране «вписать»; CustomPosition от левого верхнего угла",
    "ViewModels/ScalePanelViewModel.cs:314 — третья копия сборки ContentDialog, нужен общий помощник",
    "Views/PreviewOverlay.xaml.cs:91 — между кликами линейки мышь не захвачена: клик вне кадра уходит в панораму",
    "ViewModels/ScalePanelViewModel.cs:26 — ObjectiveNames не перечитываются после «Сбросить настройки»",
    "Плашка: список угла показывает угол, хотя плашка перетащена",
    "Сессию прервал лимит API (~19:55–22:54): таск 07 перезапущен с нуля, ревью 06 повторено",
    "ViewModels/GalleryViewModel.cs:146 — «Удалить» оставляет копию _clean сиротой",
    "ViewModels/GalleryViewModel.cs:157 — четвёртая копия ContentDialog-подтверждения",
    "ViewModels/CapturePanelViewModel.cs:255 — старый цикл тайм-лапса может затереть счётчик новой серии",
    "ViewModels/CapturePanelViewModel.cs:24 — FrameExporter/StaWorker не освобождаются",
    "ViewModels/CapturePanelViewModel.cs:313 — при закрытии окно молча ждёт запись до 10 с",
    "Export/FrameExporter.Save.cs:169 — уникальность имени не учитывает _clean (безопасно лишь при одном потоке записи)"
  ],
  "reviewers": {
    "manifestSpec": "accc3d70277f71405",
    "craft": "aa13cd7782a4444a5"
  },
  "blind": null
}
