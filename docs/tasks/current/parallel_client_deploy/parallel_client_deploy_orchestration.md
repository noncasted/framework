---
task: parallel_client_deploy
orchestrator_session: 1ea8da09-bb8d-479a-b17c-0ee1aa4b52e8
orchestrator_generation: 1
updated: 2026-11-04 06:45
---

## Граф шагов

| Шаг | Зависит от | Волна | Агент (marker / agentId) | Поколение | Статус | Handoff |
|-----|-----------|-------|---------------------------|-----------|--------|---------|
| R1 run-tests.sh: снимок, слоты, память | — | 0 | ORCH-parallel_client_deploy-R1-g1 / a1f81437fea884552 | 1 | done | handoff/R1_research.md |
| R2 batchmode-сборка: stripping, Addressables | — | 0 | ORCH-parallel_client_deploy-R2-g1 / afee41ea4693b768a | 1 | done | handoff/R2_research.md |
| R3 игры и цели, копии client_telemetry | — | 0 | ORCH-parallel_client_deploy-R3-g1 / ab668f70ff8aa1aba | 1 | done | решения в _info.md |
| 1 WebGlBuild: явный stripping | — | 1 | ORCH-parallel_client_deploy-1-g1 / ab198cd15fa6c0df7 | 1 | done | — |
| 2 deploy-client.sh + deploy-client.json | 1 | 2 | ORCH-parallel_client_deploy-2-g1 / a169587a462e2222d | 1 | done | — |
| 3 скилл и документация | 2 | 3 | ORCH-parallel_client_deploy-3-g1 / a12bf9a2a7c6269ba | 1 | done | — |
| 4 боевой деплой pixel-swarm | 3 | 4 | — | — | skipped (пользователь: деплой делается в другой сессии) | — |

Всё последовательно: шаг 2 проверяет сборки с изменением шага 1; шаги 2 и 4 нагружают Unity batchmode и память.

## Ресурсы
- Открытый редактор пользователя pixel-swarm - не трогать никому.
- Worktree client_telemetry (/projects/pixel-swarm/.claude/worktrees/client_telemetry) - не трогать.

## Журнал
### [2026-11-04 06:30] Старт
Воркспейс был только с требованиями. Волна 0 - три исследования (Explore) параллельно.

### [2026-11-04 06:45] Волна 0 готова, план записан
R1-R3 DONE. Решения и план - в _info.md. Запущен шаг 1.

### [2026-11-04 07:00] Шаг 1 DONE
WebGlStripping.Require() (новый файл) из WebGlBuild.BuildChecked; LoadSettings форка сделан public (патч в FORK.md);
web зависит от stripping-tool; releases/unreleased.md - BREAKING (storytale без настроек стрипа). Компиляция client/project OK
(проверил git diff). В реальной сборке не проверено - проверит шаг 2.

### [2026-11-04 07:30] Шаг 2 DONE
deploy-client.sh (245) + deploy-client/{common,snapshot,build,verify,deploy}.sh; pixel-swarm .claude/deploy-client.json.
a) --no-deploy обе цели: 585 с всего (было ~16 мин), тесты PASS 552 с, сборки 546/551 с одновременно, stripping (WebGlStripping) сработал,
минимум MemAvailable 11 ГБ. b) падающий тест: код 1, заглушка выкладки не вызвана, сборки убиты, копии удалены. c) bash -n OK.
Решения агента: исключён Library/Bee/Player* (DAG с абсолютными путями игры - Bee стёр бы builds/ исходника) -> сборка полная;
.git в копиях не нужен; упавшая сборка не останавливает тесты. Открыто: пик RSS сборки (~10 ГБ к середине) снять в шаге 4.

### [2026-11-04 07:40] Шаг 3 DONE
SKILL.md переписан (114 строк, без MCP), project-skills.md pixel-swarm ссылается на json, releases/unreleased.md - раздел deploy-client,
CLAUDE.md Skills - deploy-client.json. Проверено grep/wc.
Шаг 4 (боевой деплой) - вопрос пользователю: рабочее дерево pixel-swarm содержит чужие незакоммиченные правки (удалены ассеты Insect,
правка ContainerGraphAssetScanner.cs в сабмодуле) - они уйдут в прод.

### [2026-11-04] Закрытие
Пользователь: деплой не делать, подтянуть изменения, закоммитить и запушить фреймворк. git pull --rebase (1 коммит SafeArea, без
пересечений), коммиты 954068cf (форк stripping-tool), c061e543 (Web, BREAKING), 6f49c1df (deploy-client, BREAKING), push в origin/main.
Итого: шагов 4 (+3 исследования), агентов 6, handoff 0, пропущен шаг 4. В pixel-swarm не закоммичены .claude/deploy-client.json и
правка project-skills.md - нужны при update-framework.
