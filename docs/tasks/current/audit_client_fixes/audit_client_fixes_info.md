---
task: audit_client_fixes
status: done
phase: implementation
created: 2026-11-06
updated: 2026-11-06
total_steps: 4
completed_steps: [1, 2, 3, 4]
blocked_steps: []
---

## Задача: исправить подтверждённые находки аудита клиента

### Что я хочу

Аудит `docs/audits/2026-11-06_client.md` проверен вручную. Из «критичных» K2 и T1 оказались ложными, K1 подтверждён
экспериментом (но это не критично: падение сразу при сборке с понятным сообщением). Нужно исправить подтверждённое:
K1, E5, U1, P3, а P2 решить удалением неиспользуемого `InjectGameObject` (если вызывающих нет), и пометить K2/T1 в отчёте
как ложные с объяснением. Порядок: сначала падающий тест, затем исправление, затем повторный прогон. Правки — через
оркестрацию агентами. Без коммитов.

### Цель
1. K1: `RegistrationOccurrences.InstanceOccurrence` не считает экземплярные регистрации из исключающей ветви того же
   оператора (`BranchGroups.Exclude`), сценарий `if/else RegisterInstance<IClock>` строится в обеих ветвях. Два отдельных
   `if` без `else` остаются заявленным ограничением (сообщение рантайма).
2. E5: выбранная ветвь `kind switch { ... => builder.Register<X>() }`, реализующая `IDisposable`, освобождается в `Dispose`
   скоупа (`PlanDisposal.cs:17` исключает `slot.IsSwitch`).
3. U1: синхронный `IUIStateEnterHandler.OnEntered`, бросивший исключение, выходит из handle (как async-ветка `RunEntered`).
4. P3: шаг для `AddTelegramAPI(platformOptions, TelegramEditorOptions)` в release notes; `CLAUDE.md:273` с новой сигнатурой.
5. P2: `IContainer.InjectGameObject` / `IViewInjector.Inject(GameObject)` бросает на первом `MonoBehaviour` без
   сгенерированного inject; удалить, если ни одна игра не вызывает перегрузку с `GameObject`, иначе — не бросающий обход.
6. Отчёт аудита: K2 и T1 помечены ложными с доказательствами, K1 понижен до важного; итог пересчитан.

### Контекст
- Доказательства K2/T1: `common/Reactive/Events/LifetimedValueExtensions.cs:49-57` (listener получает lifetime значения,
  `LifetimedValue.Set` его завершает); IL2CPP `il2cpp_codegen_marshal_free(returnValue)` после
  `il2cpp_codegen_marshal_string_result` (`peace-data-reborn/client/Library/Bee/artifacts/WebGL/il2cppOutput/cpp/Internal.cpp:10067`).
- Сценарии K1/E5, которые сейчас падают, уже написаны во временной копии:
  `/tmp/claude-1000/-projects-framework/58b92c58-447b-444e-a145-38154e7eb8a5/scratchpad/k1/client/core/Source~/ContainerGenerator.Verify/RegressionCases.cs`
  (`K1Scenario`, `K1bScenario` — ожидаемо падает, не переносить как тест успеха, `E5Scenario`).
- Правило 8 CLAUDE.md: ломающее изменение API — `BREAKING:` и шаг в `releases/unreleased.md`.

## План реализации

| Шаг | Что | Файлы | Проверка |
|-----|-----|-------|----------|
| 1 | Генератор: K1, E5, P2 (тесты первыми) | `client/core/Source~/ContainerGenerator/**`, `ContainerGenerator.Verify/**`, `client/core/Runtime/Tools/Container/Abstract/IContainer.cs`, `Runtime/Scopes/Common/Services/GeneratedViewInjector.cs`, `client/core/Tests/Editor/Container/GeneratedScopeProbe.cs`, `Analyzers/ContainerGenerator.dll`, `releases/unreleased.md` (P2) | Verify: новые сценарии падали до и проходят после, всего ≥166+новые |
| 2 | Документы: P3 + отчёт аудита | `releases/0.1.0.md`/`unreleased.md`, `CLAUDE.md`, `docs/audits/2026-11-06_client.md` | grep |
| 3 | U1 + EditMode тест | `client/modules/ui/Runtime/StateMachines/UIStatesExtensions.cs`, тест в `client/modules/ui/Tests` | EditMode тест падал до, проходит после |
| 4 | Полный прогон | `tools/scripts/test-all.sh` | все стадии зелёные, числа не меньше ожидаемых |
