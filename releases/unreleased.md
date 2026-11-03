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
