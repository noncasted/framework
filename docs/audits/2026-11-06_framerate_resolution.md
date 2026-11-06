# Аудит кода: framerate_resolution (2026-11-06)

Область: `client/modules/framerate` (17 `.cs`), `client/modules/resolution` (12 `.cs`, 1 `.jslib`, 1 `.js` в `WebTemplate~`).
Проверено правил: архитектура (P1–P14), стиль (`CODE_STYLE_FULL`), структура (P11–P14), баги; по фокусу —
`COMMON_LIFETIMES.md`, `COMMON_REACTIVE_BASICS.md`, `WEBGL_TROUBLESHOOTING.md`, `ARCHITECTURE_PATTERNS.md`.

## Итог

Пакетов: 2 (`FRT` — `client/modules/framerate`, `RES` — `client/modules/resolution`). Разведчиков: 2 (`scout-FRT.md`,
`scout-RES.md`). Находок: 6. Проверяющих: 6, по одной на находку (`verify-*.md` в `.work/`).

Подтверждено 4: важных 1, мелких 3. Ложных 2. Не доказанных 0. Критичных нет.

Исправлять первым:
1. **`BuiltPageScripts.Inject` ставит `render-resolution.js` перед `<meta charset>`** (RES-1): объявление кодировки
   уходит за 1024 байта прескана, страница на сервере без заголовка charset декодируется как windows-1252 — любой
   не-ASCII текст шаблона (заголовок вкладки, экран загрузки) превращается в кракозябры. Корневая причина — в
   `client/core/Editor/Tools/Build/BuiltPageScripts.cs`, общая с `telemetry` и `platforms/web`.
2. **`AutoFrameRateOptions.Validate` недостижим вне Auto на мобильном** (FRT-2): перепутанные `_low`/`_high` молча
   применяются наоборот на всех платформах, бросая только на телефоне в Auto.
3. **Заголовок `render-resolution.js` описывает границу `maxRatio` в обратную сторону** (RES-2): автор игры по
   комментарию выставит тег и получит рендер меньше CSS-размера.

## Важные

### RES-1. Вставленный скрипт оказывается перед `<meta charset>`, собранная страница теряет кодировку
- Где: `client/modules/resolution/Editor/RenderResolutionInjection.cs:16-19`, `client/core/Editor/Tools/Build/BuiltPageScripts.cs:18,38-40`
- Правило: баг
- Проблема: `BuiltPageScripts.Inject` находит только открывающий тег `<head(...)>` и вставляет скрипт по
  `head.Index + head.Length`, то есть перед всем, что шаблон положил в head, включая `<meta charset="utf-8">`.
  `render-resolution.js` — 6257 байт, поэтому уже он один выносит объявление кодировки за окно прескана в 1024 байта;
  вместе с `startup-trace.js` (2383 байта) в реальной сборке `/var/tmp/ps-wpt23/builds/website/index.html`
  `<meta charset>` стоит на байте 9217 (строка 238). BOM нет (`d[:3] == b'<!D'`), а nginx деплоя
  (`client/deploy/nginx.conf` игр) директивы `charset` не задаёт и для `index.html` добавляет только `Cache-Control`,
  так что страница уходит как `text/html` без кодировки. Баг латентный только потому, что шаблоны pixel-swarm пока
  чистый ASCII; любой кириллический заголовок или текст загрузки декодируется по локали (windows-1252/1251).
- Как исправить: вставлять после последнего `<meta>` перед первым `<script`/`<link` (или явно после charset-meta),
  с откатом на «сразу после `<head>`», если charset-meta нет; в `RenderResolutionScriptTests` добавить `<meta charset>`
  в фикстуру и проверять и «перед `web-shell.js`», и «charset в первых 1024 байтах». Тест самого `BuiltPageScripts`
  был бы лучше: тем же путём идут `telemetry` и `platforms/web`.
- Проверка: run (побайтовый разбор реальной сборки), `d.lower().find(b'<meta charset') == 9217`, `d[:3] == b'<!D'`;
  корневая причина — в `BuiltPageScripts`, смещение уже было до скрипта resolution (сборка `website-before` — байт 2918
  от одного `startup-trace.js`).

## Мелкие

### FRT-2. `AutoFrameRateOptions.Validate` достижим только через правило Auto
- Где: `client/modules/framerate/Runtime/Auto/AutoFrameRateOptions.cs:36-49`,
  `client/modules/framerate/Runtime/Auto/AutoFrameRateRule.cs:10-13`, `client/modules/framerate/Runtime/FrameRateLimit.cs:35-71`
- Правило: P8 (fail fast)
- Проблема: единственная проверка чисел опций (`_low >= _high`, `_riseWorkMs >= _dropWorkMs`, `_windowSeconds <= 0`)
  вызывается только из конструктора `AutoFrameRateRule`, а правило создаётся в `AutoFrameRate.Run` — только в ветке
  Auto на мобильном. Ветки фиксированных лимитов и `?fps=` берут `_options.Low`/`_options.High` напрямую и ничего не
  проверяют; `AddFrameRate` только регистрирует экземпляр (`[Min(1)]` — подсказка сериализации, не проверка).
  Ассет с `Low=60, High=30` молча применяет 60 на «Low» и 30 на «High» на десктопе, в редакторе и для явного выбора,
  и бросает только когда на телефоне включат Auto.
- Как исправить: `options.Validate()` в `AddFrameRate` сразу после `RegisterInstance` (падает при сборке корневого
  scope) либо в начале `FrameRateLimit.OnSetupCompletion` до проверки `?fps=`; проверку в правиле оставить для прямого
  конструирования в тестах.
- Проверка: reading, ровно один вызов `Validate` — в конструкторе правила; ни модуль, ни `client/core`/`client/flow`
  ассет не валидируют.

### FRT-3. Публичная константа стоит между конструктором и приватными полями
- Где: `client/modules/framerate/Runtime/FrameRateLimit.cs:14-33`
- Правило: `CODE_STYLE_FULL` § Member Order
- Проблема: порядок в документе — конструктор, приватные поля, публичные члены; в файле после конструктора (14-22)
  идёт `public const int Native = -1;` (25), затем четыре `private readonly` поля (27-31), и только потом первый
  публичный член `OnSetupCompletion` (33). Написанного исключения для констант нет; де-факто то же место занято в
  `network` (`LatencyPeerStats.cs:14`, `LatencyProbes.cs:17`, `NetworkConnectionWatchdog.cs:22`,
  `LocalSessionServer.cs:157`) — практика, а не правило, но исправление одного файла оставит конвенцию раздвоенной.
- Как исправить: перенести константу с комментарием (строка 24) к публичным членам, прямо над `OnSetupCompletion`.
- Проверка: reading, номера строк находки были сдвинуты на 4 (константа на 25, не 21), структурное утверждение верно.

### RES-2. Ограничение `maxRatio` в заголовке `render-resolution.js` описано наоборот
- Где: `client/modules/resolution/WebTemplate~/render-resolution.js:9-10` против `:95-97`, `:84-85`, `:18-19`
- Правило: `CODE_STYLE_FULL` § Comments (комментарий утверждает ограничение, которого код не даёт)
- Проблема: `pixelRatio` берёт `limited = min(ceil(device/maxRatio - 1e-6), floor(visibleSide/360))` и возвращает
  `device / max(1, limited)` (:95, :97), то есть k идёт **от** `device/maxRatio` вверх и отношение остаётся **не выше**
  `maxRatio`. Заголовок же говорит «low takes k up to device / maxRatio» — верхнюю границу на k, то есть отношение
  **не ниже** `maxRatio`. Его же пример «maxRatio 1 there: 820x360» (отношение 0.9167) и закреплённые ожидания тестов
  (`RenderResolutionScriptTests.cs:33` — `2.75/3`, `:51` — `1.375 > maxRatio 1`) описывают код, а не фразу. Вторая
  половина заголовка («наибольшее k, оставляющее короткую сторону ≥ 360») тоже неверна, когда связывает ограничение
  отношения: 800 CSS при device 2 и maxRatio 1 даёт k 2, а не 4. Сентенция про `?dpr` говорит «заменяет maxRatio у
  low», тогда как числовой `?dpr` применяется до `else if (isHigh())` (:84-85) и ограничивает в том числе High
  (тест `:38` — Android high с `?dpr=2` даёт 2.75/2).
- Как исправить: «low takes the smallest k with device / k at most maxRatio (k from device / maxRatio up); when that
  would leave the visible short side under 360 canvas pixels, the largest k that keeps it at 360, the ratio then
  exceeding maxRatio; `?dpr=<number>` replaces maxRatio in every mode, so it caps High too, `?dpr=native` keeps the
  device ratio».
- Проверка: run (`node harness.js` на копии файла): low/maxRatio 1 → ratio 0.916667, k 3.000, `<= maxRatio true`;
  high с `?dpr=2` на Android → 1.375000, `<= maxRatio false`.

## Не проверено

- Unity-ветки `framerate` (порядок маркеров `PlayerLoop`, гистерезис Auto) проверены чтением, не прогоном;
  `RenderResolutionScriptTests` не запускались (нужен `node` и редактор с пакетом в `Assets`).
- `.meta` и GUID пакета `resolution` (импортёр `.jslib`, GUID asmdef) проверены выборочно, не сплошным аудитом.
- Изменения `AutoFrameRateMemory`/`PlayerPrefs` на устройстве, DPR и поведение на iPadOS — только чтение.

## Не доказано

Нет: каждая находка получила CONFIRMED или FALSE.

## Отброшено

- **FRT-1.** `FrameWorkMeter`: «первый `End` после `Run` пишет аптайм машины в `LastWorkMs`». Опровергнуто:
  `PlayerLoop.SetPlayerLoop` вступает в силу со следующего полного прохода цикла, поэтому кадр `Run` исполняет старый
  цикл без маркеров вовсе, а с следующего кадра `Begin` (голова `EarlyUpdate`) и `End` (хвост `PostLateUpdate`) стоят
  в одном цикле и всегда парны; первый `Feed` нового правила и так отбрасывается (`_hasFrame == false` → `Restart`).
  Маркеры живут на lifetime скоупа (`_setting.FrameRate.View(lifetime, Choose)`), поэтому и повторный вход в Auto не
  даёт устаревшего `_started`. Отдельный незаявленный риск: `With` молча не вставит маркер в кастомный цикл без
  `EarlyUpdate`/`PostLateUpdate` и оставит `End` без пары.
- **RES-3.** «`render-resolution.js` хардкодит localStorage-ключ игровой оболочки, нарушая P14, и чужое значение под
  ключом валит старт». Опровергнуто: `window.webShell` — контракт платформы `web` самого фреймворка
  (`client/platforms/web/Runtime/WebShell.cs:7,10`, `WebExtensions.cs:7`, `WebShell.jslib:1`, `package.json:6`), а не
  фича игры; сохранение ключа задокументировано (`render-resolution.js:31-32`, `releases/unreleased.md:32`,
  `docs/tasks/current/web_perf_tools/handoff/23_g1.md:17`) и закреплено тестом. Предложенная замена на
  `framework.resolution` не чинит ни одно из заявленных вредных следствий: ключ остаётся пер-origin (стало бы хуже),
  бросок на неизвестном значении остаётся. Сам бросок уже зафиксирован как U2 в
  [2026-11-06_client.md](2026-11-06_client.md) (мелкий, только при ручной правке хранилища) и здесь не переоткрывается.
