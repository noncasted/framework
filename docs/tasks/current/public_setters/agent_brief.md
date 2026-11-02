# Общая часть брифа агентов исправления

Код правится ТОЛЬКО в worktree `/projects/framework/.claude/worktrees/public_setters` (ветка `public_setters`). Файлы задачи и handoff лежат в основном дереве: `/projects/framework/docs/tasks/current/public_setters/`. Основное дерево `/projects/framework` не трогать: там чужие незакоммиченные правки. Worktree `file_splits*` и код игр (`/projects/mines-leader`, `peace-data-reborn`, `pixel-swarm`, `storytale-workshop`) только читать.

Прочитай: `CLAUDE.md`, `.claude/docs/CODE_STYLE_FULL.md` (раздел про сеттеры), `public_setters_info.md` (решения), свой раздел `public_setters_inventory.md`.

## Правила
- Данные меняются только через методы (`Init`, `Set...`, intent-методы), `{ get; private set; }`; значения, неизменные после создания: `readonly`, `{ get; }`, `{ get; init; }`.
- `common`, `shared` и всё, что компилирует Unity: C# 9 и netstandard2.1. `init` в Unity не компилируется (`CS0518`, проверено), полифил не добавлять, брать конструктор или метод. Полный список исключений: `CODE_STYLE_FULL.md`.
- API менять можно. Игры в этой задаче не правятся. Коммит, меняющий публичный API, содержит `BREAKING:` и список того, что должны поменять игры (искать по играм только для сообщения).
- Поведение сохраняется. Если правка затрагивает сериализацию, сначала тест на старом коде, потом правка.
- Fail fast: без защитных null-проверок и молчаливых fallback. Комментарии в коде на английском. `.meta` не пересоздавать. Разрезом файлов не заниматься, это задача `file_splits`.
- Не трогать другие группы инвентаризации.

## Проверка, всё в worktree
Скретчпад: `/tmp/claude-1000/-projects-framework/c83b6d6a-df3b-4319-a9aa-1c50de253acb/scratchpad`.

База перед шагом (на коммите `ebe5f64`, новые шаги сравнивают с последним коммитом): backend 836, common 92 прошло, Unity EditMode 394: 390 прошло, 4 пропущено, упало 0. Заново снимать базу не нужно, если не появится падение; тогда проверить его на предыдущем коммите.

1. `dotnet build framework.slnx`, затем `.build/bin/Tests/Debug/net10.0/Framework.Tests` и `.build/bin/Framework.Common.Tests/Debug/net10.0/Framework.Common.Tests` (исполняемые файлы, `dotnet test` не работает).
2. Unity EditMode, если затронут код, который компилирует Unity: `/tools/unity/<версия из client/project/ProjectSettings/ProjectVersion.txt>/Editor/Unity -batchmode -projectPath <worktree>/client/project -runTests -testPlatform EditMode -testResults <скретчпад>/results.xml -logFile <скретчпад>/unity.log` (без `-quit`), с большим таймаутом или в фоне. Чужие процессы Unity не останавливать.
3. Счётчик: `dotnet run /projects/framework/docs/tasks/current/public_setters/count_public_members.cs -- <worktree> --md <скретчпад>/after.md`. Членов шага в выводе больше нет.

## Коммит
Один, в ветке `public_setters`, в формате истории (`git log`). Пути добавлять явно. Без push, на `main` не переносить, это делает оркестратор. Последняя строка:
`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Итоговый ответ
Коротко, не пересказывая код: `DONE`, хеш, число файлов, числа тестов (backend, common, Unity), отклонения, что должны поменять игры, открытые вопросы. Проза по-русски.
