## Исправление находок аудита backend — Результат

### Статус: Готово (ветка `backend-audit-fixes`, не слита в main, не запушена)

### Что сделано
Находки `docs/audits/2026-11-14_backend.md` исправлены шагами 1-9 (ветки `baf-*`, слиты в `backend-audit-fixes`); на каждый
подтверждённый баг сначала красный тест. Шаги игр - `releases/unreleased.md` (7 разделов «Бэкенд/Консоль/match-metrics»), `CLAUDE.md`
(Cluster, Console, Sessions, ожидаемые числа тестов).

Evidence: `tools/scripts/test-all.sh` 2026-11-14 22:18 - backend 1694/1694, common 96, verify 168, control 23, telemetry 84,
EditMode 1199/1203 (4 skipped), PlayMode 96/120 (24 skipped), все PASS. Сборка `framework.slnx` 0 ошибок.

### Отличия от плана
- `RequeueStuck` удалён целиком (шаг 9), кнопка консоли -> `RequeueStuckOlderThan`.
- `OperationProgress` используют 3 игры, а не одна.
- Попутно починены 3 теста протокола, падавшие на main (теги match-metrics в тестовом union, golden).

### Не исправлено / пропущено
- Backlog удалённого наблюдателя `RuntimeChannel`: задуманное поведение (тест `Publish_ObserverQueueOverflows_...`).
- Опрос Discovery / ClusterHealthWidget: у `IServiceDiscovery` нет observable.
- `Notify` двух grain ClientTelemetry не объединён (одна строка).
- `[Id]` с пропусками - только комментарии; `ServiceTag` legacy - без изменений.
- 103 `new Lifetime()` в State/Hosting/Network/ClientTelemetry-тестах без общего silo; задержки в `TaskBalancerTests` (AgingMechanism, LifetimeTermination).
- Шлюзы игр (await `Closed`) - только шаг релиза.

### Нерешенные вопросы
- `TaskScheduling`: удалить фичу или оставить (нет производителя).
- Цикл namespace Lobbies <-> Hosting через `ISessionsCollection`.
- Sealed в `RunningMatchEntry` отвечает Full (возможно, Started).
- Перекрытие чанков одного слота между батчами match-metrics не проверяется.
- `DeploymentStateEpochTests` не перепроверен красным после правки самого теста.
- `IsOrleansStarted` на стадии Active проверен юнит-тестом, не в живом кластере.
