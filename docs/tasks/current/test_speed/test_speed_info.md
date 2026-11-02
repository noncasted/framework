# Ускорение тестов

## Контекст

Слова пользователя: ждать тесты часами надоело. Цель: полный набор тестов проходит примерно за одну минуту. Средства любые: параллельность, оптимизация, всё что угодно.

Полный набор фреймворка: `dotnet build framework.slnx`, `Framework.Tests`, `Framework.Common.Tests`, `ContainerGenerator.Verify`, Unity EditMode, Unity PlayMode. `tools/scripts/run-tests.sh` в фреймворке нет, он лежит в играх и запускает другое: backend игры, генератор и Unity EditMode, без `common` и PlayMode. Его `flock` на `/tmp/claude-1000/unity-batchmode.lock` выстраивает Unity-прогоны всех сессий в очередь. Единой команды для фреймворка нет.

Один последовательный проход занял **3 мин 54 с - 3 мин 59 с**, а не часы. Гипотеза: часы складываются из повторов (полный набор после каждого шага задач вроде `public_setters`, 16 шагов), перезапусков из-за нестабильного теста, очереди `flock` и прогонов игр. Эти причины здесь не замерялись.

## База

Коммит `d8b482d`, 2026-11-02, 24 потока, 61 ГБ, worktree с копией `Library`, рядом работали другие сессии. Нагрузка: `uptime`, средняя за минуту, в начале и в конце этапа.

| Этап | Время, с | Тестов | Нагрузка |
|---|---:|---|---|
| Копия `Library` (1,2 ГБ, reflink) | 1 | - | 8,5 -> 8,1 |
| `dotnet build`, чистая | 20,0 (первый запуск 19,5) | - | 8,1 -> 7,1 |
| `dotnet build`, инкрементальная | 2,5 (первый запуск 1,8) | - | 8,1 -> 7,0 |
| `Framework.Tests` | 99,1 | 836 пройдено | 5,7 -> 3,6 |
| `Framework.Common.Tests` | 0,3 | 92 пройдено | 3,6 |
| Сборка Verify, Release | 3,5 | - | 3,6 -> 3,5 |
| Запуск Verify | 9,1 | 149 PASS, `ALL PASSED` | 3,5 -> 5,2 |
| Unity EditMode, первый запуск на копии | 65,9 | 606 + 4 пропущено из 610 | 4,8 -> 7,3 |
| Unity PlayMode | 40,8 | 71 пройдено | 7,3 -> 5,5 |
| Unity EditMode, повторный | 60,8 | 605 + 1 упал + 4 пропущено | 5,5 -> 9,1 |
| **Сумма последовательно** | **233,6 - 238,7** | 836 + 92 + 149 + 610 + 71 | |

Разбивка Unity (до тестов и выход считаны по времени в отчёте теста и по `time`, с точностью до секунды):

| Запуск | До тестов, с | Тесты, с | Выход, с | `Csc` | `Project Loading` |
|---|---:|---:|---:|---:|---:|
| EditMode, первый на копии | 22 | 41,9 | 2 | 271 | 20,6 |
| EditMode, повторный | 10 | 41,7 | 10 | 0 | 9,2 |
| EditMode, вместе с backend | 11 | 41,5 | 4 | 0 | 9,8 |
| EditMode, комментарий в `client/core` | 13 | 40,9 | 7 | 1 | 12,3 |
| EditMode, комментарий откатан | 12 | 40,5 | 4 | 1 | 10,8 |
| PlayMode | 14 | 20,7 | 7 | 0 | 9,5 |
| Один тест, `-testFilter` (3 запуска) | 11 | 0,6 | 4 | 0 | - |

Пол одного запуска редактора по замеру: **15,8 с** при одном тесте. Полная компиляция добавляет около 11 с (20,6 против 9,2-9,5). Изменение публичного API не замерялось, комментарий пересобрал одну сборку.

Нестабильный тест: `WebRtcPeerSocketTests.Connect_NoAnswer_FailsInTimeout` упал в повторном EditMode за 0,009 с: `Unhandled log message: [Network] [libdatachannel] ... SCTP write: OpenSSL error ... protocol is shutdown`. Отдельно он прошёл 3 из 3, класс целиком 4 из 4, в остальных четырёх полных EditMode тоже прошёл. Итого 1 падение из 5 полных прогонов. Гипотеза: это лог закрытия нативного сокета предыдущего теста, он доходит до основного потока во время этого теста и считается его ошибкой. На таймаут теста падение за 0,009 с не похоже.

## Что тратит время

Замерено:

- **Backend почти всё время ждёт.** CPU процесса 12,7 с на 99,1 с (без учёта Postgres в контейнере). 16 тестов от 1 с занимают 79,4 с из 90,4 с суммы, остальные 820 тестов занимают 11 с.
- **Одна коллекция держит всё.** `OrleansIntegrationCollection`: 227 тестов, 79,0 с. Остальные 54 коллекции: 11,4 с.
- **Параллельность выключена** в `backend/Tests/xunit.runner.json`: `maxParallelThreads: 1`, `parallelizeAssembly: false`, `parallelizeTestCollections: false`.
  - `-parallelMode collections -maxThreads 24`: 84,5 с, падений нет (один прогон, это не доказывает отсутствие гонок на статическом состоянии).
  - `-parallelMode all -maxThreads 24`: 27,4 с, но 134 падения, из них 106 `deadlock detected`. `ResetDatabaseAsync` делает `TRUNCATE` перед каждым тестом в общей базе.
- **Postgres и Orleans стоят мало.** Контейнер стартует не на каждый класс, а 3 раза за прогон: `_refCount` падает до нуля между тремя коллекциями с фикстурой. Один старт фикстуры (контейнер и кластер) около 4,7 с (класс из 3 тестов: 4,8 с, сами тесты 0,11 с). Накладные расходы всего прогона 8,4 с (98,8 - 90,4).
- **Unity: два domain reload на старте**, 5,2-5,6 с и 1,7-2,1 с, из 9,2-9,5 с `Project Loading`. Плюс `Asset Pipeline Refresh` 2,8 с внутри загрузки.
- **PlayMode перезагружает домен:** `m_EnterPlayModeOptionsEnabled: 1`, но `m_EnterPlayModeOptions: 0`, это полная перезагрузка. В логе `Reloading assemblies for play mode`, 1,6 с, затем refresh 2,2 с.
- **`-noUpm` ничего не даст:** разрешение пакетов 0,11 с, регистрация 0,00 с.
- **Параллельный запуск не мешает:** backend и EditMode одновременно в одном дереве: 99,1 с и 55,9 с, падений нет, нагрузка 8-10.
- **Тесты Unity:** `Internal.Editor.Tests` 18-19 с на 222 теста, `Internal.Lobbies.Tests` 11 с на один тест, `Internal.Network.Tests` 8 с на 71, `Internal.Tests` 1 с на 205.

Гипотезы, не замерялось:

- `-nographics` (его используют игры, здесь прогон без него) может сократить старт. Для PlayMode не проверялся.
- 18-19 с `Internal.Editor.Tests` уходят на `AssetDatabase` и компиляцию в `GeneratedCodeCompiler`: 95 тестов EditMode от 0,1 с дают 38,9 с. Какая операция дороже, неизвестно.

## Самые медленные тесты: backend

Классы (время из `-xml`):

| Класс | Тестов | Время, с |
|---|---:|---:|
| `TransactionTests` | 20 | 17,80 |
| `RuntimeChannelCatchUpTests` | 9 | 12,60 |
| `RuntimeChannelObserverIsolationTests` | 3 | 12,04 |
| `TransactionTakeoverTests` | 1 | 8,12 |
| `RuntimePipeTests` | 10 | 7,45 |
| `SideEffectsWorkerTests` | 3 | 6,28 |
| `SessionKeepAliveTests` | 2 | 6,26 |
| `RuntimePipeTimeoutTests` | 2 | 5,02 |
| `RuntimePipeRetryTests` | 2 | 3,52 |
| `ServiceDiscoveryLivenessTests` | 4 | 1,53 |
| `TaskBalancerFailureTests` | 2 | 1,16 |
| `ConnectionCloseTests` | 2 | 1,08 |
| `MessagingKeepAliveTests` | 2 | 1,02 |
| `EventStateTests` | 16 | 0,71 |
| `DurableQueueTests` | 10 | 0,66 |
| `RuntimeChannelTests` | 12 | 0,58 |
| `RuntimeChannelResubscribeTests` | 2 | 0,52 |
| `TaskBalancerTests` | 5 | 0,39 |
| `ConnectionWriterTests` | 8 | 0,21 |
| `SessionsCollectionTests` | 1 | 0,21 |

Тесты, с источником ожидания в коде:

| Тест | Время, с | Что ждёт |
|---|---:|---|
| `TransactionTests.Transaction_Takeover_SecondTransactionSucceeds` | 15,02 | `IncrementWithDelay(15_000)`, `Delay(4000)`, опции фикстуры 2 с и 5 с |
| `RuntimeChannelCatchUpTests.CatchUp_AfterResubscribe_ReplaysFromLastSeen` | 12,51 | `Delay(12 с)`: `minInterval` 10 с зашит в `RuntimeChannelClient` |
| `TransactionTakeoverTests.Takeover_StalledTransactionResumes_NoLostUpdate` | 8,12 | `Delay(8 с)` |
| `RuntimeChannelObserverIsolationTests.AttachedObserver_KeepsChannelActivation_OnCollection` | 6,02 | `DeliveryTimeoutSeconds` + 1 с |
| `RuntimeChannelObserverIsolationTests.ChannelWithoutObservers_IsCollected` | 6,01 | то же |
| `SessionKeepAliveTests.ClientThatReads_StaysConnectedLongerThanTimeout` | 4,51 | `(Interval + Timeout) * 3` |
| `SideEffectsWorkerTests.LongRunningEffect_NotRequeuedAsStuck` | 4,01 | `Delay(4 с)` |
| `RuntimePipeTests.Send_HandlerTerminated_ThrowsException` | 3,71 | повторы `SendRetryBaseDelayMs` 500: 0,5 + 1 + 2 с |
| `RuntimePipeRetryTests.Send_NoHandler_AllRetriesExhausted_Throws` | 3,51 | то же |
| `RuntimePipeTests.Send_NoHandler_ThrowsException` | 3,51 | то же |
| `RuntimePipeTimeoutTests.Send_HandlerTimesOut_NotRetriedAndObserverKept` | 3,51 | `Delay(3 с)` в обработчике |
| `SideEffectsWorkerTests.Stop_WhileDisabled_LoopEnds` | 2,21 | `Delay(700)`, `Delay(1500)` |
| `TransactionTests.Transaction_StatePersistsAfterDeactivation` | 2,02 | `Delay(2000)` |
| `SessionKeepAliveTests.ClientThatStopsReading_IsDisconnectedByKeepAliveTimeout` | 1,76 | таймаут keep-alive |
| `ServiceDiscoveryLivenessTests.WaitClusterReady_MissingService_WarnsOnce` | 1,51 | `Delay(1,5 с)` |
| `RuntimePipeTimeoutTests.Exists_ObserverBusy_ObserverKept` | 1,51 | `Delay(1,5 с)` |
| `TaskBalancerFailureTests.LifetimeEnds_DrainCompletesWithoutError` | 0,66 | `Thread.Sleep`, `Delay` |
| `ConnectionCloseTests.Run_HandlerThrows_ConnectionEnds` | 0,56 | `Delay(500)` |
| `MessagingKeepAliveTests.RuntimePipe_IdleCollection_KeepsObserver` | 0,52 | `Delay(500)` |
| `ConnectionCloseTests.ForceDisconnect_ClientGetsCloseFrame` | 0,51 | `Delay(500)` |

`Framework.Common.Tests` целиком 0,2 с, ускорять нечего.

## Самые медленные тесты: Unity

Классы (первый EditMode и PlayMode, время из `-testResults`):

| Класс | Режим | Тестов | Время, с |
|---|---|---:|---:|
| `NetworkLatencyMonitorTests` | PlayMode | 15 | 15,83 |
| `MatchmakingTests` | EditMode | 1 | 11,03 |
| `HierarchyBindingsBatchTests` | EditMode | 35 | 5,13 |
| `AudioGroupDecodeTests` | PlayMode | 1 | 3,69 |
| `HierarchyBindingsNamingTests` | EditMode | 11 | 3,15 |
| `AsepriteAnimationBakerTests` | EditMode | 7 | 2,76 |
| `ConnectionWriterTests` | EditMode | 18 | 2,71 |
| `SpriteCatalogMetadataTests` | EditMode | 17 | 1,96 |
| `NetworkConnectionTests` | EditMode | 4 | 1,76 |
| `CatalogOldYamlTests` | EditMode | 5 | 1,58 |
| `CatalogMetadataTests` | EditMode | 10 | 1,52 |
| `EditorMenuTests` | EditMode | 5 | 1,23 |
| `DefaultWebSocketTests` | EditMode | 1 | 1,21 |
| `BackendRequestsTests` | EditMode | 3 | 1,09 |
| `AudioPlayerTests` | EditMode | 28 | 0,82 |
| `WebRtcPeerSocketTests` | EditMode | 4 | 0,65 |
| `SocketThreadingTests` | EditMode | 2 | 0,54 |
| `PrefabScreenshotTests` | EditMode | 4 | 0,54 |
| `ScenesCatalogTests` | EditMode | 6 | 0,48 |
| `ContainerBenchmarkTests` | EditMode | 6 | 0,42 |

Тесты:

| Тест | Режим | Время, с |
|---|---|---:|
| `MatchmakingTests.CreateLobby_SlowAnswerWithinWriterTimeout_IsTaken` (`SlowWriter` 11 с, реальное время) | EditMode | 11,03 |
| `NetworkLatencyMonitorTests.AfterTimeout_PingsAreLostProbesExpireAndSilentRowsAreMarked` | PlayMode | 6,07 |
| `AudioGroupDecodeTests.DecodeFails_Released_NoClipLeft` | PlayMode | 3,69 |
| `NetworkLatencyMonitorTests.Rates_OfTheTrafficOfTheLastSecond` | PlayMode | 2,05 |
| `NetworkLatencyMonitorTests.KnownDefect_FailedReportStopsTheProbes` | PlayMode | 1,23 |
| `DefaultWebSocketTests.Send_AfterServerClose_ClosedCarriesServerCode` | EditMode | 1,21 |
| `NetworkConnectionTests.DefaultWebSocket_SendAfterServerClose_IsDropped` | EditMode | 1,15 |
| `BackendRequestsTests.Get_SilentServer_FailsInOptionsTimeout` | EditMode | 1,06 |
| `NetworkLatencyMonitorTests.EveryHalfSecond_PingAndProbeGoOutWithOneRisingId` | PlayMode | 1,04 |
| `NetworkLatencyMonitorTests.UserLeaves_ItsRowGoesWithTheNextRound` | PlayMode | 1,03 |
| `ConnectionWriterTests.RunAgainWhileLostSendPending_NewSocketGetsOnlyItsMessages` | EditMode | 0,81 |
| `HierarchyBindingsNamingTests.PlainHierarchy_Compiles_BrokenCodeDoesNot` | EditMode | 0,80 |
| `NetworkLatencyMonitorTests.LifetimeEnds_ProbesStopAndOverlayIsDestroyed` | PlayMode | 0,78 |
| `NetworkLatencyMonitorTests.BeforeLocalUser_NothingIsSentAndReportWaits` | PlayMode | 0,73 |
| `ConnectionWriterTests.ForceSendAll_WhileLoopSends_SendsDoNotOverlap` | EditMode | 0,61 |
| `AsepriteAnimationBakerTests.DeleteStale_RemovesOnlyTheNotWritten` | EditMode | 0,57 |
| `AsepriteAnimationBakerTests.Bake_AnimationsOfTheFile_TagsThenCustom` | EditMode | 0,57 |
| `NetworkLatencyMonitorTests.Pong_SetsPingAndGoesToReport` | PlayMode | 0,53 |
| `NetworkLatencyMonitorTests.HostReport_RoutesOfGuestsAndNoPing` | PlayMode | 0,53 |
| `NetworkLatencyMonitorTests.ServerReport_RowsOfEveryUserThatAnswered` | PlayMode | 0,53 |

`NetworkLatencyMonitorTests` идут в реальном времени: константы `ProbeInterval` 0,5 с, `RateInterval` 1 с, `LogInterval` 5 с в `NetworkLatencyMonitor`.

## Рост Unity-тестов с 394 до 610

Рост объясняется целиком, тесты не задвоились и не подхватились из чужой сборки. Сверка по числу `[Test]`, `[UnityTest]`, `[TestCase]` между коммитами и по `-testResults`:

- 394 -> 395: `2395e8e` (шаг 15a `public_setters`).
- 395 -> 610, ровно +215 в EditMode: тесты поведения, которые `file_splits` писал перед разрезом файлов (P10), коммиты `613d219`, `79d6a8c`, `a65fb90`, `e313b1b`, `1ac27c7`, `0f0a16a`.

| Класс | Было -> стало |
|---|---|
| `ContainerBuilderTests` | 0 -> 36 |
| `GeneratedScopesTests` | 0 -> 28 |
| `AudioPlayerTests` | 2 -> 28 |
| `HierarchyBindingsScannerTests` | 0 -> 23 |
| `HierarchyBindingsBatchTests` | 14 -> 35 |
| `GamepadReaderTests` | 11 -> 30 |
| `SpriteCatalogMetadataTests` | 0 -> 17 |
| `ContainerGraphAssetScannerTests` | 0 -> 13 |
| `ContainerGraphReadTests`, `AsepriteDocumentTests` | 0 -> 10 каждый |
| `AsepriteAnimationBakerTests` | 0 -> 7 |
| `PrefabScreenshotTests` | 0 -> 4 |
| `ProfilerFrameCaptureTests` | 0 -> 1 |
| `WebRtcPeerSocketFailureTests` (`ab24d81`), `ContainerBenchmarkTests` (`5791401`) | +1 и -1 |

Ещё +59 попали в PlayMode и в число 610 не входят: `UINavigationTests` 44, `NetworkLatencyMonitorTests` 15. До этого в PlayMode было 12 тестов.

Что это объясняет про время: **EditMode стал медленнее не больше чем на 12 с из 42**. Десять новых классов дают 5,65 с, классы с ростом (`AudioPlayerTests`, `HierarchyBindingsBatchTests`, `GamepadReaderTests`) вместе 5,97 с, включая их старые тесты. Число тестов выросло в полтора раза, а время почти нет: `ContainerBuilderTests` (36) и `GeneratedScopesTests` (28) занимают 0,09 с и 0,05 с. В PlayMode заметнее: `NetworkLatencyMonitorTests` дал 15,83 с из 20,7. Самый медленный тест Unity (`MatchmakingTests`, 11 с) появился не от `file_splits`.

## Достижима ли минута

**Да, на этой машине и впритык: оценка конечного состояния 50-60 с, если этапы идут параллельно и ожидания в тестах сокращены.** Вынос из Unity и MCP для этого не нужны. Пол в 15,8 с на запуск редактора в минуту помещается.

Что мешает:

- **Узкое место после всех шагов: EditMode.** 11-13 с старта, около 31 с тестов и 4-10 с выхода дают 46-54 с. При полной компиляции ещё 11 с: 57-65 с. Запас почти нулевой, поэтому **при смене публичного API в `client/core` минута может быть превышена**.
- **Unity не открывает один проект дважды:** для параллельных EditMode и PlayMode нужна вторая копия дерева с `Library`.
- **Verify в Release перезаписывает `client/core/Analyzers/ContainerGenerator.dll`**, это отслеживаемый файл. В параллельном запуске он должен собираться так, чтобы Unity этого не увидел, иначе гипотеза: полная перекомпиляция.
- **Сокращение таймаутов под нагрузкой других сессий может породить новые нестабильные тесты,** это главный риск. Без устранения нестабильного `Connect_NoAnswer_FailsInTimeout` повторы съедят выигрыш.

## План

В порядке выигрыша. Время до и после: стена всего набора. Оценки считаны из замеров выше, сами шаги не проверялись, кроме отмеченных.

| № | Шаг | Выигрыш | Основание |
|---|---|---:|---|
| 1 | Единая команда, этапы параллельно: сборка -> backend; рядом EditMode, PlayMode (вторая копия), Verify | ~115 с: 234-239 -> ~120 | Замерен запуск backend с EditMode. PlayMode рядом не замерялся |
| 2 | Ожидания backend: опции транзакций в фикстуре (`LockWaitSeconds`, `StuckGraceSeconds`, они `float`) и задержки тестов, сделать их долями | ~20 с | `TransactionTests.Takeover`, `TransactionTakeoverTests`: 23,1 с |
| 3 | `SendRetryBaseDelayMs` 500 -> 50 в фикстуре; тесты, проверяющие именно задержку, оставить | ~12 с | 4 теста по 3,5 с |
| 4 | Интервал resubscribe из константы в опции `RuntimeChannelClient` | ~11 с | `CatchUp_AfterResubscribe` 12,5 с |
| 5 | `DeliveryTimeoutSeconds` 5 -> 1 в тестах изоляции наблюдателей | ~8 с | 12 с на два теста |
| 6 | `-parallelMode collections` для backend | ~15 с сейчас, ~10 с после шагов 2-5 | Замерено: 99,1 -> 84,5, падений нет. Нужны несколько прогонов и проверка статического состояния (`xunit.runner.json` запрещает это из-за него) |
| 7 | `MatchmakingTests`: таймаут без 11 с реального ожидания | ~10 с EditMode, это узкое место | 11,03 с |
| 8 | `NetworkLatencyMonitorTests`: интервалы и время подменяются в тесте | ~12 с PlayMode | 15,83 с. Вне узкого места: PlayMode уже не определяет стену |
| 9 | `SideEffectsWorkerTests`, `SessionKeepAliveTests`, задержки 1,5 с | ~8 с | 12,5 с суммы |
| 10 | Разбить `OrleansIntegrationCollection` на несколько коллекций, у каждой своя база; один контейнер на прогон | После шагов 2-5 останется ~19 с; гипотеза: половина | Без отдельных баз не работает: `-parallelMode all` даёт 106 deadlock. Старт фикстуры 4,7 с |
| 11 | Разобрать `Internal.Editor.Tests` (18-19 с): общая подготовка ассетов, меньше импортов | Гипотеза: 5-10 с, узкое место EditMode | Сначала замерить, что дороже |
| 12 | `m_EnterPlayModeOptions: 3` без перезагрузки домена и сцены | ~3-4 с PlayMode | 1,6 + 2,2 с по логу. Риск: статическое состояние тестов |
| 13 | `AudioGroupDecodeTests`: короче MP3 | ~3 с PlayMode | 3,69 с |

Обязательное условие перед шагами 2-13: разобрать нестабильный `Connect_NoAnswer_FailsInTimeout` (см. гипотезу в разделе База). Выигрыша во времени это не даёт, но без этого минутный прогон ненадёжен.

Расчёт конечного состояния: сборка 20 с (инкрементальная 2,5 с) и backend ~25 с дают ~45 с; EditMode 46-54 с; PlayMode ~25 с; Verify 12,6 с. Стена задаётся EditMode и составляет 50-60 с с учётом расхождений.

Варианты, которые рассматривались для Unity:

| Вариант | Что выигрывает | Вывод |
|---|---|---|
| Уже запущенный редактор через MCP | Убирает пол 15,8 с. Гипотеза: возвращает часть обратно, потому что перекомпиляция и domain reload всё равно идут (5,2-5,6 с и 1,7 с по логу старта). Не замерялось | Удобно для быстрой правки, для полного набора не нужно; общий редактор конфликтует с параллельными сессиями |
| Вынос логики из Unity в dotnet | Почти ничего: `Internal.Tests`, 205 тестов, занимают 1 с. Выигрыш только если уйдёт вся сборка, а это не снимает Unity | Не цель; делать только там, где это лучше для кода |
| Быстрый и полный наборы | Быстрый (`common` и backend без коллекций с кластером): 664 теста, 0,3 с + 10,3 с. Инкрементальная сборка 2,5 с | Делать дополнительно: обратная связь за 15 с после правки backend. Полный набор остаётся единственным критерием |

## Критерий готовности

1. Одна команда в фреймворке, например `tools/scripts/test-all.sh` (её надо создать): все этапы, таблица этап / время / число тестов, ненулевой код при любом падении.
2. `time tools/scripts/test-all.sh` на свежем worktree от `main` с копией `Library`: `real` не больше 60 с, 3 запуска подряд, нагрузка записана. Отдельно записать случай смены API в `client/core`; превышение до ~65 с в нём допустимо, если пользователь согласен.
3. Набор не уменьшен: backend не меньше 839 (836 в базе + 3 теста B1), `common` 92, Verify 148 (в базе 149 посчитана и строка `ALL PASSED`), EditMode 615 (4 пропущено; 610 в базе + 5 тестов U1), PlayMode 71. Удаляемый или пропускаемый тест называется явно.
4. Нет падений за 10 запусков подряд, включая запуск при нагрузке других сессий.
5. `git status` основного дерева чистый после запуска; `ContainerGenerator.dll` не изменён.

## Как замерялось

```bash
git worktree add --detach .claude/worktrees/test_speed_run main
cp -a --reflink=auto client/project/Library .claude/worktrees/test_speed_run/client/project/Library
time dotnet build framework.slnx                                              # после rm -rf .build, затем повторно
time .build/bin/Tests/Debug/net10.0/Framework.Tests -noColor -xml backend.xml
time .build/bin/Framework.Common.Tests/Debug/net10.0/Framework.Common.Tests -noColor -xml common.xml
time dotnet build client/core/Source~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj -c Release
time dotnet run --no-build --project client/core/Source~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj -c Release
git restore client/core/Analyzers/ContainerGenerator.dll
time /tools/unity/7000.0.0a10/Editor/Unity -batchmode -projectPath <worktree>/client/project -runTests \
    -testPlatform EditMode -testResults <файл> -logFile <файл>                 # затем PlayMode
```

Перед каждым замером записывается `uptime`. Запуск Unity создаёт в дереве `client/project/ProjectSettings/SceneTemplateSettings.json` без отслеживания.

## Решения оркестратора

1. **Три дорожки, у каждой свой worktree и ветка от `main`:** `test_speed_runner` (`tools/scripts`, общая команда), `test_speed_unity` (`client/`), `test_speed_backend` (`backend/`). Файлы дорожек не пересекаются, поэтому они идут параллельно. Внутри дорожки шаги последовательны, один шаг на коммит.
2. **Коммиты только в ветках дорожек.** Оркестратор собирает их в ветку `test_speed`. В `main` и origin ничего не попадает без просьбы пользователя.
3. **Ускорение не должно ослаблять тест.** Тест проверяет то же поведение, что и раньше: сокращается ожидание, а не проверка. Удаление или пропуск теста называется явно. Нельзя ускорять тест ценой хрупкости: таймаут, сокращённый так, что под нагрузкой тест падает, не засчитывается.
4. **Нестабильный тест чинится первым в дорожке Unity,** до сокращения ожиданий.
5. **Параллельность backend включается только после проверки статического состояния,** несколькими прогонами под нагрузкой.

## План выполнения

| Шаг | Дорожка | Что | Пункты плана выше |
|---|---|---|---|
| R1 | runner | `tools/scripts/test-all.sh`: этапы параллельно, вторая копия для PlayMode, Verify не трогает `ContainerGenerator.dll`, таблица времени | 1 |
| U1 | unity | `Connect_NoAnswer_FailsInTimeout`: причина и исправление | условие |
| U2 | unity | `MatchmakingTests`, `NetworkLatencyMonitorTests`, `AudioGroupDecodeTests` | 7, 8, 13 |
| U3 | unity | `Internal.Editor.Tests`, `m_EnterPlayModeOptions`, старт редактора (`-nographics`) | 11, 12 |
| B1 | backend | Ожидания: транзакции, повторы, resubscribe, delivery, side effects, keep-alive | 2, 3, 4, 5, 9 |
| B2 | backend | Параллельность: коллекции, отдельные базы, один контейнер | 6, 10 |
| F1 | все | Сборка в `test_speed`, критерий готовности: 3 прогона на время, 10 подряд | критерий |

Статус шагов: `test_speed_orchestration.md`.

## Решения пользователя

- **Быстрые варианты тестов остаются** (2026-11-02). `MatchmakingTests` не видит таймер, который не является задачей UniTask: принято, реальное ожидание 11 с не возвращается.
