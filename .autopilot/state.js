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
  "updatedAt": "2026-10-07T19:26:24+05:00",
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
      "note": "1 из 6 тасков готов"
    },
    {
      "id": "review",
      "status": "active",
      "startedAt": "2026-10-07T19:08:00+05:00",
      "note": "проверено 1 из 6"
    },
    {
      "id": "final",
      "status": "pending"
    }
  ],
  "requirements": {
    "total": 18,
    "done": 0,
    "inTicket": 17,
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
      "status": "review",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-07T19:11:41+05:00"
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
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
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
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
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
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
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
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
    }
  ],
  "singlePass": null,
  "tests": {
    "passed": 22,
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
    "UI/Effects/SpotlightCard.cs:403 — новые кисти на каждое движение мыши"
  ],
  "reviewers": {
    "manifestSpec": "accc3d70277f71405",
    "craft": "aa13cd7782a4444a5"
  },
  "blind": null
}
