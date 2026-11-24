# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

## Шаги

### 1. Код лобби после матча: `PassToMatch(…, leaderId, userIds)`, `ILobbyCodes.MarkReturned`, `LobbyOptions.ReturnsAfterMatch` (BREAKING)

Кого касается: бэкенд игр с лобби фреймворка (`Game.Lobbies`: pixel-swarm, neon-dodge; у peace-data-reborn своя копия
`LobbyCodeLeases` - не трогать). Код лобби может вернуться из матча в новое лобби того же сервера с тем же кодом: с
`LobbyOptions.ReturnsAfterMatch = true` один раз на матч - при первом игроке, ушедшем из матча насовсем (разрыв с окном переподключения -
не уход), или при закрытии матча - `ILobbyFactory` игры получает `LobbyCreation { Code = тот же, Return = { LobbySessionId,
MatchSessionId, LeaderId } }`, регистр указывает код на новое лобби (`ILobbyCodes.MarkReturned`), места зарезервированы всем игрокам
матча (`JoinTimeout` лобби), лидер - лидер матча, как только он вошёл (до него - первый вошедший). Отказ фабрики - код освобождается,
матч идёт дальше. Без флага поведение прежнее.
Поиск: `git grep -nE "PassToMatch\(|new LobbyCodeLeases\(|: ILobbyCodes\b|new LobbySessionInfo" -- backend`.
Сделать:
- `ILobbyCodeLeases.PassToMatch(Guid lobbySessionId, ISession match)` -> `PassToMatch(lobbySessionId, match, leaderId, userIds)`:
  реализация `ILobbyMatches.Create` игры (`backend/Game/Global/LobbyMatches.cs`) передаёт лидера и пользователей, которых получила.
- `new LobbyCodeLeases(codes, logger)` -> `new LobbyCodeLeases(codes, afterMatch, logger)`: в тестовых мирах игры
  (`SessionTestWorld`, `GameTestWorld`) `afterMatch` - `new LobbyCodeRelease()` (код освобождается с матчем) или `new LobbyReturns(lobbyFactory,
  lobbyOptions, logger)`. В контейнере сервера правило регистрирует `AddSessionHosting` по флагу.
- Фейки `ILobbyCodes` в тестах игр: новый член `Task MarkReturned(string code, Guid sessionId, Guid returnSessionId)` - запись кода
  переходит сессии `returnSessionId` того же сервера без матча; вызов не держателя кода бросает, как `Release` (образец -
  `backend/Tests/Hosting/FakeLobbyCodes.cs` фреймворка).
- Игра, включившая возврат (`ReturnsAfterMatch = true`): фабрика лобби передаёт `Return = creation.Return` в `LobbySessionInfo` (без
  этого лидером лобби возврата будет первый вошедший) и сама переносит свои свойства комнаты (режим, арену) из своей записи матча
  `Return.MatchSessionId`. Свои фейки `ILobbyFactory` - тоже.

## Проверить поведение

- Сеть: событие объекта, тип которого ещё никто не запросил (`GetEvent<T>`), больше не бросает `Event type ... not found in actions`
  (раньше исключение ловил `NetworkCommandsDispatcher`, событие терялось): `NetworkEvents` держит его в порядке прихода и отдаёт
  первому подписчику источника своего типа (`NetworkEventSource<T>`, первый `Advise` после `GetEvent`), до событий, пришедших позже.
  Предел - `NetworkEvents.MaxPendingEvents = 64` событий без источника на объект, 65-е бросает `InvalidOperationException` с именем
  типа. Правок игр не требует: подписи `INetworkEvents`, `GetEvent`, `InvokeLocal` прежние, источник по-прежнему `ViewableDelegate<T>`.
