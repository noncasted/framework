## Разрез больших файлов — Результат

### Статус: Завершено

### Что сделано
- Границы классов генератора поправлены по независимому просмотру (11 пунктов), вывод прежний.
- C11: `GeneratedScopes` и `ContainerBuilder` разрезаны, 64 теста на старом коде, вывод генератора прежний.
- Каталоги: восемь файлов разрезаны, самый длинный 257 строк, 34 теста на старом коде, дамп вывода побайтно прежний.
- HierarchyBindings: четыре файла разрезаны, самый длинный 221 строка, вывод генератора привязок побайтно прежний.
- E11: `ContainerDebuggerWindow` и ещё шесть файлов окон и графов разрезаны, дубли `ContainerGraphRead` убраны, 28 тестов на старом коде.
- N8, N9: файлы модуля `network` разрезаны, 15 PlayMode-тестов монитора на старом коде. `BREAKING:` конструктор `NetworkConnection` принимает `IWebSocketFactory`; игры его напрямую не вызывают.
- G11: генератор разрезан на 93 файла, самый длинный 293 строки, без `partial`; вывод не изменился.
- U7, I6, I7: `UINavigation`, `AudioPlayer`, `GamepadReader` разрезаны, API не менялся, добавлены тесты на старом коде (+45 EditMode, +44 PlayMode).

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `client/core/Runtime/Tools/Container/Runtime/**` | разрез двух файлов, 64 теста | 8 | EditMode 606, снимок равен эталону |
| `client/core/Editor/Catalogues/{Sprites,Prefabs,Assets,Audio}/**` | разрез восьми файлов, тесты | 7 | EditMode 542, дамп md5 `d3fb2151...` |
| `client/core/Editor/Tools/HierarchyBindings/**` | разрез четырёх файлов, 44 теста | 6 | EditMode 507, дамп md5 `5dc361be...` |
| `client/core/Editor/Tools/{Container,ContainerCodegen,ContainerGraph,LifetimeGraph,Profiling,GraphWindows}/**`, `PrefabScreenshot*` | разрез семи файлов | 5 | EditMode 463, PlayMode 71/71, дампы окон |
| `client/modules/network/**` | разрез пяти файлов, тесты монитора | 4 | PlayMode 71/71, сборка WebGL-ветки Roslyn |
| `client/core/Source~/ContainerGenerator/**` | разрез, удалён мёртвый код | 2 | `ca6cc23`, `check_generator.sh` |
| `client/modules/ui/Runtime/Navigation/*` | разрез `UINavigation` | 3 | `3e3b0de`, тесты зелёные |
| `client/modules/audio/Runtime/*` | разрез `AudioPlayer` | 3 | `0d69baf`, тесты зелёные |
| `client/modules/inputs/Runtime/*` | разрез `GamepadReader` | 3 | `93b20e9`, тесты зелёные |

### Отличия от плана
- Добавлен шаг 10: 11 правок границ классов генератора по независимому просмотру.
- C11 выполнен без изменения эмиттера: `GeneratedScopes` остался фасадом.
- С шага 5 по решению пользователя тесты прогоняются в конце шага, а не после каждого класса. Промежуточные коммиты шага отдельно не проверяются.

### Нерешенные вопросы
- Ветка `file_splits` не влита и не запушена, основана на локальном `main`; локальный `main` и `origin/main` разошлись.
- Шаг F2 задачи `public_setters` ждёт вливания генератора.
- Плееры IL2CPP и WebGL не собирались, игры с новой версией не собирались.
- Длиннее 300 строк вне области задачи: backend 15 файлов (задача родом из аудита клиента, отдельная задача): `Session/Root/Session.cs` 523, `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` 519, `Infrastructure/Orleans/State/Direct/DirectStorage.cs` 451, `Infrastructure/Orleans/Transactions/Transactions.cs` 418, `Session/Root/SessionAdmission.cs` 380, `Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` 372, `Infrastructure/Orleans/Transactions/TransactionAttribute.cs` 371, `Infrastructure/Orleans/State/Events/EventStorage.cs` 368, `Hosting/Lobbies/LobbyService.cs` 348, `Infrastructure/Messaging/Channels/RuntimeChannel.cs` 341, `Cluster/Deploy/DeploymentState.cs` 339, `Infrastructure/Messaging/Pipes/MessagePipeClient.cs` 329, `Hosting/Lobbies/LobbySessions.cs` 328, `Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` 325, `Infrastructure/Messaging/Queues/DurableQueueClient.cs` 320. Форки `client/packages` 608 (сторонний код), тесты и Verify 41 (P11 без лимита).
- `SpriteGenerator.GenerateInternal` не сбрасывает реестр групп, в отличие от других генераторов; возможный дефект.
- Полный путь `GenerateInternal` каталогов не проверен: нужен проект с Addressables.
- Ложное падение тестов из-за `[SharedAddressablesSync] Addressable settings are missing` в `client/project`.
- `PrefabScreenshot.Capture` логирует ошибку освобождения `RenderTexture` при каждом снимке.
- Открытие графа пачкает `client/project` файлами `Transient`.
- N8: исключение отчёта останавливает пинги, поведение сохранено, см. `_progress.md`.
- Нестабильность WebRTC-тестов: гонка в libdatachannel, около 8% полных прогонов EditMode на старом коде; нужна отдельная задача. Редактор загружает системную libdatachannel вместо плагина.
- `Assets/common` и `Assets/Common` в `client/project` различаются только регистром; лог Unity об этом может уронить тест при первом импорте.
- Обобщённый корень даёт CINGR001 из-за `<` в hint name (`Scope/Plan/ScopePlanBuilder.cs:45`).
- Публичные изменяемые поля в `WalkState`, `WalkLocal`, `ScopePlan`, `GraphRegistration` (P6) остались как были.
- Нестабильный `WebRtcPeerSocketTests.OtherSideTerminated_ClosedIsRaised`, см. `_progress.md`.
