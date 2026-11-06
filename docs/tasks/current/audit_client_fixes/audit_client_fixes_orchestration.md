---
task: audit_client_fixes
orchestrator_session: 58b92c58-447b-444e-a145-38154e7eb8a5
orchestrator_generation: 1
updated: 2026-11-06
---

## Граф шагов

| Шаг | Зависит от | Волна | Агент (marker / agentId) | Поколение | Статус | Handoff |
|-----|-----------|-------|---------------------------|-----------|--------|---------|
| 1 генератор K1/E5/P2 | — | 1 | ORCH-audit_client_fixes-1-g1 / a533bd77e4dbafe7d | 1 | done | — |
| 2 документы P3 + отчёт | — | 1 | ORCH-audit_client_fixes-2-g1 / a159be768db54380b | 1 | done | — |
| 3 U1 + EditMode | 1 (dll генератора, Unity) | 2 | ORCH-audit_client_fixes-3-g1 / a82f4fe4f1db5cf2c | 1 | done | — |
| 4 test-all | 1,2,3 | 3 | оркестратор (фон) | — | done | — |

## Ресурсы
- Unity `client/project`: шаг 3, затем шаг 4. Шаг 1 Unity не трогает.
- `releases/unreleased.md`: только шаг 1. `releases/0.1.0.md`, `CLAUDE.md`, отчёт аудита: только шаг 2.

## Журнал
### [2026-11-06] Волна 1 запущена (шаги 1, 2 параллельно: файлы не пересекаются).
### [2026-11-06] Шаг 2 done: releases/0.1.0.md (A7), CLAUDE.md:273, отчёт аудита (K2/T1 ложные, итог 0 критичных). P2: вызывающих Inject(GameObject) в играх нет; storytale обходит баг своим ViewInjectorExtensions. peace-data-reborn ещё на старой сигнатуре AddTelegramAPI (указатель старше d211f8a4).
### [2026-11-06] Шаг 1: K1, E5 готовы (Verify 168/168, тесты падали до). HANDOFF был ложный: --marker нашёл транскрипт чужой сессии 65ff230e (реально 80k по --agent). Тот же агент продолжен SendMessage (P2 удаление, Release dll, slnx). unreleased.md — у шага 1.
### [2026-11-06] Шаг 1 done: P2 удалён (BREAKING, unreleased.md), Release dll без InjectGameObject, Verify 168/168, slnx 0 ошибок. client/core/Runtime проверит Unity в шаге 3. Шаг 3 запущен.
### [2026-11-06] Шаг 3 done: TryEnter sync try/catch Exit; UIStateMachineTests 11/12 до, 12/12 после; Unity без CS ошибок. Шаг 4: test-all.sh запущен.
### [2026-11-06] Шаг 4: test-all — всё PASS, кроме EditMode 1 FAIL: ClientTelemetryContainerTests.Module_NothingElseReferencesIt (Internal.FrameRate.Telemetry, Internal.Resolution.Telemetry ссылаются на телеметрию). Не связано с задачей: asmdefs из f5affc6a, задача их не трогала.
## Итог
Шагов 4, агентов 3, handoff 1 (ложный, context_meter --marker нашёл чужой транскрипт), заблокированных 0.
