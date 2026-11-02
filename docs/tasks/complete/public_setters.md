## Публичные сеттеры и поля

### Что сделано
- Счётчик Roslyn: было 790 публичных сеттеров и полей, стало 188, нарушений вне исключений 0 (`d8b482d`).
- Runtime, инструменты редактора, stored-состояния, сообщения Orleans, тесты и генератор переведены на `private set`, `init`, конструкторы и intent-методы.
- Записи каталогов в YAML: `[SerializeField] private` + `FormerlySerializedAs`, чтение старых ассетов подтверждено. Форматы сохранений и stored-состояний закреплены тестами, написанными на старом коде.
- `IEventStateValue.Id` без сеттера; `EventStorage.Read` бросает, если `Id` снимка не восстановился.
- `BREAKING:` игры правятся отдельно, что менять, написано в блоках `BREAKING:` коммитов.

### Ключевые файлы
- `.claude/docs/CODE_STYLE_FULL.md`, раздел «Data Changes Only Through Methods»: единственный список исключений
- `.claude/docs/ARCHITECTURE_PRINCIPLES.md`: P6
- `.claude/docs/COMMON_ORLEANS.md`: `[JsonProperty]` на `private set`, `Id` агрегата
- `backend/Tests/State/StoredStatesFormatTests.cs`: формат stored-состояний

### Заметки
- Остаток 188: `[MemoryPackable]` 99, поля `JsonUtility` 64, контракты внешних механизмов 22, тесты и генератор 3.
- `init` не компилируется в Unity, там конструктор или метод. `FileLoggerOptions` на `init`: `ConfigurationBinder` молча теряет `private set`.
- Счётчик и инвентаризация в репозитории больше не лежат: `git show d8b482d:docs/tasks/current/public_setters/count_public_members.cs`, рядом `public_setters_inventory.md`.
- Игры ещё не обновлены и с этим framework не собираются.
- Не взят четвёртый заход аудита: worktree `audit_minor`, патч `git show d8b482d:docs/tasks/current/public_setters/handoff/foreign_audit_fourth_pass.patch`.
- Кандидаты на удаление по P13: `ObserverData.UpdateDate`, `TransactionContext.ExceptionMessage`.
- Нестабильный `WebRtcPeerSocketTests.Connect_NoAnswer_FailsInTimeout` разбирается в `test_speed`.
