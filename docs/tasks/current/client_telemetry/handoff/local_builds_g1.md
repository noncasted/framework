# local_builds_g1: Pixel Swarm, сборки для теста с телефона по http (готово, с оговоркой по dev)

Маркер ORCH-client_telemetry-local-builds-g1. Worktree `/projects/pixel-swarm/.claude/worktrees/client_telemetry`, ветка `client-telemetry`,
HEAD `89a9bfd9`, `git status` в конце чистый. Ничего не закоммичено, не запушено, кластер не запускался, общее дерево не трогалось,
порт 8770 не занимался.

## Что запрещало и что разрешено
- `ERR_CONTENT_DECODING_FAILED`: Chromium не декодирует `Content-Encoding: br` на http (не secure context) - проверено на `release-web`
  по `http://192.168.0.109:8781/`: `framework.js.br` и `data.br` не грузятся. Поэтому сборки **без сжатия** (`WebGLCompressionFormat.Disabled`,
  `decompressionFallback = false`): `serve.py` отдаёт их как есть, `.br` в них нет.
- «Insecure connection not allowed» / «Non-secure network connections disabled»: `PlayerSettings.insecureHttpOption` (Unity 7000 публичный,
  `NotAllowed` в ProjectSettings и в переопределении игрока профиля `website.asset`). Есть ещё `allowedHttpConnections` (`HttpAllowed`), но он
  `internal`; хватило `insecureHttpOption = AlwaysAllowed`. Сеттер `PlayerSettings` при активном профиле пишет в его переопределение (лог:
  `profile=website before: http=NotAllowed compression=Brotli` -> `set: http=AlwaysAllowed compression=Disabled`).
- Свои проверки схемы в игре/фреймворке: `BackendOptions` (http->ws / https->wss, мета https -> wss), `BundleUrlVersioning` (`StartsWith("http")`)
  - не мешают. В шаблонах и jslib нет `isSecureContext` / `location.protocol` / `randomUUID`; `navigator.clipboard` (WebClipboard) на http
  не будет работать, Cache API тоже (`[UnityCache] Cache API is not supported` - только лог, файлы грузятся из сети).

## Как собрано
Временный драйвер `client/Assets/Common/Internal/Editor/TmpLocalWebGlBuild.cs` (удалён вместе с `.meta`): запоминает три настройки, ставит
http/сжатие, `PlayerBuildSteps.Run` + `SharedAddressablesSync.Sync` + `BuildPipeline.BuildPlayer` (`None` / `Development | ConnectWithProfiler`
по `-devBuild`), в `finally` восстанавливает (`restored: http=NotAllowed compression=Brotli fallback=False`), `EditorApplication.Exit(code)`.
Скрипт `/tmp/claude-1000/lb/build.sh` (под `flock`, `-activeBuildProfile .../website.asset`). Обе сборки exit=0 (111 и 76 с).

`WebBootConfigStep` для несжатой сборки: временно исправлен (ищет `*.data.br` или `*.data`; несжатый файл переписывает напрямую без brotli)
и откачен после сборки. Патч: `/tmp/claude-1000/lb/WebBootConfigStep-plain.patch` (+10/-5, просто) - можно закоммитить в игру, тогда dev/несжатые
сборки перестанут выходить с кодом 1. В обеих сборках в boot.config есть `memorysetup-main-allocator-mimalloc-enabled=0` (как в релизе).

## Сборки
| | байт | Build/ | boot.config |
|-|-|-|-|
| `builds/local-web` | 25 965 251 | wasm 12.5 МБ, data 4.55 МБ, framework.js 368 КБ | guid `0287a91f...`, строки mimalloc нет (см. ниже) |
| `builds/local-web-nomimalloc` | 25 965 297 | та же сборка | то же + `mimalloc-enabled=0` |
| `builds/local-dev` | 33 050 822 | wasm 19.1 МБ, data 4.55 МБ, framework.js 785 КБ | guid `deaac3b3...`, `player-connection-mode=Connect`, ip 192.168.0.109 первым, `profiler-enable=1`, mimalloc=0 |

### Два варианта local-web (дополнение: гипотеза про mimalloc на iPhone 11)
После сборки `builds/local-web` скопирован в `builds/local-web-nomimalloc`; из `local-web` строка убрана скриптом
`/tmp/claude-1000/lb/bootline.py <file.data> show|remove|add` (тот же формат UnityWebData1.0, заголовок пересчитан; убранная строка - последняя,
так что boot.config побайтно как у Unity без шага). Проверено чтением boot.config:
- `builds/local-web` (mimalloc **включён**): строки `memorysetup-main-allocator-mimalloc-enabled=0` **нет**; data 4 552 346 байт, всего 25 965 251.
- `builds/local-web-nomimalloc` (mimalloc **выключен**, как в релизе): строка **есть**; data 4 552 392 байт, всего 25 965 297.
Остальное в boot.config одинаково (`build-guid=0287a91f...`). Смоук обоих (40 с, `http://192.168.0.109:8782/`): исключений нет, `Progress done`
692 / 709 мс, меню нарисовано, remote-log по 230 строк. `local-dev` не менялся: строка в нём есть (mimalloc выключен).
Внимание: если коммитить `WebBootConfigStep-plain.patch`, несжатые сборки станут без mimalloc.

StreamingAssets (каталог + бандлы) 8 499 510 байт, как у `release-web`. В `index.html` обеих: `<head>` -> `<script data-startup-trace>` ->
`<script src="/remote-log.js"></script>`; `remote-log.js` скопирован в корень обеих.

Активный профиль - website. `builds/telegram` не пострадал (сверен с копией `/tmp/claude-1000/lb/telegram-backup`, совпадает).
`release-web`, `release-telegram` не трогались.

## Проверка в headless Chromium (`http://192.168.0.109:8782/`, `serve.py` с портом аргументом, 60 с)
- `local-web`: каталог и все бандлы Addressables грузятся по http, `Progress done` 694 мс, ни Insecure, ни InvalidKeyException, исключений нет;
  мета `wss://meta.pixel-swarm.xyz` -> 403 (прод на старом протоколе) -> офлайн; меню нарисовано (Play / Join / Settings,
  `/tmp/claude-1000/lb/local-web.png`). remote-log получил 240 строк (`/tmp/claude-1000/lb/log-local-web.txt`).
- `local-dev`: каталог и бандлы грузятся, `Progress done` 740 мс, профайлер подключился (`ws://192.168.0.109:35020/`, 101, «Successfully
  connected to host»), игра идёт до меты (те же логи `[Network]`/`[Meta]`, что у web). **Но экран чёрный** (только надпись Development Build,
  `/tmp/claude-1000/lb/local-dev.png`) и ~120 NullReferenceException в секунду (каждый кадр, 7 246 за 60 с) со стеком из одного кадра
  `UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException` - нативный биндинг с null. Это не из-за http: в смоуке `dev-website` из
  game_g2 (127.0.0.1, brotli) то же самое - 7 276 NRE и такой же чёрный `smoke.png`, в game_g2 это не было отмечено. Причину не искал
  (вне задачи); кандидат - что-то только dev в рендере (`GamePipeline`: `ProfilerMarker GamePipeline.Render`, `_cmd.BeginSample`) или
  сборщики телеметрии `#if DEVELOPMENT_BUILD` (`StandardTelemetryCollectorsExtensions.cs:81`). Для профилирования с телефона dev-сборка
  сейчас бесполезна визуально, а NRE каждый кадр искажают замер. Внимание: remote-log шлёт каждый NRE POST'ом (14 740 строк за 60 с).

## Файлы
`/tmp/claude-1000/lb/`: `build.sh`, `build.out`, `local-web.log`, `local-dev.log` (логи Unity), `smoke.mjs` (CDP, внешний url),
`serve.py` (копия `lan/serve.py` с аргументами `<dir> <port> <log>`), `smoke-local-*.txt`, `log-local-*.txt`, `long.txt` (полные тексты NRE),
`base.txt` (смоук release-web по http: br отклонён), `WebBootConfigStep-plain.patch`, `telegram-backup/`.
