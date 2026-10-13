window.STATE =
{
  "version": 2,
  "slug": "series-projects",
  "dir": "2026-10-13-series-projects--wip",
  "title": "ZI-scope: проекты серий .zisc без .json у каждого кадра",
  "mode": "semi",
  "depth": "normal",
  "tier": "T0",
  "briefFile": "2026-10-13-brief.md",
  "memoryFile": "CLAUDE.md",
  "memoryOwner": "autopilot",
  "skillDir": "C:/Users/Ярослав/.claude/skills/autopilot",
  "baseCommit": null,
  "startedAt": "2026-10-13T19:17:46+05:00",
  "updatedAt": "2026-10-13T19:27:48+05:00",
  "finishedAt": null,
  "stages": [
    {
      "id": "preflight",
      "status": "done",
      "startedAt": "2026-10-13T19:17:46+05:00",
      "finishedAt": "2026-10-13T19:18:01+05:00"
    },
    {
      "id": "manifest",
      "status": "done",
      "startedAt": "2026-10-13T19:18:01+05:00",
      "finishedAt": "2026-10-13T19:18:22+05:00"
    },
    {
      "id": "briefing",
      "status": "done",
      "startedAt": "2026-10-13T19:18:22+05:00",
      "finishedAt": "2026-10-13T19:21:54+05:00"
    },
    {
      "id": "spec",
      "status": "done",
      "startedAt": "2026-10-13T19:21:54+05:00",
      "finishedAt": "2026-10-13T19:27:22+05:00"
    },
    {
      "id": "plan",
      "status": "active",
      "startedAt": "2026-10-13T19:27:22+05:00"
    },
    {
      "id": "build",
      "status": "pending"
    },
    {
      "id": "review",
      "status": "pending"
    },
    {
      "id": "final",
      "status": "pending"
    }
  ],
  "requirements": {
    "total": 10,
    "done": 0,
    "inTicket": 9,
    "inSpec": 0,
    "placeholder": 0,
    "deferred": 0,
    "dropped": 1
  },
  "tickets": [
    {
      "id": "01",
      "title": "Проект серии .zisc, метаданные кадров одним списком",
      "requirements": [
        "R01",
        "R02",
        "R03",
        "R04",
        "R05",
        "R06",
        "R07",
        "R08",
        "R09"
      ],
      "blockedBy": [],
      "wave": 1,
      "zone": [
        "src/LumeneraViewer/Export/",
        "src/LumeneraViewer/Series/",
        "src/LumeneraViewer/ViewModels/SeriesRunner.cs",
        "src/LumeneraViewer/ViewModels/SeriesPanelViewModel.cs",
        "src/LumeneraViewer/Views/SeriesPanel.xaml",
        "tests/LumeneraViewer.Tests/"
      ],
      "review": "нет",
      "model": "сильная",
      "status": "pending",
      "retries": 0,
      "repairs": 0
    }
  ],
  "tests": null,
  "debt": {
    "placeholders": [],
    "assumptions": [],
    "emptyEnv": []
  },
  "additions": [],
  "coverage": {
    "found": 3,
    "fixed": 3,
    "deferred": 0,
    "items": []
  },
  "concerns": [],
  "blind": null,
  "beats": [
    "2026-10-13T19:17:46+05:00",
    "2026-10-13T19:18:01+05:00",
    "2026-10-13T19:18:20+05:00",
    "2026-10-13T19:18:22+05:00",
    "2026-10-13T19:18:56+05:00",
    "2026-10-13T19:21:52+05:00",
    "2026-10-13T19:21:54+05:00",
    "2026-10-13T19:22:06+05:00",
    "2026-10-13T19:22:08+05:00",
    "2026-10-13T19:23:11+05:00",
    "2026-10-13T19:27:19+05:00",
    "2026-10-13T19:27:22+05:00",
    "2026-10-13T19:27:45+05:00",
    "2026-10-13T19:27:46+05:00",
    "2026-10-13T19:27:48+05:00"
  ],
  "rev": 16,
  "waiting": null,
  "waits": [
    {
      "kind": "briefing",
      "from": "2026-10-13T19:18:56+05:00",
      "to": "2026-10-13T19:21:52+05:00"
    },
    {
      "kind": "briefing",
      "from": "2026-10-13T19:22:08+05:00",
      "to": "2026-10-13T19:23:11+05:00"
    }
  ],
  "log": [
    {
      "t": "2026-10-13T19:17:46+05:00",
      "k": "init"
    },
    {
      "t": "2026-10-13T19:18:01+05:00",
      "k": "stage",
      "id": "manifest",
      "act": "start"
    },
    {
      "t": "2026-10-13T19:18:22+05:00",
      "k": "stage",
      "id": "briefing",
      "act": "start"
    },
    {
      "t": "2026-10-13T19:18:56+05:00",
      "k": "ask",
      "kind": "briefing",
      "note": "два вопроса — старые серии, имя файла .zisc"
    },
    {
      "t": "2026-10-13T19:21:52+05:00",
      "k": "answered",
      "kind": "briefing"
    },
    {
      "t": "2026-10-13T19:21:54+05:00",
      "k": "gate",
      "g": "G1",
      "ok": false,
      "open": 9,
      "total": 10
    },
    {
      "t": "2026-10-13T19:21:54+05:00",
      "k": "stage",
      "id": "spec",
      "act": "start"
    },
    {
      "t": "2026-10-13T19:22:06+05:00",
      "k": "stage",
      "id": "briefing",
      "act": "start"
    },
    {
      "t": "2026-10-13T19:22:08+05:00",
      "k": "ask",
      "kind": "briefing",
      "note": "два вопроса — старые серии, имя файла .zisc"
    },
    {
      "t": "2026-10-13T19:23:11+05:00",
      "k": "answered",
      "kind": "briefing"
    },
    {
      "t": "2026-10-13T19:23:11+05:00",
      "k": "gate",
      "g": "G1",
      "ok": false,
      "open": 9,
      "total": 10
    },
    {
      "t": "2026-10-13T19:23:11+05:00",
      "k": "stage",
      "id": "spec",
      "act": "start"
    },
    {
      "t": "2026-10-13T19:27:19+05:00",
      "k": "gate",
      "g": "G2",
      "ok": true,
      "found": 3,
      "fixed": 3,
      "deferred": 0
    },
    {
      "t": "2026-10-13T19:27:22+05:00",
      "k": "stage",
      "id": "plan",
      "act": "start"
    },
    {
      "t": "2026-10-13T19:27:45+05:00",
      "k": "tier",
      "v": "T0"
    },
    {
      "t": "2026-10-13T19:27:46+05:00",
      "k": "tickets",
      "n": 1
    },
    {
      "t": "2026-10-13T19:27:48+05:00",
      "k": "gate",
      "g": "G3",
      "ok": true,
      "problems": 0
    }
  ],
  "reqList": [
    {
      "id": "R01",
      "text": "«мне не нравится json файл к каждому файлу в серии, надо это пофиксить»",
      "status": "in-ticket"
    },
    {
      "id": "R02",
      "text": "«в серии в .zisc одним списком» (метаданные всех кадров)",
      "status": "in-ticket"
    },
    {
      "id": "R03",
      "text": "«Ещё хочу чтобы были проекты серий»",
      "status": "in-ticket"
    },
    {
      "id": "R04",
      "text": "«я заходил в папку с серией, а там буде .zisc файл»",
      "status": "in-ticket"
    },
    {
      "id": "R05",
      "text": "«.zisc (заменяет series.zscope-series.json)»",
      "status": "in-ticket"
    },
    {
      "id": "R06",
      "text": "«при открытие его можно было продолжить серию»",
      "status": "in-ticket"
    },
    {
      "id": "R07",
      "text": "«кнопка «Открыть проект серии…» продолжает серию с того же шага»",
      "status": "in-ticket"
    },
    {
      "id": "R08",
      "text": "«Обычные снимки не трогать — у них свой выключатель метаданных»",
      "status": "in-ticket"
    },
    {
      "id": "R09",
      "text": "«через открыть проект серии, пока без реестров» / «Ассоциацию .zisc в реестре Windows не делать»",
      "status": "in-ticket"
    },
    {
      "id": "R10i",
      "text": "(подразумевается) уже начатые серии со старым series.zscope-series.json и .json у кадров тоже можно продолжить",
      "status": "dropped",
      "why": "пользователь: «Забей, старые серии были только для тестов, в продакшене серии ещё небыли.»"
    }
  ],
  "gates": {
    "G1": {
      "ok": false,
      "at": "2026-10-13T19:23:11+05:00",
      "open": 9,
      "total": 10
    },
    "G2": {
      "ok": true,
      "at": "2026-10-13T19:27:19+05:00",
      "found": 3,
      "fixed": 3,
      "deferred": 0
    },
    "G3": {
      "ok": true,
      "at": "2026-10-13T19:27:48+05:00",
      "problems": 0
    }
  },
  "result": null
}
