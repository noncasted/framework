## Оркестратор - handoff поколения 1

Всё рабочее состояние в файлах этой папки: `client_telemetry_info.md` (план и решения пользователя), `_progress.md` (snapshot, что поменять в игре на шаге 11), `_result.md`, `_orchestration.md` (граф и журнал).

Код: `/projects/framework/.claude/worktrees/client_telemetry`, ветка `client-telemetry`, не закоммичено, коммитить нельзя без разрешения. Общее дерево `/projects/framework` не трогать, кроме этой папки.

### Сделано и проверено
Шаги 1, 2 (заменён 2b), 2b, 3, 4, 5, 7. Шаг 0 отменён пользователем, его вопросы решаются на шаге 11.

### Работает сейчас
- ORCH-client_telemetry-8-g1, agentId afe71a959ecee693f: клиентская часть ошибок, владеет Unity в worktree.
- ORCH-client_telemetry-3b-g1, agentId aa3b3735dd465641a: постоянный флаг, backend и `backend/Console/Infrastructure/Features`.
Их уведомления придут оркестратору поколения 1, он перешлёт их через SendMessage.

### Дальше (строго этот порядок)
1. 8s: сервер ошибок: отпечаток в `TelemetrySessionBuffer.Apply` (стек без адресов и номеров строк), группы в `Digest`, глобальные группы по коллекции, `GET /errors`. Зависит от 2b.
2. 6: сквозной тест транспорта. У фреймворка своего кластера нет: выбери реальный Kestrel-хост с Orleans-фикстурой и PlayMode-клиент либо реальный браузерный/UnityWebRequest-запрос; кластер pixel-swarm из копии НЕ запускать (второй Postgres на живом томе).
3. 9: панель консоли (после 3b и 8s, Unity не нужна). Стили и поля: `BLAZOR.md`, поля сессии `TelemetrySessionInfo.Digest`, счётчики Dropped*, RejectedRequests, ErrorCount.
4. 10: скилл агента `client-telemetry`, скрипт, адрес из `.claude/project-skills.md`, токен `CONSOLE_TOKEN` из локального файла `~/.config/<игра>/deploy.env`.
5. 11: подключение pixel-swarm, включить `enableFrameTimingStats`, коллекторы игры (коллекторы из другого скоупа: решить; вопрос в журнале). Перед деплоем и коммитом СПРОСИТЬ пользователя: нужен также `CONSOLE_TOKEN` в `~/.config/pixel-swarm/deploy.env` (сейчас там нет).
6. 12: документация, `releases/unreleased.md`, числа тестов в `CLAUDE.md`, `test-all.sh`.

Пока идут агенты, новые шаги с Unity или теми же файлами не запускать. 8s и 9 пересекаются с 3b по backend: ждать его.

### Решения, которые ждут ответа пользователя
- Reopen: вкладка, свёрнутая дольше 15 с, при возврате переоткрывает закрытую сессию и пишет метаданные ещё раз. Пользователю сообщено в последнем сообщении, запись «ровно дважды» в обычной сессии сохранена.
- Память `StateCollection`: около 12 КБ на сессию на хост (10 тыс. сессий = 120 МБ). Сообщено пользователю.

### Правила для брифов
Каждый агент: self-contained бриф по `.claude/skills/orchestrate/SKILL.md`, Phase 3, с ORCH marker и контролем контекста. Просите агентов не читать большие файлы и логи целиком: первый 2b исчерпал контекст. Тесты: `.build/bin/Tests/Debug/net10.0/Framework.Tests -namespace Framework.Tests.ClientTelemetry`; Unity: `/tools/unity/7000.0.0a10/Editor/Unity -batchmode -nographics -projectPath <worktree>/client/project -runTests -testPlatform EditMode -testFilter ... -testResults ... -logFile ...`. Последние числа: Framework.Tests 1404, EditMode 806 (4 skipped) до шага 8, PlayMode телеметрии 11.
