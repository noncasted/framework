## Дефекты, найденные при разрезе файлов (file_splits_findings)

### Что сделано
- Дефекты 1-8 из `file_splits` и находки `test_speed`, `public_setters`: сначала красный тест, затем правка. Плагин WebRTC переименован в `framework_datachannel`, редактор грузит именно его; выход без SIGSEGV.
- Backend: 15 файлов длиннее 300 строк разрезаны; исправлены messaging (потеря первой доставки, порядок привязки труб, переподписка), транзакции (перехват без бесконечного ожидания, откат всех участников, два `Join` одной транзакции), `Connection` (клиент получает close-кадр), старт матча лобби.
- Client: `NetworkLatencyMonitor` переживает сбой кадра, утечки каналов WebRTC, битая разметка спрайтов бросает исключение, ложные падения `client/project`, `CompactMenuBar` в batchmode.
- `ContainerGenerator`: CINGR016 на обобщённый корень, корень во вложенном типе, хинт с пространством имён.
- Проверка на копии pixel-swarm: IL2CPP, WebGL, Windows CoreCLR, jslib в браузере, WebRTC между плеерами, каталоги с Addressables, профайлер, сериализация. Дефектов от правок нет.

Итог: backend 839 -> 1102, Verify 148 -> 166, EditMode 615 -> 645, PlayMode 71 -> 77. Шаги игр: `releases/unreleased.md`, 3-9.

### Ключевые файлы
- `backend/Infrastructure/Orleans/Transactions/GrainTransactionOwner.cs`
- `backend/Infrastructure/Messaging/Pipes/RuntimePipeClient.cs`
- `backend/Common/Network/Connection.cs`, `ConnectionWriter.cs`
- `client/modules/network/Runtime/Sockets/WebRtc/NativeWebRtcPeer.cs`
- `client/modules/network/Runtime/Latency/NetworkLatencyMonitor.cs`
- `client/core/Source~/ContainerGenerator/Scope/Plan/ScopePlanBuilder.cs`

### Заметки
- **Кластер из копии проекта через `control` садится на именованный том проекта.** Так 2026-11-02 два Postgres около 8 минут работали на `pixel-swarm-postgres-data`. `control` не исправлен. Том не пересоздан, целостность не доказана (`data_checksums` выключен): решение за пользователем.
- Реестр спрайтов закреплён тестом связности по IL: тест с настоящей генерацией пишет `Sprites.cs` и вешает импорт в полном EditMode.
- `test-all` иногда падает на старте Unity: SIGSEGV в `ApiGLES::ClearBufferSubData` до запуска C#, 1 из 7 прогонов. Дефект запуска движка, `-nographics` не пробовали.
- При `tools/scripts/test-all.sh` числа не проверяются скриптом: сверять с `CLAUDE.md`.

### Открыто
- Не исправлено сознательно: поздний одиночный `Join` закончившейся транзакции занимает зерно до перехвата (нужен отдельный механизм); `_isDisposed` в `NativeWebRtcPeer` не атомарен (только строка лога); `Foo.BarBaz`/`FooBar.Baz` дают CINGR001; возврат группы из `Resources` меняет GUID группы и порядок; очередь `HierarchyBindingsQueue` может потеряться при обновлении.
- Решение пользователя: `MatchmakingTests` находит таймер через `TaskTracker` UniTask и не видит таймер без UniTask.
- Не проверено: запуск на Windows, `TelegramProvider.jslib`, fullscreen с жестом и `beforeunload`, туннельный путь P2P, настоящая мышь и отрисовка окон, редакторы консоли в браузере, модули вне манифеста pixel-swarm, поэтапный деплой при смене сигнатуры `IRuntimeChannel.AddObserver`.
