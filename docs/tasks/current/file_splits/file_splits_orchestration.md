---
task: file_splits
orchestrator_session: 0b8312a5-015f-428b-99c3-51bf78155ba7
orchestrator_generation: 1
updated: 2026-11-02
---

## Граф шагов

| Шаг | Зависит от | Волна | Агент (marker / agentId) | Поколение | Статус | Handoff |
|-----|-----------|-------|---------------------------|-----------|--------|---------|
| 1 | — | 1 | ORCH-file_splits-1-g1 / aeb932ca937d361c6 | 1 | done | — |
| 2 | 1 | 2 | ORCH-file_splits-2-g1 / ad6b65bd3014a9df5 | 1 | done, влит | — |
| 3 | 1 | 2 | ORCH-file_splits-3-g1 / affa488a07174e88d | 1 | done | — |
| 4 | 3 | 3 | ORCH-file_splits-4-g1 / a44bdae0b26261e22 | 1 | done | — |
| 5 | 4 | 4 | ORCH-file_splits-5-g1 | 1 | in_progress | — |
| 6 | 5 | 5 | — | 1 | pending | — |
| 7 | 6 | 6 | — | 1 | pending | — |
| 8 | 2, 7 | 7 | — | 1 | pending | — |
| 9 | 8 | 8 | — | 1 | pending | — |

## Ресурсы
- Дерево: только `.claude/worktrees/file_splits` (ветка `file_splits`). В `/projects/framework` работает другая сессия (`public_setters`), там ничего не запускать.
- Unity (`client/project` этого worktree, batchmode): один владелец, цепочка шагов 1, 3, 4, 5, 6, 7, 8, 9.
- Перед каждым шагом `git rebase main`: правки сеттеров из `public_setters` подхватываются сразу.
- Генератор: шаг 2 в worktree `.claude/worktrees/file_splits_gen` (ветка `file_splits_gen`), в `file_splits` вливается после завершения.

## Журнал
### [2026-11-02 03:52] Задача перенесена в отдельный worktree
При старте обнаружена параллельная оркестрация `public_setters` в том же дереве. HEAD основного дерева возвращён на `main`, сессия `framework-33` предупреждена. Шаг 1 перенаправлен в worktree.
### [2026-11-02] Шаг 1 завершён
Эталон в `baseline.md`. Агент до перенаправления успел запустить сборку и Unity в основном дереве; dll вернулся к закоммиченному (md5 совпал), лишний `SceneTemplateSettings.json` удалён. Этот файл Unity создаёт сам при каждом запуске, коммитить его нельзя.
### [2026-11-02] Запущены шаги 2 и 3
Шаг 2 в `file_splits_gen`, шаг 3 в `file_splits`. Файлы шагов не пересекаются.
