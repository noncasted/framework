## Pixel Swarm: разведка лагов на слабом Android (только чтение кода, коммит 9205a88a)

Телефон не запускался, Frame Debugger и `malioc` не использовались. Доли в мс не измерены.

### Меню
- Вероятный упор: заполнение. `Menu/.../MenuBack.shader` считает процедурный фон на каждый экранный пиксель, около 2.66 млн (вычислено).
- Разрешение: `WebGLTemplates/Default/TemplateData/web-shell.js:46-52`, `renderPixelRatio`: 393 x 2.75 = 1080.75 >= 1080, делитель 1, canvas 2461x1081. При 1079 было бы 540 по высоте: телефон прошёл порог на 0.75 пикселя. В Telegram так же.
- `_PixelSize` только квантует координаты (`MenuBack.shader:135`), не экономит.
- Шейдер: 6 sin/cos/exp, в 18% ячеек до 10; `fmod`, массив на 16, `colors[5]`. Лобби: 3 sin + 1 exp при тех же пикселях, кадр не больше 28.6 мс.
- Забег быстрее: тяжёлая сцена 772x340 (262 тыс. пикселей), на весь экран только дешёвый `Upscale.shader`.
- Не проверено: разница объясняется подсчётом в 2-3 раза, наблюдается не меньше 3.5; 116.7 мс ровно 7 vsync.
- Настроек `renderScale`, MSAA, постобработки нет; флага качества нет.

### Эксперимент для меню
Делитель `k` для `config.devicePixelRatio` в `index.html`/`web-shell.js` выкладки, без сборки Unity. Прогноз `frame.dt.p50`: 15 + 100/k² (k=2: 40 мс, k=3: 26 мс). Смотреть `frame.dt.p50`, `frame.cpu.main` (около 15), `render.draw_calls` (контроль). Подтверждение: рисовать фон `MenuBack` в текстуру 410x180 (оценка: в 36 раз меньше фрагментов).

### Забег, главный поток (кандидаты, порядок предположительный; N тел, S искр, P снарядов, L света)
1. `GamePlay/Swarm/Services/SwarmView.cs:186-266`, O(N): 6+ вызовов движка на тело без проверки изменений.
2. `Swarm/Simulation/SwarmWorld.cs:372-400`, O(N): 9+ проходов за шаг.
3. `Swarm/Simulation/SwarmMovement.cs:79-113`, O(N·k): окно растёт на `MaxRadius` всех видов, включая отсутствующих (`HeartHoarder.asset:19` 2.55).
4. `SwarmSteering.cs:77`, `Arena/ArenaGrid.cs:109-120`, O(N·d): до 96 выборок на тело.
5. `Players/Progression/SparkWorld.cs:89-121,220-230`, O(S): искры копятся до 1024. Лучший кандидат на рост за 2 минуты, счётчика нет.
6. `Common/Rendering/Lighting/LightPass.cs:57-88`, `LightSet.cs:50`, O(L).
7. `Services/Projectiles/ProjectileFlight.cs:101-108`: при кадре больше 50 мс шагов 2, положительная обратная связь.
8. `Services/Particles/PixelMesh.cs:65-70`, `SwarmShadows.cs:71-76`: тени пересобираются каждый кадр.
9. `GamePipeline.cs:286,319`: 3 сортируемых списка.
10. `SwarmGrid.cs:34-46`: 10 240 клеток дважды за кадр.
11. `SwarmField.cs:35-85`: до 40 960 узлов раз в 0.25 с, это скачок.
- Множитель, не проверено: `il2cppCodeGeneration: WebGL: 1`, по памяти `OptimizeSize`; может замедлять `Read<T>`/`Has<T>` в StaticEcs.
- Отклонено: физика роя (нет коллайдеров), аллокации как главная причина, Burst и jobs не найдены.

### Render textures забега
14 штук совпадают с кодом: Albedo, Emission, Height, Shade (`GamePipeline.cs:410-419`), 2 Shape, 2 Occlusion, Light, Lit, 4 уровня блума. Это штатный набор кадра. Постоянно 12 полноэкранных проходов и около 20 смен целей. Почему setpass больше draw calls, код не доказывает (`DrawProcedural` или чередование материалов); смотреть Frame Debugger.

### Для коллекторов телеметрии
- Есть: `ISwarm.World.Count`, `IProjectiles.Count`, `IPixelParticles.Count` (`FrameProfiler.cs:178-180`), `ITransientLights.Count`, `LightSet.Lights.Count`. `SparkWorld._count` приватный.
- Телеметрия живёт в скоупе meta (`MetaScopeExtensions.cs:77-78`), сервисы забега в игровом: нужен мост или `AddCollector(lifetime, collector)`.
- Телеметрия не видит применённый ratio: `TelemetryWeb.jslib:94-96` отдаёт CSS-размер и `window.devicePixelRatio`.
