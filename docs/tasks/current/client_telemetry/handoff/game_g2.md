# game_g2: Pixel Swarm на фреймворке bba43a1f, релизные и dev-сборки (готово)

Маркер ORCH-client_telemetry-game-g2. Worktree `/projects/pixel-swarm/.claude/worktrees/client_telemetry`, ветка `client-telemetry`,
HEAD `b6d21d42`, дерево чистое. Ничего не запушено и не задеплоено, кластер не запускался, общее дерево не трогалось.

## 1. Скилл фреймворка: не менялся
`client-telemetry/report.py` `EXPECTED` содержит только имена фреймворка; комментарий в скилле прямо говорит, что значения игры в нём
не перечисляются. `game.*` нигде в скилле нет, `project-skills.md` игры имеет только url/env-file. Коммита во фреймворке нет.

## 2. Коммиты игры
- `b15c8796` [Framework]: submodule `a88498ba` -> `bba43a1f`.
- `b6d21d42` [Client] [Shared] [Tests]:
  - `NetworkOptions.asset`: `_latencyOverlayFont` = шрифт `GlobalUIOptions._outdatedNoticeFont` (guid `e2a67f96...`, тот же, что в
    `ClientOutdatedNoticeStyle` и `UIOptions._font`; адресуемый в `Shared_Common`, каталог `Internal` тоже адресуемый, дублирования нет).
  - `MetaScopeExtensions`: `ClientTelemetryOptions(devProfilerMarkers: ...)`: `Shader.CreateGPUProgram`, `GamePipeline.Render` (свой
    маркер пайплайна), `Camera.Render`, `Mesh.UploadMeshData`, `Gfx.UploadTextureData`, `PlayerEndOfFrame`. Имена проверены по строкам
    `WebGLSupport/BuildTools/lib/modules_development/*CoreModule*.a` Unity 7000.0.0a10. `Gfx.WaitForPresentOnGfxThread`,
    `Gfx.PresentFrame`, `Gfx.ProcessCommands` в плеере WebGL отсутствуют (нет графического потока). `Camera.Render` есть в плеере, но с
    собственным SRP может не прийти - тогда метрики просто нет.
  - Goldens (`UPDATE_PROTOCOL_GOLDEN=1`): в схеме только `PreviousSession` + `SampleNames`, `Samples`; `ProtocolHash.Value`
    `65cd3fcd58328aa7` -> `1b8ac62a9790766c`.
- Остальное из unreleased: подделок `ITelemetryPage` / `ITelemetryGlErrors`, `new TelemetrySessionTrace(`, `new TelemetryPreviousSession(`,
  `new NetworkLatencyMonitor(` в игре нет (grep пустой).

## 3. main
`main` уже предок ветки, слияние не понадобилось.

## 4. Тесты
- Бэкенд: сборка 0 ошибок, 567/567.
- EditMode (`UNITY_SLOTS=1 run-tests.sh --unity-only`): 925, 875 passed, 0 failed, 50 skipped.

## 5. Релизные сборки (`scratchpad/build-webgl.sh`, под flock; telegram, затем website)
| | guid | байт | telegram-web-app.js | первый в `<head>` |
|-|-|-|-|-|
| `builds/release-telegram` (= `builds/telegram`) | cbaec564... | 13 551 182 | 1 (telegram-shell.js 2) | `<script data-startup-trace>` |
| `builds/release-web` (= `builds/website`) | d2029160... | 13 552 608 | 0 | `<script data-startup-trace>` |

Обе: стриппинг прошёл (новые папки в `Library/com.unity.web.stripping-tool`), в каталоге нет StandaloneLinux64, дубликатов бандлов нет,
`cmp` web-shell.js/style.css OK, в framework.js есть `telemetryGlCallsState`. Bee снова удалил `builds/telegram/Build` при сборке website:
восстановлен из копии. Активный профиль - website.

Смоук website (headless Chromium, 60 с): нет ReferenceError и исключений, `Progress done` 699 мс, меню нарисовано (офлайн).
Мета отвечает 403 на новый хеш `1b8ac62a9790766c` (прод на старом протоколе) -> офлайн, телеметрия не уходит, поэтому
`web.gl.shader_compiles` смоук не видит. Увидеть их можно только после деплоя клиента и бэкенда вместе.

## 6. Dev-сборка `builds/dev-website` (не коммитится)
Временный драйвер `client/Assets/Common/Internal/Editor/TmpDevWebGlBuild.cs` (удалён): `PlayerBuildSteps.Run` + `BuildPipeline.BuildPlayer`
с `BuildOptions.Development | BuildOptions.ConnectWithProfiler`, `-activeBuildProfile website` (профиль и настройки проекта не менялись,
`EditorUserBuildSettings.connectProfiler` не трогался, deep profiling выключен). guid `e5479c8d...`, 33 050 279 байт, файлы без сжатия
(wasm 19.1 МБ). Сборка Success, но процесс вышел с кодом 1: `WebBootConfigStep` игры ищет `*.data.br` и не нашёл - поэтому в boot.config
dev-сборки нет `memorysetup-main-allocator-mimalloc-enabled=0` (куча не как в релизе).

boot.config: `player-connection-mode=Connect`, `player-connection-ip=192.168.0.109` (первый), затем 10.8.1.1, 172.17/18/19.0.1 -
все IPv4 машины сборки; задать адрес вручную Unity не даёт. `profiler-enable=1`.

### Как подключается профайлер WebGL (Unity 7000)
- Плеер подключается сам при старте: WebSocket `ws://192.168.0.109:35020/`, повторы 10 с (смоук: «Successfully connected to host after 0
  seconds», handshake 101).
- На стороне редактора порт 35020 держит websockify (node из Emscripten Unity), проксирует в `localhost:<ProfilerDriver.directConnectionPort>`
  (34999). Запускается окном Profiler -> выпадающий список цели -> `<Enable Web Profiling>` (окно показывает адреса `IP:35020`).
  Окно нужно открыть до загрузки страницы (10 с повторов); вручную позже - в консоли браузера `unityInstance.ConnectToProfiler('192.168.0.109:35020')`
  (`window.unityInstance` задаёт шаблон игры; кнопки Profile из `profiler.js` Unity в шаблоне нет).
- Сейчас websockify уже работает у открытого редактора пользователя `/projects/pixel-swarm/client` (pid 4441), и мой смоук на 60 с к нему
  подключался. Внимание: на 34999 слушает и batchmode-копия `/var/tmp/unity-copies/pixel-swarm/balance` (pid 1071842); прокси идёт на
  localhost:34999 и может попасть не в тот редактор - копию лучше закрыть на время профилирования.
- С телефона: раздать `builds/dev-website` статикой на 192.168.0.109 (файлы без сжатия, подойдёт любой сервер), открыть порт 35020 в
  файрволе.

Логи и скрипты: `/tmp/claude-1000/-projects-framework/335627c0-e338-455f-b883-14b9b709f1a2/scratchpad/` (`webgl/telegram.log`,
`webgl/website.log`, `webgl/dev-website.log`, `smoke-g2.txt`, `smoke-dev.txt`, `smoke.png`, `editmode-g2.log`).
