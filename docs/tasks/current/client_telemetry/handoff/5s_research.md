## Шаг 5s: разведка (выводы из кода, WebGL-сборка не запускалась)

### Решения оркестратора
- Отправка: снимок `Build` при коннекте к Meta и полный трейс по `ProfilerTraceStorage.Saved`; полный заменяет снимок. Дельты нет: это проще и её нельзя получить (нет курсора, id перенумеровываются при каждом `Build`).
- Аллокаций в player нет (`ContainerDiagnostics.cs:44-50`, icall не реализован в WebGL): колонка показывается только при ненулевых данных, другой источник не ищем. Пользователю сообщено.
- Сервер ставит время приёма; локальное `StartedAt` без пояса не отправляем.
- Имена спанов проходят `TelemetryScrubber` и серверный `Scrubber`. Проверить, покрывают ли они URL из `BackendGet/Post/Media` (`GET {request.Uri}`).

### Основа
- `GameProfiler.Stage` и `CoverChildren` есть только в незакоммиченных правках `/projects/pixel-swarm/tools/framework` (база 4aa4df2): `GameProfiler.cs`, `Profiler.cs`, `ProfilerScope.cs`. Игра уже вызывает `Stage` (`client/Assets/Meta/MetaScopeExtensions.cs:14`). В ветке телеметрии их нет. Перед 5s проверить, куда они попали, чужие правки не забирать и не менять. В версии игры `Build` меняет концы закрытых родителей, снимок может измениться задним числом.

### Сбор
- `client/core/Runtime/Tools/Profiling/GameProfiler.cs:25-115`, `Profiler.cs:65-124`, данные `ProfilerTraceData.cs:13-60`. В player работает, `#if` нет.
- `GameProfiler.Current` имеет тип `IProfiler` без `Build`; публичный `Profiler.Build()` только через приведение, нужен API снимка.
- `Build` на открытом трейсе: незакрытые спаны с текущей длительностью, `Unfinished=false`, `EndFrame=-1`. Для снимка нужен свой признак.
- Трейс `Startup` открыт от `GameStartup.cs:24` игры до первого показа забега (`GamePlayLoader.cs:68`), новый `GamePlay` открывается на следующем забеге. Без забега трейс не закрывается. После `Finish` вызовы пустые.
- Окно редактора: `client/core/Editor/Tools/Profiling/ProfilerTraceWindow.cs:154`, `ProfilerTraceRows.cs:66-71`, `ProfilerTraceRow.cs:123-155` (аллокации от 64 КБ).

### Формат для отправки
- Трейс: Name, DurationMs, Frames, признак снимка или финала, id записи запуска.
- Спан: ParentId, Name, StartMs, DurationMs, Frames, Unfinished; AllocBytes, если не ноль. Id заменяется порядком, Depth считается из ParentId.
- Не отправлять: StartNs, EndNs (0 без `ENABLE_PROFILER`), StartFrame, EndFrame, `frames_*.json`.

### Объёмы и примеры
- Настоящие трейсы: `/projects/pixel-swarm/client/traces/` (21 файл, 18-88 КБ), самый полный `trace_20261103_035153_699.json`.
- Запуск до забега: около 365 спанов, 88 КБ JSON; компактный массив 16.8 КБ. Глубина в окне до 6. Имя до 44 символов.
- К коннекту готово 110 из 365 спанов (около 1180 мс из 54337).
- В длительность входит простой игрока (Menu 31 с, Lobby 19 с): окно должно отличать его от загрузки.
- Приватность: скан 247 имён не нашёл цифр от 3 подряд, `://`, `?`, `=`; второй шаблон просмотрен неполностью. Риск: URL целиком в `BackendGet.cs:25`, `BackendPost.cs:25`, `BackendMedia.cs:25,40`; `Request:` через `ErrorResponse.Describe` не прочитан.
