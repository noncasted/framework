# Resume of the orchestrator (client_telemetry)

Сейчас (2026-11-04 11:10):
- Прод: web 20261104-093234-73eae103, telegram 20261104-093248-73eae103 (новый параллельный deploy-client, 515 с). origin/main игры = origin/client-telemetry = 73eae103 (пачки пользователя до 70e543fb + шаги 44 fe76b453, 45). Framework origin/main = 6f49c1df (сабмодуль там же; 0280e2dd шаг 45, deploy-client параллельной сессии).
- Деплой теперь: из worktree `DEPLOY_PASSWORD_FILE=/projects/pixel-swarm/tools/scripts/.deploy-password tools/framework/tools/scripts/deploy-client.sh` (run_in_background), затем `git push origin HEAD:main HEAD:client-telemetry`. Перед этим `git merge origin/main` в worktree и при нужде сабмодуль на framework origin/main.
- Замеры 73eae103: Android остывший f2b47e3a 30 FPS, cpu 18.4-19.5 до 183 тел, без тел 15.2 (пик тел ~185 из-за лимитов cb2c0682); iPhone 11 ландшафт 1792x662 60 FPS cpu ~5 холодный (нагретый x2 -> fps.auto.30); iPad Низкое ~56 FPS cpu 8.
- 60 FPS Android: handoff/android_60fps.md; ждём забеги ?autorun=1&fps=60 High и Low (пользователь).
- Прокси устройств: iPhone `ios_webkit_debug_proxy -c 00008030-000471D13692402E:9222 -F` (/tmp/claude-1000/lb/iosctl.py, safari-restart.sh), iPad :9223 (tmp/ipad/iosctl.py, run_res.sh).

Работают агенты (уведомления придут сюда):
- Шаг 46 g2 (ORCH-client_telemetry-46-g2 / a06552d2d41799e0a): стресс-тест в /var/tmp/ps-stress, handoff/46_g1.md. Итог: tmp/step46-stress.diff (--binary), step46-msg.txt, step46-framework.diff + msg (PlatformType.WebStress). Применить: framework diff в сабмодуль worktree (commit, push main), game diff `git apply --index` в worktree, указатель сабмодуля, тесты/деплой через deploy-client (стресс-сборку на прод НЕ выкладывать).
- Документ WebGL Troubleshooting (ORCH-web_perf_tools-doc-g1 / a8f305355e700dc03): tmp/webgl-doc/{WEBGL_TROUBLESHOOTING.md, INDEX.diff, sources.md}. Проверить по sources.md, закоммитить в сабмодуле worktree ([Docs] ...), push framework main.

Новые задачи: docs/tasks/current/web_perf_tools (перенос DPR, адаптивного FPS, проверки шейдеров, прогрева, стража аллокаций; пункт 1 вынесен), docs/tasks/current/web_bench_tools (стенды и тулзы отладки, snapshot скриптов; ведёт параллельная сессия пользователя - не трогать), docs/plan/render_mesh_utilities.md (тайлмапы, QuadMesh, OptimizeSpeed - скорее откат ради размера).

Открыто: бесконечная загрузка клиента при недоступном игровом сервере (цикл билета в мёртвую сессию после рестарта бэкенда) - пользователь не просил чинить; релиз фреймворка (BREAKING: IWebSocket.Send, IScreenCanvasCamera, стрип); /workflow-complete.

Правила: CONSOLE_TOKEN не печатать; /projects/pixel-swarm и /projects/framework (кроме docs задач) не трогать; коммит только через diff агентов; кластер не из worktree; без handoff оркестратора агенту.
