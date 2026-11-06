## WebGL build size per Unity version

### Что сделано
- `client/measurements`: шаблонный проект, варианты стриппинга (`base` — список pixel-swarm; `aggressive` — без canvas, uGUI, TMP и физики), `tools/measure.py <version>` собирает, меряет и прогоняет сборку в headless Chromium, `report.py` пишет RESULTS.md.
- `--trace-missing` показывает вызовы вырезанных сабмодулей, `--profile` — сабмодули, которые сцена вызвала в инструментированной сборке (кандидаты на вырезание).
- Измерены 6000.6.8f1, 6000.7.0b8, 7000.0.0a10, a11, a12, a16; выводы в ANALYSIS.md.

### Ключевые файлы
- `client/measurements/README.md` — как запускать и что значат числа
- `client/measurements/ANALYSIS.md` — выводы по версиям
- `client/measurements/tools/measure.py`, `tools/smoke.mjs`, `tools/report.py`
- `client/measurements/variants/*.json`, `stripping/*.txt`
- `client/measurements/template/Assets/Measurement/Editor/MeasurementBuild.cs`

### Заметки
- 6.7 и a10 легче 6.6 (меньше кода в wasm). С a11 добавляется `UnityEngine.EntitiesModule` (+250 KiB), а .NET 10 в a16 — ещё около 1.4 MB.
- На a16 ICU обязателен, а группа `Advanced Text Generator Support` его содержит (WEBGL_TROUBLESHOOTING.md, B14).
- Сборка идёт с `-nographics`, иначе редактор открывает окно TMP Importer.
- Brotli файла data шумит примерно на ±20 KiB.
- Возможные продолжения: invariant globalization на a16 (`IL2CPP_SYSTEM_GLOBALIZATION_INVARIANT`), своё определение сабмодуля для EntitiesModule, проверка 9 кандидатов из профиля base на самой игре.
- Сгенерированные `projects/` в git не идут; каждый новый релиз Unity — `tools/measure.py <version>`, затем ANALYSIS.md.
