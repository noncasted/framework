# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Сеть: оверлей пинга в отдельной сборке `Internal.Network.Overlay` (BREAKING)

Кого касается: клиент игр с сессиями (`AddSessionServices`): pixel-swarm, gamejam-autobattler, mines-leader. `Internal.Network` больше не
ссылается на TMP / uGUI: отладочный оверлей `NetworkLatencyOverlay` (N, тап тремя пальцами) переехал в сборку `Internal.Network.Overlay`
(`client/modules/network/Overlay`, компилируется только с пакетом `com.unity.ugui` 2.x, `FRAMEWORK_TMP`) и создаётся, только если игра его
подключила. Шрифт ушёл из `NetworkOptions` (`LatencyOverlayFont` удалён) в новый ассет `NetworkOverlayOptions`.
Поиск: `git grep -nE "AddSessionServices\(|LatencyOverlayFont|new NetworkLatencyMonitor\(" -- client/Assets` и
`git grep -n "_latencyOverlayFont" -- client/Assets` (ассет `NetworkOptions`).
Сделать:
- Создать ассет `NetworkOverlayOptions` рядом с ассетом `NetworkOptions` игры (так же доступный скоупу сессии) и задать в нём `Font` -
  тот `TMP_FontAsset`, что был в `Latency Overlay Font` ассета `NetworkOptions`.
- В скоупе сессии, рядом с `AddSessionServices()`: `builder.AddNetworkLatencyOverlay(overlayOptions)`; asmdef этого скоупа ссылается на
  `Internal.Network.Overlay` (`GUID:99f35bbb8b15421ba39cd5252789051f`). Без вызова оверлея нет, без шрифта
  `NetworkLatencyOverlaySetup` бросает при сборке скоупа.
- Код, который создаёт `NetworkLatencyMonitor` сам (тесты): конструктор без `NetworkOptions` и `IScreenCanvasCamera` -
  `new NetworkLatencyMonitor(connection, session, peerMatch, clock)`; оверлей там больше не появляется, его жизнь -
  `INetworkLatencyMonitor.IsMeasuring` (`IViewableProperty<bool>`: true от первого раунда проб до конца lifetime). Своя подмена
  `INetworkLatencyMonitor` реализует `IsMeasuring`.
- Поле `_latencyOverlayFont` в ассете `NetworkOptions` Unity больше не читает: убрать его при пересохранении ассета.
