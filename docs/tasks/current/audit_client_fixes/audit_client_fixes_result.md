## Результат
Исправлены K1, E5, U1, P2 (удалён, BREAKING), P3 (документы); в отчёте аудита K2 и T1 помечены ложными.
test-all: backend 1551, common 96, Verify 168, control 23, telemetry 84, EditMode 1151/1156 (4 skipped, 1 FAIL не по задаче:
ClientTelemetryContainerTests.Module_NothingElseReferencesIt — asmdefs Internal.FrameRate/Resolution.Telemetry из f5affc6a), PlayMode 96/120 (24 skipped).
Не сделано: try/catch в StatementWalker не размечается (обе ветви могут выполниться) — оставлено.

## Изменённые файлы
| Файл | Шаг | Что |
|------|-----|-----|
| releases/0.1.0.md | 2 | шаг A7 AddTelegramAPI + TelegramEditorOptions |
| CLAUDE.md | 2 | сигнатура AddTelegramAPI |
| docs/audits/2026-11-06_client.md | 2 | раздел «Проверка отчёта», K2/T1 ложные, K1 в важные, итог пересчитан |
| client/core/Source~/ContainerGenerator/Scope/RegistrationOccurrences.cs | 1 | K1: BranchGroups.Exclude в InstanceOccurrence |
| client/core/Source~/ContainerGenerator/Scope/Plan/PlanDisposal.cs, Emit/ContainerApiWriter.cs | 1 | E5: dispose выбранной ветви switch; P2: удалён InjectGameObject |
| client/core/Source~/ContainerGenerator/Scope/Plan/ScopePlan.cs, ScopePlanBuilder.cs | 1 | P2: HasUnityGameObject удалён |
| client/core/Source~/ContainerGenerator.Verify/RegressionCases.cs, Program.cs | 1 | 2 регрессии (K1, E5), заглушка IContainer |
| client/core/Runtime/Tools/Container/Abstract/IContainer.cs, Scopes/Common/Services/GeneratedViewInjector.cs, Tests/Editor/Container/GeneratedScopeProbe.cs | 1 | P2 |
| client/core/Analyzers/ContainerGenerator.dll | 1 | Release-сборка |
| releases/unreleased.md, .claude/docs/COMMON_CONTAINER.md | 1 | шаг BREAKING P2, API контейнера |
| client/modules/ui/Runtime/StateMachines/UIStatesExtensions.cs | 3 | U1: sync OnEntered бросил → handle.Exit(), throw |
| client/modules/ui/Tests/Editor/UIStateMachineTests.cs | 3 | тест TryEnter_SyncEnterThrows_RethrownAndStateExits |
