---
task: file_splits
updated: 2026-11-02
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [1] | [x] | `baseline.md`: Verify 149/149, EditMode 361 passed + 4 skipped, PlayMode 12/12, md5 `4924df6a...` | — |
| [2] | [x] | `file_splits_gen` @ `ca6cc23`: Verify 149/149, снимок и вывод кейсов равны эталону (перепроверено оркестратором), 4 игры без отличий (со слов агента) | — |
| [3] | [x] | `93b20e9`: EditMode 406 + 4 skipped, PlayMode 56/56; проверено по `final.*.xml`, файлы до 165 строк | — |
| [4] | [x] | Шаг 4b: EditMode до разреза 4/25 прогонов с падением, после 0/25; diff без смысловых отличий | — |
| [5] | [x] | EditMode 463 + 4 skipped, PlayMode 71/71; дампы окон и графов совпали до и после (со слов агента) | — |
| [6] | [x] | EditMode 507 + 4 skipped, PlayMode 71/71; дамп привязок побайтно равен (43 сценария) | — |
| [7] | [x] | EditMode 542 + 4 skipped, PlayMode 71/71; дамп каталогов побайтно равен | — |
| [8] | [ ] | — | — |
| [9] | [ ] | — | — |

## Заметки

### [2026-11-02] Находки шага 7
- Не проверено: `GenerateInternal` целиком (`CatalogAddressablesSync.Sync`, `CatalogGroupAssets.Move`), в `client/project` нет настроек Addressables; реальные каталоги игр не генерировались.
- `AudioSourceCollector.EnumerateMarked(string logTag)`: параметр не используется.
- `SpriteCatalogMetadataJson.ReadTagSpeeds`: `tag` может остаться `null`.
- `SpriteGenerator.GenerateInternal` не вызывает `SpriteGroupsRegistry.Instance.Invalidate()`, в отличие от Prefab, Asset и Audio.
- Решение оркестратора: internal-типы `PrefabSource`, `AssetSource`, `ClipSource`, `MarkedSource` остаются top-level, иначе зависимость шла бы вверх (P14). Они не попадают в `$type` и ассеты.

### [2026-11-02] Находки шага 6
- Ещё одно ложное падение: `GameLoopScopeLoaderTests.Load_AfterFailedDispose_Succeeds` и другие тесты с `[SharedAddressablesSync] Addressable settings are missing`. Вероятно, debounce `CatalogGenerationRunner.Schedule` (`CatalogGenerationRunner.cs:119`) срабатывает внутри теста, а в `client/project` нет настроек Addressables (`SharedAddressablesSync.cs:55`). Не воспроизводилось отдельно.
- Мёртвый код: `HierarchyBindingsCodeGenerator.FileSuffix`, `HierarchyBindingsScanner.ToCodeTypeName(Type)`.
- `HierarchyBindingsBatch.PrefabItem` стал top-level `HierarchyBindingsPrefabItem` (internal).

### [2026-11-02] Находки шага 5
- `PrefabScreenshot.Capture`, блок `finally`: `RenderTexture` уничтожается, пока камера держит его целью, каждый снимок логирует ошибку. Закреплено через `LogAssert.Expect` в `PrefabScreenshotTests`.
- `ContainerLiveGraphAsset`: открытие графа создаёт `Assets/Common/Internal/Editor/Transient/...` в рабочем дереве `client/project`, `git status` становится грязным.
- Не проверено: чтение реальных кадров профайлера, клик по свёртке строки в `ProfilerTraceWindow`, domain reload с открытым окном.
- Решение оркестратора: `ContainerGraph.cs` и `LifetimeGraph.cs` переименовываются по типам на шаге 9, с `git mv` файла и `.meta`.

### [2026-11-02] Шаг 4b: регрессии нет
Чередование по 25 полных прогонов EditMode: до `NativeWebRtcPeer` 4 с падением, после 0 (p≈0,11, разницы не доказывает). Гонка в libdatachannel: `setRemoteDescription` отдаёт описание ICE раньше, чем сохраняет его, поэтому DTLS в этом окне получает `unknown CA`, а следующий кандидат `error=-2`. Воспроизведено стендом на C: с задержкой 5 мс и больше падают 12 из 12. Редактор загружает системный `/usr/lib/libdatachannel.so.0.24.5` на OpenSSL, а не закоммиченный плагин. Логи: `scratchpad/step4b/`.

### [2026-11-02] Решение пользователя: тесты в конце шага
Начиная с шага 5 полный прогон EditMode и PlayMode идёт в конце шага, а не после каждого класса. По коммиту на класс остаётся. Это отступление от пункта 2 раздела «Что сделать».

### [2026-11-02] Слияние шагов 2 и 4 с main
- `file_splits` перебазирован на `main` (коммиты `public_setters`). Единственный конфликт: сеттеры `GamepadState` перенесены в вынесенный `GamepadState.cs` и `ReadInput`.
- `file_splits_gen` перебазирован на `file_splits`, слияние fast-forward.
- Старый (`4004262`) и новый генератор на одном дереве дают одинаковый вывод, md5 манифеста `24993616...`. `harness/baseline_manifest.md5` обновлён на него: прежний эталон устарел из-за новой регистрации в `network`.
- Первый прогон EditMode после слияния уронил `AudioPlayerTests.UpdaterPaused_MusicStillFadesIn` на логе Unity о `Assets/common` и `Assets/Common` (различие только в регистре), код теста ни при чём. Лог пишется при импорте и может попасть в любой тест. Это особенность `client/project`.
- Второй прогон EditMode на том же коммите дал другие два падения: `PeerHostFirstFrameTests.OpenAndFirstFrameInOneFrame_GuestIsAuthorized` (`DTLS alert: unknown CA`) и `WebRtcPeerSocketTests.Connect_Frames_ArriveInOrder` (`add remote candidate failed: error=-2`). Шаг 4 поэтому не считается доказанным, пока шаг 4b не сравнит частоту падений до и после разреза `NativeWebRtcPeer`.

### [2026-11-02] Находка шага 4
Подтверждён дефект N8: исключение отчёта завершает цикл пингов до конца сессии (`NetworkLatencyMonitor.cs:81`, источник `GuestLatencyReport.cs:18`, `HostLatencyReport.cs:20`). Поведение сохранено, закреплено тестом `KnownDefect_FailedReportStopsTheProbes`.

### [2026-11-02] Находки шага 2
- Ветка `file_splits_gen` вливается в `file_splits` после шага 4: Unity этого worktree занят, смена dll вызвала бы перекомпиляцию посреди прогона.
- Тела методов перенесены скриптом, границы выбраны по графу ссылок между членами. Агент читал не все классы, поэтому на шаге 9 границы смотрит независимый агент.
- `client/core/Runtime/Tools/Container/Runtime/ServiceRegistration.cs:8` называет `GraphWalker.IsRegistrationLike`, теперь это `InstallerClassifier`. Исправит шаг 8.
- Обобщённый корень даёт hint name с `<`: `Scope/Plan/ScopePlanBuilder.cs:45`, CINGR001. Поведение старое.

### [2026-11-02] Находки шага 3
- Нестабильный тест `WebRtcPeerSocketTests.OtherSideTerminated_ClosedIsRaised` (`client/modules/network/Tests/Editor/WebRtcPeerSocketTests.cs:77`): libdatachannel пишет `DTLS alert: unknown CA` в лог ошибок, 1 падение на 8 полных прогонов EditMode. С разрезами не связан.
- В `client/project/ProjectSettings/InputManager.asset` нет осей `Gamepad Axis`, поэтому `GamepadReader.Substitute` и `ReadInput` в проекте фреймворка не проверить.
- Коммит `0d69baf` создан раньше проверки своего прогона. Повторный прогон на этом коммите зелёный.

### [2026-11-02] Решения оркестратора
- Работа идёт в ветке `file_splits`, не в `main`. Коммиты по одному классу задача требует сама.
- Работа идёт в worktree `.claude/worktrees/file_splits`: в основном дереве параллельно идёт `public_setters`.
- Unity только один за раз, в этом worktree. Свободно около 11 ГБ, редактор pixel-swarm занимает память, а `control copy` рассчитан на проект `client`: в фреймворке нет `.claude/control.json`, проект здесь `client/project`.
- Параллельно с Unity идёт только генератор: это чистый dotnet в своём worktree.
