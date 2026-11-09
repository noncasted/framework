# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.


### Консоль: статистика онлайна, Home без инфраструктурных карточек

Кому: бэкенд и консоль всех игр. BREAKING: `HomeInfrastructureSection` удалён (его карточки теперь только на `/infrastructure`).

Поиск: `git grep -nE 'HomeInfrastructureSection|ConnectedUsersWidget|SetupCoordinator|ConnectedUsersLiveData' -- 'backend/*'`

- Учёт онлайна (`backend/Cluster/Monitoring/Online`): в `SetupCoordinator` (`Orchestration/Extensions/ProjectsSetupExtensions.cs`) вызвать
  `builder.AddOnlineTracker()`, только на координаторе. Источник: `ConnectedUsersLiveData`, который игра уже пишет; пользователь считается
  по id (две вкладки или два гейтвея = 1). Читать на любом хосте: `IDeploymentState<OnlineStatsLiveData>` (регистрирует `AddMonitoring`).
- Тесты игры, перечисляющие состояния фреймворка, получают два новых: `Cluster.Monitoring.Online.OnlineDayState` (`state_online_days`,
  `online_day`, ключ `Integer`) и `Cluster.Monitoring.Online.OnlineStatsLiveData` (`cluster`, `deployment_state_OnlineStatsLiveData`,
  ключ `Guid`, deploy-scoped). В pixel-swarm: `Tools/Tests/Codegen/StatesLookupGeneratorTests.cs` (`_productionStates`),
  `Tools/Tests/Cluster/DeployCleanupTests.cs` (список deploy-scoped типов и их identity, как у `ConnectedUsersLiveData`).
- Home консоли (`Console/Home/HomeSections.razor`, `Home.razor`): `<HomeInfrastructureSection />` -> `<HomeClusterSection />` (Features и
  Client Telemetry, последняя только с `AddClientTelemetryReader`); свой `ConnectedUsersWidget` удалить, на его место `<OnlineWidget />`
  (текущий онлайн, пик и уникальные по часам / дням, клик по заголовку открывает `/online`). Остальные карточки, карточки игры
  (`AddConsoleInfrastructureCard`) и новая `Online` - на `/infrastructure`. `ConsoleInfrastructureCard` переехал в `Console/Infrastructure`,
  пространство имён то же (`Console`).
- Страница `/online` (`ConsoleConstants.Pages.Online`) берётся из сборки `Console`, ничего регистрировать не надо. Классы Tailwind - только
  те, что уже есть в `app.css` всех игр, пересобирать `app.css` не нужно.
