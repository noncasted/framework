# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Телеметрия: `IClientTelemetry.AddCollector`
Кого касается: клиент, игры с собственной реализацией `IClientTelemetry` (подделки в тестах).
Поиск: `git grep -nE ': IClientTelemetry|, IClientTelemetry' -- 'client/Assets/*.cs'`
Сделать: добавить `public void AddCollector(IReadOnlyLifetime lifetime, ITelemetryCollector collector) { }`. Коллекторы забега,
которые раньше требовали моста в скоуп meta, регистрировать так: `telemetry.AddCollector(runLifetime, collector)`. Новые
метрики кадра, рендера, настроек, rAF, WebGL и мета-сокета приходят сами с `AddStandardTelemetryCollectors` и `AddSocketClientTelemetry`.
