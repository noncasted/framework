## Разрез больших файлов — Результат

### Статус: В работе

### Что сделано
- E11: `ContainerDebuggerWindow` и ещё шесть файлов окон и графов разрезаны, дубли `ContainerGraphRead` убраны, 28 тестов на старом коде.
- N8, N9: файлы модуля `network` разрезаны, 15 PlayMode-тестов монитора на старом коде. `BREAKING:` конструктор `NetworkConnection` принимает `IWebSocketFactory`; игры его напрямую не вызывают.
- G11: генератор разрезан на 93 файла, самый длинный 293 строки, без `partial`; вывод не изменился.
- U7, I6, I7: `UINavigation`, `AudioPlayer`, `GamepadReader` разрезаны, API не менялся, добавлены тесты на старом коде (+45 EditMode, +44 PlayMode).

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `client/core/Editor/Tools/{Container,ContainerCodegen,ContainerGraph,LifetimeGraph,Profiling,GraphWindows}/**`, `PrefabScreenshot*` | разрез семи файлов | 5 | EditMode 463, PlayMode 71/71, дампы окон |
| `client/modules/network/**` | разрез пяти файлов, тесты монитора | 4 | PlayMode 71/71, сборка WebGL-ветки Roslyn |
| `client/core/Source~/ContainerGenerator/**` | разрез, удалён мёртвый код | 2 | `ca6cc23`, `check_generator.sh` |
| `client/modules/ui/Runtime/Navigation/*` | разрез `UINavigation` | 3 | `3e3b0de`, тесты зелёные |
| `client/modules/audio/Runtime/*` | разрез `AudioPlayer` | 3 | `0d69baf`, тесты зелёные |
| `client/modules/inputs/Runtime/*` | разрез `GamepadReader` | 3 | `93b20e9`, тесты зелёные |

### Отличия от плана
- С шага 5 по решению пользователя тесты прогоняются в конце шага, а не после каждого класса. Промежуточные коммиты шага отдельно не проверяются.

### Нерешенные вопросы
- `PrefabScreenshot.Capture` логирует ошибку освобождения `RenderTexture` при каждом снимке.
- Открытие графа пачкает `client/project` файлами `Transient`.
- N8: исключение отчёта останавливает пинги, поведение сохранено, см. `_progress.md`.
- Нестабильность WebRTC-тестов: гонка в libdatachannel, около 8% полных прогонов EditMode на старом коде; нужна отдельная задача. Редактор загружает системную libdatachannel вместо плагина.
- `Assets/common` и `Assets/Common` в `client/project` различаются только регистром; лог Unity об этом может уронить тест при первом импорте.
- Обобщённый корень даёт CINGR001 из-за `<` в hint name (`Scope/Plan/ScopePlanBuilder.cs:45`).
- Публичные изменяемые поля в `WalkState`, `WalkLocal`, `ScopePlan`, `GraphRegistration` (P6) остались как были.
- Нестабильный `WebRtcPeerSocketTests.OtherSideTerminated_ClosedIsRaised`, см. `_progress.md`.
