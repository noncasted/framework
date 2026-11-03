# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Телеметрия: `IClientTelemetry.AddCollector`
Кого касается: клиент, игры с собственной реализацией `IClientTelemetry` (подделки в тестах).
Поиск: `git grep -nE ': IClientTelemetry|, IClientTelemetry' -- 'client/Assets/*.cs'`
Сделать: добавить `public void AddCollector(IReadOnlyLifetime lifetime, ITelemetryCollector collector) { }`. Коллекторы забега,
которые раньше требовали моста в скоуп meta, регистрировать так: `telemetry.AddCollector(runLifetime, collector)`. Новые
метрики кадра, рендера, настроек, rAF, WebGL и мета-сокета приходят сами с `AddStandardTelemetryCollectors` и `AddSocketClientTelemetry`.

### Телеметрия: прошлая незакрытая сессия, предупреждения, ошибки WebGL
Кого касается: клиент и бэкенд игр с телеметрией (протокол телеметрии изменился: новое поле `SessionStart.Previous`, уровень
`ErrorLevel.Warning`), игры с собственной реализацией `ITelemetryPage` (подделки в тестах).
Поиск: `git grep -nE 'ITelemetryPage|ErrorLevel\.(Error|Exception)' -- '*.cs'`
Сделать: выкатывать клиент и бэкенд вместе (старый сервер не прочтёт `SessionStart` нового клиента; хеш протокола игры меняется
при пересборке). В подделку `ITelemetryPage` добавить `public IEventSource<TelemetryPageEvent> Lifecycle => _lifecycle;`
(`EventSource<TelemetryPageEvent>`). Свой `switch` по `SharedClientTelemetry.ErrorLevel` дополнить `Warning`. Новые метки и метрики
(`web.gl.errors`, `webgl.error.*`, `page.*`, `memory.texture_count`, `session.previous_unclosed`) и предупреждения лога Unity
приходят сами; на странице сессии консоли появляется блок прошлой незакрытой сессии.
