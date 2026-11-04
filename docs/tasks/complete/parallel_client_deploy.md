## parallel_client_deploy: деплой целей параллельно

### Что сделано
- `WebGlStripping.Require()`: WebGL-сборка бросает без настроек стрипа сабмодулей (BREAKING: в релиз 0.4.0); форк `com.unity.web.stripping-tool` патчен (`LoadSettings` public).
- `tools/scripts/deploy-client.sh` + `tools/scripts/deploy-client/`: тесты и сборки каждой цели в своей reflink-копии параллельно, `--no-deploy`; редактор не нужен. Pixel Swarm: ~16-20 мин -> 471-515 с на два деплоя.
- Значения игры в `<game>/.claude/deploy-client.json` (цели, проверки); скилл `deploy-client` вызывает скрипт, ссылка в `.claude/project-skills.md`. Раздел для игр в `CLAUDE.md`.
- Фреймворк закоммичен и запушен (`954068cf`, `c061e543`, `6f49c1df`).

### Открыто
- Шаг 4 «что выкладываем» пропущен по решению пользователя.
