window.STATE =
{
  "slug": "zi-scope-iterations",
  "dir": "2026-10-08-zi-scope-iterations",
  "title": "ZI-scope: серии съёмки, новый дизайн, глубокий цвет, профили",
  "mode": "semi",
  "depth": "normal",
  "polish": null,
  "tier": "T2",
  "briefFile": "2026-10-08-brief.md",
  "memoryFile": "CLAUDE.md",
  "skillDir": "C:/Users/Ярослав/.agents/skills/autopilot",
  "startedAt": "2026-10-08T13:25:59+05:00",
  "updatedAt": "2026-10-09T10:17:03+05:00",
  "finishedAt": "2026-10-09T10:17:03+05:00",
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
      "status": "done",
      "startedAt": "2026-10-08T13:44:07+05:00",
      "note": "9 из 9 тасков готовы",
      "finishedAt": "2026-10-08T23:57:52+05:00"
    },
    {
      "id": "review",
      "status": "done",
      "startedAt": "2026-10-08T13:53:09+05:00",
      "note": "9 из 9 тасков готовы",
      "finishedAt": "2026-10-08T23:57:52+05:00"
    },
    {
      "id": "final",
      "status": "done",
      "startedAt": "2026-10-08T23:57:52+05:00",
      "finishedAt": "2026-10-09T10:17:03+05:00",
      "note": "слепая приёмка: 17/17 реализовано, расхождений нет"
    }
  ],
  "requirements": {
    "total": 32,
    "done": 32,
    "inTicket": 0,
    "inSpec": 0,
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
      "status": "done",
      "retries": 0,
      "repairs": 0,
      "handoffs": 1,
      "startedAt": "2026-10-08T14:02:02+05:00",
      "finishedAt": "2026-10-08T18:44:04+05:00",
      "commit": "dea8333",
      "tests": {
        "passed": 197,
        "failed": 0
      }
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
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-08T18:44:04+05:00",
      "repairFindings": [
        "решение оркестратора: позиция окна и последнее изменение теряются при выходе; изменение <33 мс до смены увеличения отбрасывается; кисти в OnRender; цвета каналов в Palette; тесты CurveEditing"
      ],
      "commit": "45a90d7",
      "finishedAt": "2026-10-08T23:24:32+05:00",
      "tests": {
        "passed": 281,
        "failed": 0
      }
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
      "status": "done",
      "retries": 0,
      "repairs": 2,
      "handoffs": 0,
      "startedAt": "2026-10-08T18:44:04+05:00",
      "repairFindings": [
        "решение оркестратора: откат частичной записи шага; лишний вид при пересъёмке без масштаба; Ctrl+Z во время съёмки/диалога; оверлей вместе с кадром; точные Persist в тестах"
      ],
      "commit": "d44b4af",
      "finishedAt": "2026-10-08T23:37:32+05:00",
      "tests": {
        "passed": 286,
        "failed": 0
      }
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
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 1,
      "startedAt": "2026-10-08T18:44:04+05:00",
      "commit": "56ddaf9",
      "finishedAt": "2026-10-08T23:38:40+05:00",
      "tests": {
        "passed": 290,
        "failed": 0
      }
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
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-08T23:38:40+05:00",
      "repairFindings": [
        "доводка: тройная подсказка «Переключите микроскоп», разрыв «мкм/px», обрезанные подписи конструктора серии, (string)Tag, Pill на внутренних кнопках WPF-UI, scrim из палитры, тест контраста WCAG, скриншоты калибровки и профилей"
      ],
      "commit": "138666c",
      "finishedAt": "2026-10-08T23:57:52+05:00",
      "tests": {
        "passed": 311,
        "failed": 0
      }
    },
    {
      "id": "09",
      "title": "Доводка по отложенным замечаниям",
      "requirements": [
        "R18",
        "R17"
      ],
      "blockedBy": [
        "01",
        "02",
        "03",
        "04",
        "05",
        "06",
        "07",
        "08"
      ],
      "wave": 5,
      "zone": [
        "Export.Prefix",
        "фокус-ассистент",
        "атомарная запись",
        "профили при --settings",
        "PaletteTests"
      ],
      "status": "done",
      "retries": 0,
      "repairs": 1,
      "handoffs": 0,
      "startedAt": "2026-10-09T00:07:57+05:00",
      "commit": "22843bd",
      "finishedAt": "2026-10-09T10:17:03+05:00",
      "tests": {
        "passed": 327,
        "failed": 0
      }
    }
  ],
  "singlePass": null,
  "tests": {
    "passed": 327,
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
    "Лимит API прервал таск 04 (~14:10–18:20); продолжен тем же исполнителем",
    "Ревью 04 (крафт): правки автоэкспозиции/измерений/UI-потока/синхронизации списка увеличений сделаны оркестратором сам (нарушение правила 5 — в финальный отчёт)",
    "Ревью 04: ScalePanel.Reload вызывается дважды (ApplyLive + SettingsReplaced); лишний StoreLive в ConfigProfilesViewModel.SaveCurrent",
    "Ревью 04: перенос старой калибровки — только в увеличения, у которых уже есть профиль; «перенесено» определяется по совпадению мкм/px",
    "Ревью 04: метаданные снимка — поле Objective теперь «×200»; в уведомлении «мкм/ px» переносится",
    "Исполнитель 04 оставил вне проекта: %AppData%\\ZI-scope\\profiles\\Конфигурация 1.zscope, %TEMP%\\zs-04\\, %TEMP%\\zs-04-backup\\ — решение за пользователем",
    "Ревью 05: дорожки HSL голубой/фиолетовый/пурпурный — цвета сектора оттенка (не акцент), сказать пользователю",
    "Ревью 05: окно глубоких настроек при первом открытии частично закрывает превью; гистограмма — после коррекции",
    "Параллельные ручные проверки с бэкапом/восстановлением одного settings.json конфликтуют — в будущих волнах ручной запуск сериализовать или давать программе путь настроек в scratch",
    "Ревью 05 (повтор): при LoadConfiguration/ResetSettings несброшенная (<33 мс) правка DeepColor пишется в профиль загруженной конфигурации (DeepColorViewModel.cs:218-248)",
    "Таск 08: HistogramView RGB и Palette.HslSectors — ссылаться на Palette.Channel*/Error/Success/Cyan/Accent, не дублировать значения",
    "Ревью 06 (3-й проход): неудачный откат из .zs-new\\old проглатывается; откат только для IO/UnauthorizedAccess; нет теста на UnauthorizedAccess",
    "Ревью 07: калибровка по снимку пересчитывается к текущей ширине кадра, не сенсора — при смене разрешения мкм/px неверен (сказать пользователю)",
    "Вне автопилота (по просьбе пользователя): уведомления — журнал в отдельном окне + колокольчик; ключ --settings для проверок без %AppData%",
    "Ревью 08: в тест контраста добавить TextDisabled и Accent на Panel; затемнение под диалогами стало сильнее (α 0.85)",
    "Профили конфигурации всегда в %AppData%\\ZI-scope\\profiles — ключ --settings их не переносит"
  ],
  "reviewers": {
    "manifestSpec": "a5b01cc46c7d842fd",
    "craft": "a37291edf1fbf8844"
  },
  "blind": {
    "verdict": "все 17 пунктов брифа и дополнений — реализовано",
    "drift": [],
    "notes": [
      "профили конфигурации: сохранение не проверено (писало бы в %AppData%)",
      "калибровка по снимку: подпись показывается как «100 pm», число и единица верны; проверен только жёлтый цвет"
    ],
    "commands": [
      "dotnet build LumeneraViewer.sln -c Debug → 0/0",
      "dotnet test LumeneraViewer.sln → 311+1 skip",
      "ZI-scope.exe --settings scratch\\zs-g4\\settings.json → демо 25 к/с"
    ]
  },
  "resumeNotes": {
    "done": "прогон сдан 2026-10-09T10:17:03+05:00"
  },
  "concernsTriage": {
    "fixNow": [
      2,
      5,
      7,
      11,
      26,
      27
    ],
    "drop": {
      "6": "переписывает агент памяти в финале",
      "8": "исправлено в 08",
      "9": "исправлено в 08",
      "10": "исправлено в 08",
      "12": "служебная заметка",
      "20": "решено ключом --settings",
      "22": "исправлено в 08",
      "25": "учтено как добавление"
    },
    "report": [
      1,
      3,
      4,
      13,
      14,
      15,
      16,
      17,
      18,
      19,
      21,
      23,
      24
    ]
  }
}
