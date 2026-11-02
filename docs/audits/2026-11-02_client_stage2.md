# Аудит кода: client, второй этап (2026-11-02)

Продолжение [аудита от 2026-11-01](2026-11-01_client.md). Здесь только то, что не закрыто на первом этапе (коммиты `e1bb569`, `75d2714`). Новой проверки кода не было: номера находок те же, разборы критичных находок в первом файле.

Порядок работы тот же: падающий тест, правка, повторный прогон. Находка, для которой тест не падает, не считается исправленной и записывается как неподтверждённая.

**Насколько точны ссылки.** Строки оставшихся пунктов из первого этапа и могли сдвинуться, кроме I10 и размеров файлов в P11. Перед работой над пунктом его нужно перепроверить.

## Сделано на втором этапе
Не закоммичено. Результат: EditMode 365 (361 прошло, 4 пропущено, 0 упало), PlayMode 12/12, Verify ALL PASSED. Игры с этими правками не собирались.

Падающий тест, затем правка:
- **C6.** Прежний тест не падал, потому что второй клип декодировался слишком быстро. С длинным клипом баг воспроизвёлся. `AudioGroupAsset.Decode` ждёт все задачи и при ошибке уничтожает клипы.
- **E9.** «Run All» теперь `RegenerateAll.Run`, кнопки вызывают `GenerateNow`, ошибка попадает в статус. Тест проверяет только вызовы по IL: настоящий прогон записал бы каталоги проекта. Полоса прогресса удалена.
- **E10.** Спрайт без группы даёт `LogError`, инспектор и генератор используют один список расширений.
- **E14.** Незавершённую трассу закрывает новый `ProfilerTraceFinisher`, окно для этого не нужно.
- **E15.** Смешанное значение `[Flags]` печатается числом, запись гостя не в формате json показывается как `<malformed>`.
- **N7.** `JsWebRtcPeer.cs` компилируется только в WebGL-плеере. Тест проверяет отсутствие типа в редакторе, сборка IL2CPP не запускалась.
- **N14.** Без лога остаются только `OperationCanceledException` и `ObjectDisposedException`.

- **I10.** `ListenBack` теперь метод `IBackInput` и вызывается в начале кадра по событию нажатия; одно нажатие получает только окно, добавленное последним.

- **N10.** Единые `SocketInbox` и `SocketPump` для четырёх сокетов. Настоящий баг: в `WebRtcPeerSocket` ошибка обработчика навсегда останавливала цикл. Теперь ошибка логируется, следующие кадры идут.
- **U10.** Все `Process*` через `ExitAfter`. `ProcessStack` и `Process` больше не оставляют состояние без обработчика входа: его ограничения действовали до конца lifetime.
- **I5.** `settings` больше не зависит от `localization` и TextMeshPro. `Language` и `IsLanguagePicked` убраны из `SettingsSave`, игре нужны свои поля и `OnLoad` с `OnPush`. Зависимость `telegram` от `inputs` оставлена по решению.

Без падающего теста, проверено сборкой и прогоном:
- **E6.** Закрыто ещё на первом этапе, файл второго этапа ошибался: сканер бросает на override (`RequireNoVariantOverride`), есть тест `ContainerMap_VariantOverrideOfAutoDetected_Throws`. Правка в `HierarchyBindingsEntityViews` не нужна.
- **G12, часть.** Удалён `ScopeCodegenDiagnosticTests`: он читал текст исходника, а CINGR001, 003 и 004 проверяются настоящим прогоном в Verify. Из `ContainerBenchmarkTests.AssertReport` убраны проверки, которые не могли упасть.
- **U8, U9, C14, E16, I12, часть N11.** Мёртвый код удалён, в том числе 22 файла, `ClearStack`, `OuterLifetime`, `IContainerDiagnostics.History` с секцией окна отладчика. Перед удалением код и guid проверены по шести потребителям: mines-leader, peace-data-reborn, pixel-swarm, pixel-swarm-audio, pixel-swarm-ecs, storytale-workshop. `gradient-puzzle` не в счёт: у него свои копии этих классов и нет submodule. Для `post-radio` клиентской папки нет.
- **`.Forget()` заменён на `.NoAwait()`** везде, кроме `AsyncExtensions`. `Internal.Flow.Editor` теперь ссылается на `Internal`.
- **C8.** В `EventLoop` по одному циклу в `Invoke` и `InvokeAwaited` вместо трёх, поведение то же.
- **C9.** Сбор ошибок вынесен в `ScopeTeardown`. Порядок шагов сохранён как был: в неудавшейся загрузке контейнер освобождается раньше выгрузки сцен, в обычном teardown позже. Это отдельное решение, его не трогал.
- **I8.** `IPlatformAuth`: один `GetCredentials`, заглушки с `throw` и `SteamAuthTicket` удалены.
- **I9.** Вне Steam регистрируются `NoPlatformInventory`, `NoPlatformInvites`, `NoPlatformAchievements`, проверки `IsStarted` и `ISteamClient` удалены. На настоящем Steam не запускалось.
- **U12.** `InputConstraints` и `InputConstraintsStorage` перенесены в `Global.UI`.
- **Суффикс `Async`.** Убран у колбэков scope (`OnSetupAsync` стал `OnSetup` и так далее) и у внутренних методов. `PlayAsync` стал `PlayAwaited`, `RunCustomAsync` стал `RunCustomAwaited`: их имя без суффикса занято синхронным вариантом.
- **C10.** `UpdateTargets` и `UpdaterSpeed` общие для `Updater` и `UpdaterProxy`; `GroupEntries.Find` вместо копий с защитными проверками.
- **E12.** `CatalogGroupAssets` (запись, перенос, удаление устаревших групп) для четырёх генераторов, `GeneratedFile.GroupByFolder` и `DeleteFromUnusedFolders` для двух генераторов классов.
- **N11, I13.** `PeerProbeCommand.EchoOf`, `SpriteAnimation.Prepare`, `Pick` в `PlayRandomFromGroup`, `JsLibStub.Missing` вместо 20 копий. `ForwardAnimationAsset` и `ForwardAnimationData` не объединены: это ломает сериализованные ассеты игр, а это не API. Ожидание места в `SessionControl` и `NetworkSession` не объединено: условия разные.
- **N15, I14.** Комментарии переведены и очищены от ссылок на игры. Осталось на русском вне находок аудита: `ProfilerTraceDownload.jslib`, тексты `ProfilerTraceWindow` и `ProfilerFrameCapture`.

Третий заход, закрывает оставшееся:
- **N6, часть.** Откат на туннель работал, но случайно: `TimeoutWithoutException` выдаёт отмену за таймаут. Поэтому `Lost` сразу после `Open` писал в лог «не открылся за 10 с» вместо причины, а конец lifetime снаружи давал `WebRtcConnectFailedException`, и `PeerHostSocket` открывал туннель на мёртвом lifetime. Оба теста падали, после правки `WebRtcPeerSocket.Connect` прошли.
- **G12, остаток.** Текстовые `TestOptionalAlternative` и `TestRegistrationShape` заменены сценариями, которые запускают сгенерированный код. Мутация генератора (слот 0 для всех экземпляров, без `RequireAlternative`) роняет оба. `TestManifestEmitsAttributes` удалён: запись и чтение манифеста проверяет `TestCrossAssemblyManifest`. `Graph_MatchesProductionShape` и ставший ненужным `BenchmarkGraph.Count(ServiceLifetime)` удалены.
- **N12.** Исключение записано в P3: то, что framework показывает без ассета игры, собирается из кода (оверлей задержки, уведомление об устаревшем клиенте).

## Что должны поменять игры (`BREAKING:`)
- `IScope*Async`: методы без суффикса (`OnSetup`, `OnBaseSetup`, `OnSetupCompletion`, `OnLoaded`, `OnDispose`). Класс не может реализовать `IScopeSetup` и `IScopeSetupAsync` одновременно.
- `IAnimation.PlayAsync` стал `PlayAwaited`, `IEventLoop.RunCustomAsync` стал `RunCustomAwaited`.
- `back.ListenBack(lifetime, updater, onBack)` стал `back.ListenBack(lifetime, onBack)`.
- `IPlatformAuth.GetTelegramInitData` и `GetSteamTicket` заменены на `GetCredentials`, поле `Ticket` содержит initData или ticket.
- `AddSteam(options)` стал `AddSteam(options, platformOptions)`, `ISteamClient` удалён.
- `InputConstraints` теперь в `Global.UI`.
- `SettingsSave.Language` и `IsLanguagePicked` удалены: поля и `Localizer.SetLanguage` переходят в настройки игры (`OnLoad`, `OnPush`). JSON сейчас совпадает, если имена полей те же.
- `Process` и `ProcessStack` закрывают состояние без обработчика входа по завершении вызова.
- `_behaviours` у `DesignElement`, `WebPage.Reload`, `ITelegramAPI.GetLanguageCode`, `IContainerDiagnostics.History` и остальной мёртвый код удалены.

## Итог
Осталось: 4 слабо подтверждённых исправления, C13 и зависимость `telegram` от `inputs` (отложены решением), архитектура 1 пункт (C12), мелких 8 пунктов (3 из них массовый стиль).

Вынесено в задачи: разрез файлов `docs/tasks/current/file_splits` (P11, G11, U7, E11, I6, I7, N8, N9, C11), сеттеры и поля `docs/tasks/current/public_setters` (P6).

## Баги

### Исправлены, но подтверждены слабо
Код менять не нужно, нужен более сильный тест.
- **N5:** тест `JsWebSocketCloseTests` идёт через reflection, настоящий браузер не проверялся.
- **N6, `WebRtcPeers.IsSupported`:** тест подтверждает мало, другая платформа не проверялась.
- **I2, `NoGamepadReader`:** это заглушка, платформы без раскладки не запускались.
- **G9c:** проверка прямым вызовом, а не через сборку.

## Нарушение правила framework
- **C13.** `client/core/Runtime/Common/PlatformType.cs:9`, `Runtime/Options/PlatformOptions.cs:11`: core знает Telegram, Steam и имена профилей. Значения перенести в `publish` и `platforms`.

## Архитектура

### Зависимости и варианты
- **C12.** `ObjectLifetime/GameObjectLifetimeExtensions.cs:20`, `Catalogues/Audio/ButtonSounds.cs`, `Tools/Profiling/ProfilerScope.cs:61`: утилиты знают фичи (P14). `ListenClick` и `ButtonSounds` в `ui`, счётчик в `AllocationCounter`.

## Мелкие

### Массовый стиль
- **Скобка на той же строке: 1200, все в `client/core/Source~`.** `ContainerGenerator/Entity/EntityAssetIndex.cs:8`, `:14`, `:18`.
- **`///` XML docs: 550 строк в 54 файлах.** `client/core/Editor/Tools/Profiling/ProfilerFrameCapture.cs` (60), `Runtime/Tools/Profiling/GameProfiler.cs` (33), `client/modules/ui/Runtime/Design/Layout/DesignLayoutEnums.cs` (25).
- **Несколько типов в файле: 107, из них 89 вне тестов и генератора.** `DesignLayoutEnums.cs` (11), `ScopeDefaultCallbacks.cs` (10), `Updatables/Interfaces.cs` (8), `network/Runtime/Local/LocalServers.cs` (6), `GamepadReader.cs` (5), `AudioCatalogGenerator.cs` (5).

### Остальное
- **U11.** `client/modules/ui/Runtime/Design/Layout/DesignLayoutBase.cs:46`, `Elements/DesignElement.cs:39`, `Buttons/DesignButton.cs:36`: защитные fallback (P8).
- **U13.** Порядок членов: `UIStateMachine.cs:26`, `Handles/UIStateHandle.cs:81`, `DesignButton.cs:72`, `client/modules/audio/Runtime/AudioPlayer.cs:128`, `client/modules/cameras/Runtime/CurrentCamera.cs:61`, `ItchSaves.cs:51`.
- **C15.** Структура (P12): `Common/DataTypes/Structs/` как свалка утилит, `RootScopeExtensions.cs` в корне `Runtime`.
- **N13.** `client/modules/network/Runtime/Commands/`, `Common/`: команды лежат отдельно от своих фич (P12).
- **I11.** `client/modules/localization/Runtime/LocalizedLabel.cs:32`: `if (_label == null) return` скрывает подписку на lifetime длиннее метки (P8).

## Проверка в играх
Не находки аудита, а незавершённая проверка первого этапа.
- **Ни одна игра не собрана с новым framework.** Генератор прогонялся по исходникам игр в памяти, по устаревшим csproj. У pixel-swarm был CINGR003 на `MenuScreen` (`IUISounds`), он не подтверждён настоящей сборкой.
- **mines-leader.** `tools/framework` на `c5b7c94`, без правок первого этапа. Оси в `InputManager.asset` не закоммичены. Предупреждения CINGR015 разбирает задача `docs/tasks/current/unused_with_parameter` этой игры.
- **storytale-workshop.** 3 предупреждения CINGR015: `SpawnPointRoot`, `Trigger`, `LevelObjectsSelection`. Задачи нет.
- **Изменения поведения для игр (первый этап).** Корни сущностей получают `ScopeLifetime`, и порядок разрушения меняется; уведомлению об устаревшем клиенте нужен `IUINavigation`; генератор на blob v2 требует пересборки.

## Не проверено
- **Что не вошло в первый этап.** Список непрочитанных файлов и ограничений `audit.sh` в разделе «Не проверено» [первого файла](2026-11-01_client.md) остаётся в силе. Второй этап его не закрывает.
- **Новый код первого этапа.** Правки и тесты первого этапа этим аудитом не проверялись.
