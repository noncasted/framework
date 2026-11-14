---
task: backend_audit_fixes
updated: 2026-11-14
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| 1-7 | [/] | — | — |
| 8-10 | [ ] | — | — |

## Заметки
- Шаг 4 (Hosting+Session) DONE, слит. Открыто: цикл namespace через ISessionsCollection; Sealed->Full в RunningMatchEntry.
- Шаг 5 (ClientTelemetry) DONE, слит (431/431).
- Шаг 6 (MatchMetrics) DONE, слит (1656/0 fail; golden протокола починен). Открыто: перекрытие чанков между батчами.
- Шаг 7 (Console) DONE, слит.
- Шаг 3 (Common) DONE, слит; интеграционная сборка 0 ошибок.
- Шаг 2 (Cluster) DONE, слит; сборка 0 ошибок. Оговорка: DeploymentStateEpochTests не перепроверен красным после правки теста.
- Шаг 1 (Infrastructure) DONE, слит; сборка 0 ошибок. Skipped: backlog RuntimeChannel (задуманное поведение). Осталось: кнопка консоли RequeueStuck -> RequeueStuckOlderThan, затем удалить RequeueStuck.
- [2026-11-14] Интеграция волны 1: полный Framework.Tests 1690/1690 зелёные (было 1565 по CLAUDE.md).
- Шаг 9 DONE, слит (1695/0).
