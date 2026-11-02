---
task: test_speed
orchestrator_session: c83b6d6a-df3b-4319-a9aa-1c50de253acb
orchestrator_generation: 1
updated: 2026-11-02
status: done
---

## Граф шагов

| Шаг | Зависит от | Волна | Агент (marker / agentId) | Поколение | Статус | Handoff |
|-----|-----------|-------|---------------------------|-----------|--------|---------|
| R1 | — | 1 | ORCH-test_speed-R1-g1 / af7cc2397990f31f2 | 1 | done (e021a7e) | — |
| U1 | — | 1 | ORCH-test_speed-U1-g1 / ab2154f8b9bb0df90 | 1 | done (9eaa477) | — |
| B1 | — | 1 | ORCH-test_speed-B1-g1 / aceda3497303e456e | 1 | done (a5caaa9..87a6b1d) | — |
| U2 | U1 | 2 | ORCH-test_speed-U2-g1 / a1a48b7d80968b0a1 | 1 | done (3b1bffc, 3163da7, 6ed2582) | — |
| B2 | B1 | 2 | ORCH-test_speed-B2-g1 / a16f788522fe9b8ef | 1 | done (1c29634, df47196, 85de06f) | — |
| U3 | U2 | 3 | ORCH-test_speed-U3-g1 / ab0bb6af72587f2b4 | 1 | done (58d4ba6..ef539ed) | — |
| F1 | R1, U3, B2 | 4 | ORCH-test_speed-F1-g1 / add733e632a3b36fe | 1 | done (1cb330b) | — |

## Ресурсы

- `.claude/worktrees/test_speed_runner`, ветка `test_speed_runner`, своя `Library`: R1.
- `.claude/worktrees/test_speed_unity`, ветка `test_speed_unity`, своя `Library`: U1, U2, U3.
- `.claude/worktrees/test_speed_backend`, ветка `test_speed_backend`: B1, B2.
- `.claude/worktrees/test_speed`, ветка `test_speed`, своя `Library`: F1.
- Основное дерево: только файлы этой задачи, не закоммичены.

## Журнал

### [2026-11-02] Волна 1

Запущены R1, U1, B1. Все от `d8b482d`.

### [2026-11-02] R1 готов

`e021a7e` в `test_speed_runner`. Стена 121,7 / 123,0 / 122,0 с при нагрузке 28-84. Verify считает 148, а не 149: прежний подсчёт включал строку `ALL PASSED`. Открыто для F1: возможная полная пересборка dotnet после нового коммита (66,9 с, гипотеза `SourceRevisionId`).

### [2026-11-02] U1 готов

`9eaa477` в `test_speed_unity`. Три источника падений, 6 полных EditMode без падений (611 + 4 из 615). Меняет продакшен: кандидаты offer уходят после ответа; непринятый сигнал считается потерей соединения; ошибки libdatachannel уровня Error пишутся как Warning. Для пользователя: редактор здесь грузит системную `/usr/lib/libdatachannel.so.0.24.5`, а не библиотеку из `Plugins`.

### [2026-11-02] B1 готов

5 коммитов `a5caaa9`..`87a6b1d` в `test_speed_backend`. 839 тестов, 5 прогонов без падений: 30-35 с на фоне, 41-49 с под `stress-ng`. `9858937` с `BREAKING:`. Для пользователя: `ResubscribeLocked` не вызывает `CatchUp` при `lastSeenSequence == 0`, первая упавшая доставка теряется без `GapDetected`; гонка `ForceDisconnect` до `Run`.

### [2026-11-02] B2 готов

`1c29634`, `df47196`, `85de06f`. Backend 21-24 с, 13 прогонов без падений, из них 3 под `stress-ng` (31-38 с). Разбиение `OrleansIntegrationCollection` не делалось: стену набора задаёт EditMode. Найденное передано `framework-57` (`file_splits_findings`).

### [2026-11-02] U2 готов

`3b1bffc`, `3163da7` (`BREAKING:`), `6ed2582`. 3 EditMode и 3 PlayMode без падений. Для пользователя: `MatchmakingTests` не видит таймер, который не является задачей UniTask; `LatencyClock` не проверен в живой сессии.

### [2026-11-02] U3 готов

5 коммитов `58d4ba6`..`ef539ed`. EditMode 31-41 с (выброс 56 с), 5 из 5; PlayMode 21 с, 3 из 3. `-nographics` не вносится: выигрыша нет. `m_EnterPlayModeOptions: 3` закоммичен. Форк stripping-tool: новый патч в `FORK.md`.

### [2026-11-02] F1 готов, задача закрыта

Ветка `test_speed`, `1cb330b`, 21 коммит поверх `ab02dec`. Стена 36,6 с в 13 запусках подряд без падений, оркестратор повторил запуск сам: 36,8 с. Смена API 41,6 с, без `.build` 51,4 с. Итого: 7 шагов, 7 агентов, 0 handoff, 0 блокировок. В `main` не влито. Worktree дорожек удалены, ветки оставлены.

### [2026-11-02] Влито

По просьбе пользователя `main` перемотан на `1cb330b` и запушен.
