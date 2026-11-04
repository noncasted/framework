# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Телеметрия: `IClientTelemetry.AddCollector`
Кого касается: клиент, игры с собственной реализацией `IClientTelemetry` (подделки в тестах).
Поиск: `git grep -nE ': IClientTelemetry|, IClientTelemetry' -- 'client/Assets/*.cs'`
Сделать: добавить `public void AddCollector(IReadOnlyLifetime lifetime, ITelemetryCollector collector) { }`. Коллекторы забега,
которые раньше требовали моста в скоуп meta, регистрировать так: `telemetry.AddCollector(runLifetime, collector)`. Новые
метрики кадра, рендера, настроек, rAF, WebGL и мета-сокета приходят сами с `AddStandardTelemetryCollectors` и `AddSocketClientTelemetry`.

### Телеметрия: страница до Unity и загрузки ассетов
Кого касается: клиент (WebGL-шаблоны), бэкенд (протокол), игры со своими `ITelemetryStartupSource` и загрузками Addressables в обход фреймворка.
Поиск: `git grep -nE 'createUnityInstance|loaderUrl|telegram-web-app.js|WebApp.ready|loading' -- 'client/Assets/WebGLTemplates/*/index.html' 'client/Assets/WebGLTemplates/*/TemplateData/*.js'`;
`git grep -nE ': ITelemetryStartupSource|new TelemetryStartupReport\(|TelemetryStartupTraces.Build\(' -- 'client/Assets/*.cs'`;
`git grep -nE 'Addressables\.(Load|Instantiate)' -- 'client/Assets/*.cs'`
Сделать:
- BREAKING (протокол): у `SharedClientTelemetry.Mark` поля `Detail`, `DurationMs`, у `StartupSpan` поля `File`, `TransferBytes`, `Cached`;
  хеш протокола меняется, клиент и бэкенд выкладываются вместе.
- Шаблон ничего не подключает сам: сборка WebGL кладёт `window.startupTrace` первым скриптом в `<head>` собранного `index.html`
  (`TelemetryStartupTraceInjection`, у страницы без `<head>` сборка падает). Шаблон только ставит спаны, имена без цифр:
  - в `<head>` вокруг блокирующего SDK платформы: `<script>startupTrace.begin('Telegram SDK')</script>` перед
    `telegram-web-app.js` и `<script>startupTrace.end('Telegram SDK')</script>` после него;
  - колесо загрузки: `startupTrace.begin('Loading wheel')` при показе, `startupTrace.end('Loading wheel')` при скрытии;
  - загрузчик: `startupTrace.begin('Unity loader')` перед `document.body.appendChild(script)`, `startupTrace.end('Unity loader')`
    в `script.onload`;
  - инстанс: `startupTrace.begin('Unity instance')` перед `createUnityInstance`, в колбэке прогресса по разу
    `startupTrace.mark('Progress quarter')`, `'Progress half'`, `'Progress three quarters'`, `'Progress done'`, в `.then`
    `startupTrace.end('Unity instance')`, в `.catch` `startupTrace.mark('Unity instance failed')`;
  - Telegram: `startupTrace.mark('Telegram ready')` рядом с `Telegram.WebApp.ready()`.
- Свой `ITelemetryStartupSource`: добавить `double SinceStartMs()` (время от начала трейса); `TelemetryStartupReport` принимает
  `TelemetryStartupPage`, `TelemetryStartupTraces.Build` - `TelemetryStartupPrefix` (`TelemetryStartupPrefix.None` вне веба).
- Загрузки групп каталогов и сцен скоупов отмечаются сами. Загрузку Addressables в обход них обернуть:
  `var load = AssetLoads.Begin("Group: <имя из кода>");` ... `load.Finish();`, при исключении `load.Fail(exception)`. Порог метки
  `load.slow`: `new ClientTelemetryOptions(slowLoadSeconds: 5f)`.
### Телеметрия: прошлая незакрытая сессия, предупреждения, ошибки WebGL
Кого касается: клиент и бэкенд игр с телеметрией (протокол телеметрии изменился: новое поле `SessionStart.Previous`, уровень
`ErrorLevel.Warning`), игры с собственной реализацией `ITelemetryPage` (подделки в тестах).
Поиск: `git grep -nE 'ITelemetryPage|ErrorLevel\.(Error|Exception)' -- '*.cs'`
Сделать: выкатывать клиент и бэкенд вместе (старый сервер не прочтёт `SessionStart` нового клиента; хеш протокола игры меняется
при пересборке). В подделку `ITelemetryPage` добавить `public IEventSource<TelemetryPageEvent> Lifecycle => _lifecycle;`
(`EventSource<TelemetryPageEvent>`). Свой `switch` по `SharedClientTelemetry.ErrorLevel` дополнить `Warning`. Новые метки и метрики
(`web.gl.errors`, `webgl.error.*`, `page.*`, `memory.texture_count`, `session.previous_unclosed`) и предупреждения лога Unity
приходят сами; на странице сессии консоли появляется блок прошлой незакрытой сессии.
### Шрифт панели задержки сети
Кого касается: клиент, игры с модулем `network` и сессиями (`AddSessionServices`).
Поиск: `git grep -nE 'NetworkOptions|_latencyOverlayFont' -- 'client/Assets/*.asset' 'client/Assets/*.cs'`
Сделать: BREAKING. В ассете `NetworkOptions` игры задать поле `Latency Overlay Font` (`TMP_FontAsset` игры, тот же, что в
`ClientOutdatedNoticeStyle`): без него `NetworkLatencyMonitor` бросает при сборке скоупа сессии. Предупреждение TMP «LiberationSans
SDF Font Asset was not found» в каждой сессии уходит. Свой код, который создаёт `NetworkLatencyMonitor` напрямую (тесты), передаёт
`NetworkOptions` последним аргументом.
### Телеметрия: вызовы WebGL, страница, сэмплы следа сессии, development-сборка
Кого касается: клиент и бэкенд игр с телеметрией (протокол: у `SharedClientTelemetry.PreviousSession` поля `SampleNames`, `Samples`),
игры с собственными `ITelemetryPage`, `ITelemetryGlErrors` (подделки в тестах), игры, которые сами создают `TelemetrySessionTrace`.
Поиск: `git grep -nE ': ITelemetryPage|: ITelemetryGlErrors|new TelemetrySessionTrace\(|new TelemetryPreviousSession\(' -- '*.cs'`
Сделать:
- BREAKING (протокол): клиент и бэкенд выкладываются вместе, хеш протокола игры меняется при пересборке.
- Подделка `ITelemetryPage`: `public IEventSource<TelemetryCanvasSize> Resized => _resized;` (`EventSource<TelemetryCanvasSize>`);
  подделка `ITelemetryGlErrors`: `public double LastTakeMs => 0d;`. `TelemetrySessionTrace` принимает третьим аргументом
  `TelemetryNameTable`, `TelemetryPreviousSession` - ещё `sampleNames` и `samples`.
- Приходят сами (WebGL): `web.gl.shader_compiles`, `web.gl.shader_compile_ms`, `web.gl.program_links`, `web.gl.program_link_ms`,
  `web.gl.buffer_bytes`, `web.gl.buffer_data_calls`, `web.gl.texture_bytes`, `web.gl.texture_uploads`, `web.gl.errors_ms`, метки
  `webgl.shader.slow` (порог `new ClientTelemetryOptions(slowShaderMs: 50d)`), `page.fullscreen.enter` / `exit`, `page.resize`,
  `audio.context.<состояние>`; в `SessionStart.Previous` - последние 5 сэмплов с главными числами кадра.
- По желанию, для development-сборки: `new ClientTelemetryOptions(devProfilerMarkers: new[] { "Shader.CreateGPUProgram",
  "Camera.Render", "<свой ProfilerMarker>" })` - время маркеров `dev.marker.*` и счётчики `dev.*`; в релизе их нет.

### Сеть: отправка без аллокаций (буферы и сегменты)
Кого касается: клиент, игры со своими реализациями `IWebSocket`, `IConnectionWriter`, `INetworkProperty` (обычно подделки в тестах).
Поиск: `git grep -nE ': IWebSocket|, IWebSocket|: IConnectionWriter|, IConnectionWriter|: INetworkProperty\b|, INetworkProperty\b' -- 'client/Assets/*.cs'`
Сделать: BREAKING (API, формат на проводе тот же):
- `IWebSocket.Send(byte[])` стал `Send(ArraySegment<byte> bytes)`: байты снова принадлежат вызывающему, когда задача завершилась,
  сокет, который держит их дольше, копирует (`bytes.ToArray()`); вызов с `byte[]` компилируется как есть;
- `IConnectionWriter` получил `void WriteOneWay(ReadOnlySpan<byte> message)` (байты `IMessageFromClient`, копируются сразу);
  подделке без него достаточно `=> throw new NotSupportedException()`;
- `INetworkProperty` получил `void Collect(IBufferWriter<byte> buffer)` (значение сериализуется в буфер);
- сообщение сериализуется в момент `WriteOneWay` / `WriteRequest` / `WriteResponse`, а не в цикле отправки: объект сообщения можно
  переиспользовать сразу после вызова;
- `ViewableDictionary.Values` теперь struct-коллекция (`foreach` без аллокаций); через `IReadOnlyDictionary` по-прежнему `IEnumerable`.

### UI: экранные канвасы фреймворка рисуются камерой UI игры (`IScreenCanvasCamera`)
Кого касается: клиент, игры с модулем `network` (сессии: оверлей задержки `NetworkLatencyMonitor`) или с `AddClientOutdatedNotice`
модуля `ui` (сейчас mines-leader, peace-data-reborn, pixel-swarm); подделки, которые создают `NetworkLatencyMonitor` или
`ClientOutdatedNotice` напрямую (тесты).
Поиск: `git grep -nE 'AddNetwork\(|AddClientOutdatedNotice\(|new NetworkLatencyMonitor\(|new ClientOutdatedNotice\(' -- 'client/Assets/*.cs'`
Сделать: BREAKING: оверлей задержки и окно устаревшего клиента больше не Screen Space - Overlay сами по себе (на iPadOS 18.2 Safari
проекция overlay-канваса движка теряет w примерно через 1100 кадров и UI пропадает): игра в корневом скоупе
(`InternalScopeExtensions.Construct`, рядом с `AddNetwork`) говорит, где их рисовать, иначе скоуп сессии или глобальный скоуп не
соберётся (нет `IScreenCanvasCamera`):
- игра, которая рисует свои экранные канвасы камерой UI: `builder.AddScreenCanvasCamera(<Func<Camera> камеры UI>, "<слой, который она
  видит>", <planeDistance между её клип-плоскостями>)`, например в pixel-swarm `builder.AddScreenCanvasCamera(UICamera.Get,
  UICamera.Layer, UICamera.PlaneDistance)`; камера спрашивается при каждом показе, канвас и всё под ним ставятся на этот слой,
  камера без слоя или плоскость вне клип-плоскостей бросают;
- игра, у которой UI в Screen Space - Overlay (как было): `builder.AddScreenCanvasOverlay();` - явный выбор, поведение прежнее;
- тесты: `new NetworkLatencyMonitor(..., options, screenCanvas)` и `new ClientOutdatedNotice(lifetime, loadingScreen, navigation,
  screenCanvas, style)`, где `screenCanvas` - `new ScreenCanvasOverlay()` или `new ScreenCanvasCamera(() => camera, "UI", 1f)`.

### Web: звук в беззвучном режиме iOS
Кого касается: клиент WebGL (все шаблоны, с пакетом `platforms/web`).
Поиск: `git grep -n 'audioSession' -- 'client/Assets/WebGLTemplates/*'`
Сделать: ничего, если не найдено. Сборка WebGL кладёт `navigator.audioSession.type = 'playback'` первым скриптом в `<head>`
собранного `index.html` (`WebAudioSessionInjection`), до `createUnityInstance`: на iOS / iPadOS звук слышен и в беззвучном
режиме. Своё выставление `audioSession` в шаблоне удалить.

### Хостинг: лидер лобби в `ILobbyMatches.Create`
Кого касается: бэкенд игр с лобби (своя реализация `ILobbyMatches`, подделки в тестах).
Поиск: `git grep -nE 'LobbyMatch Create\(' -- '*.cs'`
Сделать: BREAKING. Сигнатура `Create(Guid lobbySessionId, Guid leaderId, IReadOnlyList<Guid> userIds, bool isOpen)`: `leaderId` - мета-id
лидера лобби в момент старта (владелец сессии лобби, один из `userIds`). Игре, у которой матч хостит клиент (P2P), стоит отдать
роль владельца матча ему, а не первому вошедшему: иначе хостом становится тот, кто быстрее загрузился.
