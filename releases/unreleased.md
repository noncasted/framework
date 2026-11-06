# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Тестовые инструменты: `FrameAllocations`, `WebGlShaderCheck`

Кому: клиент игр со своими тестами аллокаций кадра или проверкой шейдеров для WebGL (pixel-swarm: `FrameAllocationTests`,
`WebGlShaderTablesTests`; остальным по желанию).

Поиск: `git grep -nE 'GetAllocatedBytesForCurrentThread|ImmCB_|TempArray|CompileVariant' -- 'client/Assets/*.cs'`

- Новая сборка `Internal.TestTools` (`client/core/Tests/Tools`, только Editor, `UNITY_INCLUDE_TESTS`). Тестовый asmdef игры ссылается
  на неё (`"Internal.TestTools"`), а в `client/Packages/manifest.json` в `testables` должен быть `com.noncasted.framework.core`: без него
  тестовые сборки пакета не компилируются.
- Замер `before = GC.GetAllocatedBytesForCurrentThread(); ...; Assert` -> `FrameAllocations.AssertNone("что", passes, pass, warmup)`
  (`warmup` по умолчанию 10, не считается). Порог: меньше байта на проход, а не ноль; разовая аллокация рантайма (48 Б) не валит тест.
  Assert внутри прохода не ставить: NUnit выделяет память. Только число: `FrameAllocations.Measure(passes, pass, warmup)`.
- Своя компиляция шейдеров под GLES3x -> `WebGlShaderCheck.AssertSafe(WebGlShaderCheck.Find(new[] { "Assets" }, new[] { "Assets/Plugins/" }))`:
  папки и исключения (сторонние шейдеры) задаёт игра. Проверка ловит ещё и flat varyings (`nointerpolation`, G1 в
  `WEBGL_TROUBLESHOOTING.md`), а не только `ImmCB_` / `TempArray`: шейдер с `nointerpolation` теперь валит тест. Пример: pixel-swarm
  `client/Assets/GamePlay/Tests/WebGlShaderTablesTests.cs`.
### Разрешение рендера на телефонах и частота кадров 60/30: модули `resolution` и `framerate`

Кому: клиент игр с WebGL, где есть свои копии этого из pixel-swarm (`Global.Resolution`, `Global.FrameRate`, `webShell.renderPixelRatio`).
Поиск: `git grep -nE "renderPixelRatio|followRenderPixelRatio|frameRateRequest|setResolution|class (FrameRateLimit|AutoFrameRate|RenderResolution|FrameRateTelemetryMarks|ResolutionTelemetryCollector)\b" client`.
Что сделать:
- `client/Packages/manifest.json`: `com.noncasted.framework.resolution` и `com.noncasted.framework.framerate` (`file:../../tools/framework/client/modules/<имя>`), оба в `testables`; удалить свои копии классов и `.jslib` (типы и пространства имён те же: `Global.Resolution`, `Global.FrameRate`).
- asmdef, где используются типы, и каждый asmdef скоупа под скоупом с `AddFrameRate` / `AddResolution` (иначе `CINGR003`, ссылки не транзитивны): `Internal.Resolution`, `Internal.FrameRate`; asmdef скоупа телеметрии: `Internal.Resolution.Telemetry`, `Internal.FrameRate.Telemetry`.
- Настройки игры реализуют `IResolutionSetting` / `IFrameRateSetting` (свойства `Resolution` / `FrameRate`) и регистрируются `.As<IResolutionSetting>().As<IFrameRateSetting>()`.
- Глобальный скоуп: `AddFrameRate(<AutoFrameRateOptions из ассета опций игры>)`, `AddResolution()`. Скоуп телеметрии: `AddFrameRateTelemetry()`, `AddResolutionTelemetry()` вместо своих `FrameRateTelemetryMarks` / `ResolutionTelemetryCollector`.
- WebGL-шаблон: `webShell.renderPixelRatio(max)` -> `renderResolution.pixelRatio(canvas, max)`, `webShell.followRenderPixelRatio(module, max)` -> `renderResolution.follow(module, canvas, max)`; из своего `web-shell.js` убрать разрешение и `?fps=` (`render-resolution.js` кладётся в `<head>` собранного `index.html` сам, ключ `webShell.resolution` в localStorage сохранён).

### Пакеты бэкенда: последние стабильные версии

Кому: бэкенд всех игр (версии берутся из `Directory.Packages.props` фреймворка).
Поиск: `git grep -n 'Aspire.AppHost.Sdk/' -- '*.csproj'`
- Orleans 10.4.2, Aspire 13.6.4, Marten 9.58.0, OpenTelemetry 1.20.x, BlazorBlueprint 4.1.0, ASP.NET Core / Extensions 10.0.14, тестовые пакеты
  (xunit.v3 4.0.2, Testcontainers 4.16.0, ...). Неиспользуемые `MudBlazor` и `Blazor.Bootstrap` удалены из списка.
- AppHost игры: `Sdk="Aspire.AppHost.Sdk/13.5.3"` -> `Sdk="Aspire.AppHost.Sdk/13.6.4"` (той же версии, что пакеты `Aspire.*`).
- Свои пакеты игры в её `backend/Directory.Packages.props` обновить до последних стабильных; собрать `backend.slnx` и прогнать тесты.

### Контейнер: удалены `IContainer.InjectGameObject` и `IViewInjector.Inject(GameObject)` (BREAKING)

Кому: клиент игр, которые вызывают внедрение в целый `GameObject` или реализуют `IContainer` / `IViewInjector` сами (на момент изменения
таких нет: mines-leader и storytale-workshop зовут `Inject<T>`).
Поиск: `git grep -nE 'InjectGameObject|: *(IContainer|IViewInjector)\b|IViewInjector.*Inject\((GameObject|[a-z]\w*\.gameObject)' -- 'client/Assets/*.cs'`
- Перегрузка бросала на первом компоненте без сгенерированного inject (`Image`, `Transform`-скрипты): обход по
  `GetComponentsInChildren<MonoBehaviour>` нельзя было использовать. Удалена из генератора, `IContainer` и `IViewInjector`.
- Вызов `Inject(gameObject)` -> `Inject(component)` для каждого компонента с `builder.Injectable<T>()` (как у storytale в
  `client/Assets/Game/Construct/Loop/ViewInjectorExtensions.cs`). Своя реализация `IContainer` / `IViewInjector`: удалить метод.
