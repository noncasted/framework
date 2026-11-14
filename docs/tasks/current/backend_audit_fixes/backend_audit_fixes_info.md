---
task: backend_audit_fixes
status: done
phase: implementation
created: 2026-11-14
updated: 2026-11-14
total_steps: 10
completed_steps: [1,2,3,4,5,6,7,8,9,10]
blocked_steps: []
---

## Задача: исправить находки аудита backend

### Что я хочу

Пользователь после `/code-audit backend` написал `/orchestrate чини`: исправить находки отчёта
`docs/audits/2026-11-14_backend.md` (9 критичных, 32 важных, 25 мелких) во всех папках `backend/`
(Infrastructure, Cluster, Common, Console, Hosting, Session, ClientTelemetry, ClientTelemetry.TestHost, MatchMetrics, Tests).

Порядок работы по багам (память `fix-workflow-tests-first`): сначала тест, который ожидаемо падает, затем фикс, затем
прогон. Находка, чей тест нельзя сделать красным, не чинится и попадает в отчёт как неподтверждённая. API framework можно
ломать (`framework-api-breaking-ok`): `BREAKING:` в коммите, шаг игры в `releases/unreleased.md`.

Работа идёт в worktree `.claude/worktrees/backend_audit_fixes` (ветка `backend-audit-fixes`), в общем дереве работают другие
сессии (`framework-parallel-sessions`). Каждый агент первой волны работает в своём worktree `baf_<area>` (ветка `baf-<area>`),
оркестратор сливает ветки в `backend-audit-fixes`. Ничего не пушится.

### Цель
- Все критичные и важные находки исправлены с тестом или с объяснением, почему нет.
- Мелкие находки исправлены там, где это не требует решения пользователя.
- Полный `tools/scripts/test-all.sh` зелёный, числа тестов не меньше ожидаемых.
- `releases/unreleased.md` содержит шаги игр для BREAKING-изменений.

### Контекст
Решения, принятые оркестратором без пользователя (пользователь может их пересмотреть):
- `TaskScheduling` не удаляется (это решение пользователя, вопрос задан в отчёте аудита); только убираются глотающие catch.
- Номера `[Id]` Orleans с пропусками не перенумеровываются: значения хранятся Orleans-сериализатором в состояниях деплоя;
  добавляется комментарий.
- `ServiceTag` legacy-ветка Integer остаётся: неизвестно, остались ли старые деплои.
- Изменения в играх (await `connection.Run()` в шлюзах) - только шагом в `releases/unreleased.md`, игры правятся отдельно.

## План реализации

#### [1] Infrastructure
- **Статус:** [x] completed
- **Цель:** находки Infrastructure из отчёта исправлены.
- **Файлы:** `backend/Infrastructure/**`, новые тесты в `backend/Tests/**` (новые файлы), `.claude/docs/MESSAGING.md`, `COMMON_ORLEANS.md`
- **Зависит от:** —

#### [2] Cluster
- **Статус:** [x] completed
- **Файлы:** `backend/Cluster/**`, новые тесты
- **Зависит от:** —

#### [3] Common (+ `backend/Session/Users/HostedUser.cs`)
- **Статус:** [x] completed
- **Файлы:** `backend/Common/**`, `backend/Session/Users/HostedUser.cs`, тесты `backend/Tests/Network`
- **Зависит от:** —

#### [4] Hosting + Session
- **Статус:** [x] completed
- **Файлы:** `backend/Hosting/**`, `backend/Session/**` кроме `Users/HostedUser.cs`
- **Зависит от:** —

#### [5] ClientTelemetry + TestHost
- **Статус:** [x] completed
- **Файлы:** `backend/ClientTelemetry/**`, `backend/ClientTelemetry.TestHost/**` кроме `ConsolePages/TelemetryHostConsole.cs`
- **Зависит от:** —

#### [6] MatchMetrics
- **Статус:** [x] completed
- **Файлы:** `backend/MatchMetrics/**`, `shared/MatchMetrics/**`, `client/modules/match-metrics/**`
- **Зависит от:** —

#### [7] Console
- **Статус:** [x] completed
- **Файлы:** `backend/Console/**`, `backend/ClientTelemetry.TestHost/ConsolePages/TelemetryHostConsole.cs`
- **Зависит от:** —

#### [8] Tests (находки самих тестов)
- **Статус:** [x] completed
- **Файлы:** `backend/Tests/**`
- **Зависит от:** 1-7 (слияние)

#### [9] Метрики и трейсы по фичам (P14 BackendMetrics / TraceExtensions)
- **Статус:** [x] completed
- **Файлы:** `backend/Common/Extensions/Metrics`, `Traces`, места объявления в фичах
- **Зависит от:** 1-7 (слияние)

#### [10] Релизные шаги, CLAUDE.md, полный прогон
- **Статус:** [x] completed
- **Проверка:** `tools/scripts/test-all.sh` в worktree (Library скопирован reflink)
- **Зависит от:** 8, 9

### Документация к прочтению
- `.claude/docs/ARCHITECTURE_PRINCIPLES.md`, `CODE_STYLE_FULL.md`, `COMMON_ORLEANS.md`, `MESSAGING.md`, `COMMON_LIFETIMES.md`, `BLAZOR.md`, `MATCH_METRICS.md`

### Риски
- Параллельные `dotnet test` в разных worktree поднимают свои контейнеры Postgres и Orleans TestCluster: возможна нагрузка.
- `releases/unreleased.md`, `CLAUDE.md` правит только оркестратор (шаг 10): агенты присылают тексты в отчёте.
