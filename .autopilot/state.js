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
  "updatedAt": "2026-10-08T13:59:38+05:00",
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
      "note": "6 тасков, ярус T2, 3 волны"
    },
    {
      "id": "build",
      "status": "active",
      "startedAt": "2026-10-08T13:44:07+05:00",
      "note": "1 из 6 тасков готов"
    },
    {
      "id": "review",
      "status": "active",
      "startedAt": "2026-10-08T13:53:09+05:00"
    },
    {
      "id": "final",
      "status": "pending"
    }
  ],
  "requirements": {
    "total": 25,
    "done": 0,
    "inTicket": 24,
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
      "status": "review",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0,
      "startedAt": "2026-10-08T13:44:07+05:00"
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
      "status": "repair",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-08T13:44:07+05:00",
      "repairFindings": [
        "решение оркестратора (не BLOCKING, но под ними встанет UI серии): тип исключения при пропавшей папке, проверка format, дубли увеличений, перенос пресетов без потерь, повторная миграция, старый .json при перезаписи"
      ]
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
      "status": "pending",
      "retries": 0,
      "repairs": 0,
      "handoffs": 0
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
    }
  ],
  "singlePass": null,
  "tests": null,
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
    "CameraPanel: «NaN % от максимума» в фокус-ассистенте без камеры"
  ],
  "reviewers": {
    "manifestSpec": "a5b01cc46c7d842fd",
    "craft": "a37291edf1fbf8844"
  },
  "blind": null
}
