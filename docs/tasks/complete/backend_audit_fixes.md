## Исправление находок аудита backend (2026-11-14)

### Что сделано
- Исправлены находки `docs/audits/2026-11-14_backend.md` (9 критичных, 32 важных, 25 мелких) во всех папках `backend/`; на каждый
  подтверждённый баг сначала красный тест. Ключевые: повтор `DurableQueue` ждёт упавшего наблюдателя; проверка эпохи деплоя в
  `DeploymentState`, `OpenLobbiesWatcher`, `SetSlice`; `Duplicate` match-metrics подтягивает слоты; контракт владельца `Connection`
  (`Closed`, чтение только с обработчиком); `Reserve` с причиной отказа; аудит-лог консоли не прячет ошибки базы; `RequeueStuck` удалён.
- Метрики и трейсы объявляют фичи, `BackendMetrics` удалён; консоль регистрируется `AddConsoleInfrastructure`, таблица аудита -
  `AuditLogTable.Create`; имена и лимиты match-metrics в `shared`.
- Тесты: lifetime теста в `IntegrationTestBase`, сигналы вместо sleep, drain бросает на упавшем эффекте; починены 3 теста протокола,
  падавшие на main.
- `test-all.sh`: backend 1694, common 96, Verify 168, control 23, telemetry 84, EditMode 1203 (4 skipped), PlayMode 120 (24 skipped) - PASS.

### Ключевые файлы
- `releases/unreleased.md` - шаги игр по всем BREAKING-изменениям (шлюзы ждут `Closed`, `OperationProgress` в игры, `Reserve`,
  состояния кластера, метры, `RequeueStuck`, консоль, match-metrics)
- `CLAUDE.md` - Cluster (`IDeployContext.Leave`), Console, Sessions (контракт `Connection`), ожидаемые числа тестов
- `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`, `backend/Cluster/Deploy/DeploymentState.cs`, `backend/Common/Network/Connection.cs`

### Заметки
- Не исправлено намеренно: backlog удалённого наблюдателя `RuntimeChannel` (задумано), опрос Discovery (нет observable),
  `[Id]` с пропусками (сохранённые значения), legacy `ServiceTag`.
- Открытые вопросы: удалять ли `TaskScheduling` (нет производителя); цикл namespace Lobbies <-> Hosting через `ISessionsCollection`;
  Sealed в `RunningMatchEntry` отвечает Full; перекрытие чанков match-metrics между батчами; `IsOrleansStarted` не проверен в живом кластере.
