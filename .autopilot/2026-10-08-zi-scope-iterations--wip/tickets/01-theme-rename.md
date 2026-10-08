# 01 — ZI-scope: переименование, спокойная тема с синим акцентом, вкладки панели

**Требования:** R18, R01, R02, R03, R04, R03.1 (ист. 6), каркас для R25i/R05
**Blocked by:** —
**Зона:** `App.xaml(.cs)` · `MainWindow.xaml(.cs)` · `UI/Effects/` · `UI/Controls/` (стили существующих контролов) · `Views/*.xaml` (только цвета и стили, не логику) · заглушки `Views/MagnificationBar*`, `Views/SeriesPanel*`, `Views/SeriesMap*`, `ViewModels/SeriesPanelViewModel.cs` · в `MainViewModel` только заглушки `PrimaryCaptureCommand` и `SeriesPanel` · `LumeneraViewer.csproj` (AssemblyName/Product/Title) · `publish.ps1` · `README.md` · `installer/README-УСТАНОВКА.txt`
**Волна:** 1
**Status:** ready

## Что должно заработать

Программа называется ZI-scope и выглядит как современный рабочий инструмент в духе Figma,
Linear и VS Code: графитово-серая, не чёрная, плоская, с синим акцентом только на активном.
Неоновые эффекты reactbits уходят. Правая панель превращается во вкладки-иконки. В шапке
и внизу появляются места под переключатель увеличений и карту серии: их наполнят следующие таски.

## Из брифа, дословно

> «Сделай интерфейс не такой вычерный, акценты сделай синим. За пример возьми современные программы. В целом симпатично сейчас, но цвета и тд создают ощущение китайского дизайна.»
> «Программу переименуй в ZI-scope.»
> (Дополнения) на вопрос о стиле: «Как Figma / Linear / VS Code»

## Разделы спецификации

Истории 1, 3, 4, 5, 6; Решения §1, §2; `interfaces.md` → «Слоты UI», «Хост — новое» (только заглушки `PrimaryCaptureCommand`, `SeriesPanel`).

## Критерии приёмки

- [ ] Палитра, шрифт и радиусы — строго по историям 3–4 (токены в `UI/Effects/Palette` и ресурсах `Effects.xaml`; WPF-UI тема Dark с акцентом `#3B82F6` через `ApplicationAccentColorManager`). Ни одного фиолетового, розового или бирюзового элемента; нет градиентов на кнопках
- [ ] Убраны Aurora-фон, ShinyText, ClickSpark, glow, подсветка Spotlight (история 5). Неиспользуемые файлы эффектов удалены; `CountUpText` остаётся. Переходы сдержанные (hover ≤120 мс, появление ≤150 мс)
- [ ] Все существующие карточки и диалоги (Камера, Цвет, Масштаб, Измерения, Сохранение, лента, «Дополнительно», диалоги драйверов, статус-панель) перекрашены в новую палитру, без визуальных регрессий и обрезаний
- [ ] Правая панель — вкладки-иконки «Камера / Цвет / Масштаб / Серия / Сохранение» по `interfaces.md` → «Слоты UI»; вкладка видна целиком с собственной прокруткой; Tab сворачивает панель; выбранная вкладка запоминается (`Settings.Window` — добавь поле в конец `WindowSettings`, если нужно, и напиши в INTERFACES)
- [ ] Заглушки слотов: `Views/MagnificationBar` в шапке, `Views/SeriesPanel` во вкладке «Серия» (текст «Серии появятся здесь»), `Views/SeriesMap` внизу (переключатель «Лента / Карта серии»), `ViewModels/SeriesPanelViewModel(MainViewModel host)` с `bool IsActive` (false) и `IAsyncRelayCommand CaptureStepCommand` (пустой); в `MainViewModel` — `SeriesPanel` и `PrimaryCaptureCommand` (пока вызывает `CapturePanel.CaptureCommand`); Пробел и кнопка «Снимок» идут через `PrimaryCaptureCommand`
- [ ] Переименование по истории 1 и Решениям §2: заголовок окна и шапка «ZI-scope», `AssemblyName` = `ZI-scope` (exe `ZI-scope.exe`), Product/Title, `publish.ps1` → `dist\ZI-scope\`, README и инструкция установки говорят «ZI-scope». Пространства имён и имена проектов не трогать. Папку настроек **не** трогать — это таск 03
- [ ] `dotnet build`/`dotnet test` зелёные. Скриншоты в демо-режиме: главное окно на вкладке «Цвет» и на вкладке «Масштаб» — `.autopilot/2026-10-08-zi-scope-iterations--wip/screens/01-theme-*.png`; исполнитель сам смотрит их и доводит до аккуратного вида
