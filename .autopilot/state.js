window.STATE =
{
  "slug": "zi-scope-iterations",
  "dir": "2026-10-08-zi-scope-iterations--wip",
  "title": "ZI-scope: серии съёмки, новый дизайн, глубокий цвет, профили",
  "mode": "semi",
  "depth": "normal",
  "polish": null,
  "tier": "T2",
  "briefFile": "2026-10-08-brief.md",
  "memoryFile": "CLAUDE.md",
  "skillDir": "C:/Users/Ярослав/.agents/skills/autopilot",
  "startedAt": "2026-10-08T13:25:59+05:00",
  "updatedAt": "2026-10-08T18:35:16+05:00",
  "finishedAt": null,
  "stages": [
    {
      "id": "preflight",
      "status": "done",
      "startedAt": "2026-10-08T13:25:59+05:00",
      "finishedAt": "2026-10-08T13:26:30+05:00"
    },
    {
      "id": "manifest",
      "status": "done",
      "startedAt": "2026-10-08T13:26:30+05:00",
      "finishedAt": "2026-10-08T13:40:19+05:00"
    },
    {
      "id": "briefing",
      "status": "done",
      "finishedAt": "2026-10-08T13:40:19+05:00",
      "note": "4 вопроса",
      "startedAt": "2026-10-08T13:27:00+05:00"
    },
    {
      "id": "spec",
      "status": "done",
      "startedAt": "2026-10-08T13:40:19+05:00",
      "finishedAt": "2026-10-08T13:44:07+05:00"
    },
    {
      "id": "plan",
      "status": "done",
      "startedAt": "2026-10-08T13:44:07+05:00",
      "finishedAt": "2026-10-08T13:44:07+05:00",
      "note": "8 тасков, ярус T2, 4 волны (07–08 — дополнения пользователя)"
    },
    {
      "id": "build",
      "status": "active",
      "startedAt": "2026-10-08T13:44:07+05:00",
      "note": "3 из 8 тасков готовы (+2 по дополнениям)"
    },
    {
      "id": "review",
      "status": "active",
      "startedAt": "2026-10-08T13:53:09+05:00",
      "note": "проверено 3 из 6"
    },
    {
      "id": "final",
      "status": "pending"
    }
  ],
  "requirements": {
    "total": 32,
    "done": 0,
    "inTicket": 31,
    "inSpec": 1,
    "placeholder": 0,
    "deferred": 0,
    "dropped": 0
  },
  "tickets": [
    {
      "id": "01",
      "title": "ZI-scope: переименование, тема с синим акцентом, вкладки",
      "requirements": [
        "R18",
        "R01",
        "R02",
        "R03",
        "R04"
      ],
      "blockedBy": [],
      "wave": 1,
      "zone": [
        "MainWindow",
        "UI/",
        "Views (стили)",
        "csproj",
        "publish.ps1",
        "README"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-08T13:44:07+05:00",
      "finishedAt": "2026-10-08T14:02:02+05:00",
      "commit": "1336cfd",
      "tests": {
        "passed": 195,
        "failed": 0
      }
    },
    {
      "id": "02",
      "title": "Движок глубокого цвета",
      "requirements": [
        "R14",
        "R15"
      ],
      "blockedBy": [],
      "wave": 1,
      "zone": [
        "Imaging/"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-08T13:44:07+05:00",
      "repairFindings": [
        "ChannelLevels.Map бросает исключение при Black>254 (падение обработки/экспорта)",
        "ToneCurve с null-точками → NRE; кламп EV; тест параллельного Apply"
      ],
      "finishedAt": "2026-10-08T13:58:06+05:00",
      "commit": "86af0fa",
      "tests": {
        "passed": 187,
        "failed": 0
      }
    },
    {
      "id": "03",
      "title": "Модель: увеличения, профили, серия, папка настроек",
      "requirements": [
        "R12",
        "R13",
        "R16",
        "R17",
        "R18",
        "R05",
        "R06",
        "R09",
        "R10",
        "R11",
        "R19",
        "R20",
        "R21",
        "R23i"
      ],
      "blockedBy": [],
      "wave": 1,
      "zone": [
        "Settings/",
        "Series/",
        "Export/SaveExact"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-08T13:44:07+05:00",
      "repairFindings": [
        "решение оркестратора (не BLOCKING, но под ними встанет UI серии): тип исключения при пропавшей папке, проверка format, дубли увеличений, перенос пресетов без потерь, повторная миграция, старый .json при перезаписи"
      ],
      "finishedAt": "2026-10-08T14:02:02+05:00",
      "commit": "1c47425",
      "tests": {
        "passed": 195,
        "failed": 0
      }
    },
    {
      "id": "04",
      "title": "Увеличения в шапке, цвет и масштаб по увеличению, профили",
      "requirements": [
        "R12",
        "R13",
        "R25i",
        "R16",
        "R17"
      ],
      "blockedBy": [
        "01",
        "03"
      ],
      "wave": 2,
      "zone": [
        "MainViewModel",
        "MagnificationBar",
        "ScalePanel",
        "ColorPanel",
        "ConfigProfiles"
      ],
      "status": "in-progress",
      "retries": 0,
      "repairs": 0,
      "handoffs": 1,
      "startedAt": "2026-10-08T14:02:02+05:00"
    },
    {
      "id": "05",
      "title": "Окно «Глубокие настройки цвета»",
      "requirements": [
        "R14",
        "R15"
      ],
      "blockedBy": [
        "02",
        "04"
      ],
      "wave": 3,
      "zone": [
        "DeepColorWindow",
        "ToneCurveEditor"
      ],
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
    },
    {
      "id": "06",
      "title": "Серия: конструктор, прогон, карта, пересъёмка",
      "requirements": [
        "R05",
        "R06",
        "R07",
        "R08",
        "R09",
        "R10",
        "R11",
        "R19",
        "R20",
        "R21",
        "R23i",
        "R24i",
        "R25i"
      ],
      "blockedBy": [
        "03",
        "04"
      ],
      "wave": 3,
      "zone": [
        "Series UI",
        "SeriesMap"
      ],
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
    },
    {
      "id": "07",
      "title": "Калибровка по снимку, таблица мкм/px, сетка только на экране",
      "requirements": [
        "G01",
        "G02",
        "G03",
        "G04",
        "G05"
      ],
      "blockedBy": [
        "04"
      ],
      "wave": 3,
      "zone": [
        "Calibration/",
        "CalibrationFromImageWindow",
        "PixelSizeTable",
        "ScalePanel",
        "OverlayRenderer (сетка)",
        "csproj TFM"
      ],
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
    },
    {
      "id": "08",
      "title": "Цвета тёмного Gemini и меню слева",
      "requirements": [
        "G06",
        "G07"
      ],
      "blockedBy": [
        "05",
        "06",
        "07"
      ],
      "wave": 4,
      "zone": [
        "UI/Effects/",
        "MainWindow (раскладка)"
      ],
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
    }
  ],
  "singlePass": null,
  "tests": {
    "passed": 195,
    "failed": 0
  },
  "debt": {
    "placeholders": [
      "R12.1 — какая старая калибровка какому увеличению: назначает пользователь"
    ],
    "assumptions": [
      "Пространства имён и имена проектов остаются LumeneraViewer (пользователь их не видит)",
      "Тональная кривая включена; «Калибровка камеры» Lightroom — вне рамок"
    ],
    "emptyEnv": []
  },
  "additions": [],
  "coverage": {
    "findings": 4,
    "missing": 1,
    "half": 3,
    "fixed": 4,
    "note": "добавлена тональная кривая (Lightroom); длина плашки по увеличению; место переноса старых калибровок; добавление/удаление увеличений. «Калибровка камеры» Lightroom — в «Вне рамок». Остальное «сверх брифа» — углубления R##.n"
  },
  "concerns": [
    "Imaging/ImageProcessor.Local.cs:20 — буферы размытия (~170 МБ на 4008×2672) живут до конца работы и только растут",
    "Export.Prefix по умолчанию «Lumenera» — не переименован в ZI-scope; при overwrite без метаданных старый .json остаётся",
    "Settings/SettingsStore.cs Migrate — ручная длина плашки (FixedMicrons) при первом переходе сбрасывается, а не переносится в профиль текущего увеличения",
    "Settings/SettingsStore.cs Migrate — пресет с именем, совпадающим с профилем цвета, отбрасывается",
    "Атомарная запись (tmp→rename) повторена в ProfileFiles, SeriesStore и FrameExporter; Flush(true) только в FrameExporter",
    "CLAUDE.md устарел после темы (SpotlightCard в рецепте, dist\\LumeneraViewer, Mica) — обновить в финале",
    "CameraPanel: «NaN % от максимума» в фокус-ассистенте без камеры",
    "MainWindow.xaml:145 — цвета плашки «ПАУЗА» захардкожены в обход Zs.*",
    "UI/Converters.cs:32 — Green/Yellow/Red дублируют Palette.Success/Warning/Error",
    "MainWindow.xaml.cs ShowPage/SelectTab — (string)Tag/ToolTip бросит на странице без строкового Tag",
    "Export/FrameExporter.Save.cs — File.Delete старого .json после записи кадра может бросить, хотя кадр сохранён",
    "Лимит API прервал таск 04 (~14:10–18:20); продолжен тем же исполнителем"
  ],
  "reviewers": {
    "manifestSpec": "a5b01cc46c7d842fd",
    "craft": "a37291edf1fbf8844"
  },
  "blind": null,
  "resumeNotes": {
    "ticket04": "в работе преемник (agent a8b180c28b59cda39) после handoff-04-1.md; дальше: ревью 04 → коммит → волна 3: 05, 06, 07 параллельно → ревью → 08 (Gemini+слева) → финал (слепая приёмка, CLAUDE.md, ADR, отчёт)",
    "reviewers": "manifestSpec a5b01cc46c7d842fd, craft a37291edf1fbf8844 (если не отвечают — новые)",
    "userSettingsNote": "в %AppData%\\LumeneraViewer\\settings.json тестовые данные (PreferDemo, 10x 0.821) — пользователь не решил удалять; не трогать"
  }
}
