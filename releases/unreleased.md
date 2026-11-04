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
