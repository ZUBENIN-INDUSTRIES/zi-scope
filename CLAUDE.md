<!-- autopilot:start -->
# Lumenera INFINITY 4 Viewer

Нативное Windows-приложение (WPF, .NET 8) для работы с микроскопной камерой Lumenera INFINITY 4.

## Команды

| Команда | Что делает |
|---------|------------|
| `dotnet build` | Собрать |
| `dotnet run --project src/LumeneraViewer` | Запустить локально |

## Как здесь работает Autopilot

Сборка ведётся навыком `/autopilot`. Требования, спецификация и таски — в `.autopilot/`.
Прогресс — `.autopilot/dashboard.html`. Правило: требование из `manifest.md`
может снять только пользователь.

Если работа продолжается — скажи «продолжи автопилот»: состояние поднимется
из `.autopilot/state.js`, переспрашивать ничего не нужно.
<!-- autopilot:end -->
