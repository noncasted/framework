# Дефекты, найденные при разрезе файлов

## Контекст

Найдено в задаче `file_splits` (`docs/tasks/complete/file_splits.md`). Там поведение менять было нельзя, поэтому ничего из этого не исправлено. Строки могли сдвинуться, перед работой перепроверить. Порядок работы: сначала падающий тест, затем исправление, затем все тесты.

## Дефекты

1. **Гонка в libdatachannel.** `setRemoteDescription` отдаёт описание ICE раньше, чем сохраняет его в `mRemoteDescription`. DTLS в этом окне получает `unknown CA`, соединение закрывается, следующий кандидат получает `add remote candidate failed: error=-2`. WebRTC-тесты (`WebRtcPeerSocketTests`, `WebRtcPeerSocketFailureTests`, `PeerHostFirstFrameTests`, `PeerHostTests`) падали в 4 из 25 полных прогонов EditMode на коде до разреза. Воспроизведено стендом на C: с задержкой от 5 мс падают 12 соединений из 12. Что останавливает главный поток на 5 мс, не измерено. Отдельно: `SCTP write: ... protocol is shutdown` в `Connect_Frames_ArriveInOrder`, не воспроизводилось.
2. **Редактор загружает не тот плагин.** В `/proc/<pid>/maps` редактора `/usr/lib/libdatachannel.so.0.24.5` на OpenSSL, а не закоммиченный плагин на mbedtls v0.24.6. Причина не выяснена. Тесты проверяют не ту сборку, что поставляется.
3. **N8: исключение отчёта останавливает пинги.** В цикле `NetworkLatencyMonitor.Run` нет `catch` (`client/modules/network/Runtime/Latency/NetworkLatencyMonitor.cs:81`, запуск через `NoAwait`). Источник: `GuestLatencyReport.cs:18`, `HostLatencyReport.cs:20` читают маршрут по индексу и бросают `KeyNotFoundException`, если его ещё нет. Закреплено тестом `KnownDefect_FailedReportStopsTheProbes`, при исправлении он меняется. Вероятность в игре низкая.
4. **`SpriteGenerator.GenerateInternal` не сбрасывает реестр.** Нет `SpriteGroupsRegistry.Instance.Invalidate()`, в отличие от генераторов Prefab, Asset и Audio (`client/core/Editor/Catalogues/Sprites/SpriteGenerator.cs`). Может быть намеренно, проверить.
5. **`PrefabScreenshot.Capture`.** Блок `finally`: `RenderTexture` уничтожается, пока камера держит его целью, каждый снимок логирует `Releasing render texture that is set as Camera.targetTexture!`. При исправлении убрать `LogAssert.Expect` из `PrefabScreenshotTests`.
6. **Обобщённый корень контейнера.** `plan.HintName = plan.ClassName + ".g.cs"` (`client/core/Source~/ContainerGenerator/Scope/Plan/ScopePlanBuilder.cs:45`) даёт имя с `<`, CINGR001 `The hintName ... contains an invalid character`. Видно на кейсах Verify, выяснить, намеренно ли.
7. **`client/project`, ложные падения тестов.**
   - `Assets/common` и `Assets/Common` различаются только регистром: лог Unity при импорте роняет случайный тест.
   - В проекте нет настроек Addressables: отложенный запуск `CatalogGenerationRunner.Schedule` (`CatalogGenerationRunner.cs:119`) логирует `[SharedAddressablesSync] Addressable settings are missing` (`SharedAddressablesSync.cs:55`) внутри следующего теста. Не воспроизводилось отдельно.
   - Открытие графа (`ContainerLiveGraphAsset`) создаёт `Assets/Common/Internal/Editor/Transient/...`, `git status` становится грязным.
   - В `InputManager.asset` нет осей `Gamepad Axis`, поэтому `GamepadReader.Substitute` и `ReadInput` там не проверить.
8. **Мелочи.** `AudioSourceCollector.EnumerateMarked(string logTag)`: параметр не используется. `SpriteCatalogMetadataJson.ReadTagSpeeds`: `tag` может остаться `null`, остальные чтения ставят `?? string.Empty`. `GeneratedScopeDisposeErrors.Throw` на пустом списке бросает пустой `AggregateException`, сейчас недостижимо.

## Не проверено в file_splits

- Плееры IL2CPP и WebGL с новым runtime контейнера и сетью; jslib в браузере.
- Полная генерация каталогов (`GenerateInternal`, `CatalogAddressablesSync.Sync`, `CatalogGroupAssets.Move`) в проекте с Addressables.
- Чтение реальных кадров профайлера, клик по свёртке строки в `ProfilerTraceWindow`, domain reload с открытым окном.

## Вне области file_splits

15 файлов backend длиннее 300 строк: `Session/Root/Session.cs`, `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`, `Infrastructure/Orleans/State/Direct/DirectStorage.cs`, `Infrastructure/Orleans/Transactions/Transactions.cs`, `Session/Root/SessionAdmission.cs`, `Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`, `Infrastructure/Orleans/Transactions/TransactionAttribute.cs`, `Infrastructure/Orleans/State/Events/EventStorage.cs`, `Hosting/Lobbies/LobbyService.cs`, `Infrastructure/Messaging/Channels/RuntimeChannel.cs`, `Cluster/Deploy/DeploymentState.cs`, `Infrastructure/Messaging/Pipes/MessagePipeClient.cs`, `Hosting/Lobbies/LobbySessions.cs`, `Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs`, `Infrastructure/Messaging/Queues/DurableQueueClient.cs`.
