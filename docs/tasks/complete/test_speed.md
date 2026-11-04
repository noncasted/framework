## Ускорение тестов

### Что сделано
- `tools/scripts/test-all.sh`: полный набор 234-239 с -> **36,6 с** (цель ≤ 60). Смена API в `client/core` — 41,6 с, без `.build` — 51,4 с, первый запуск на новой копии `Library` — 73,7 с.
- Параллельность: колонки test-all в фоне (`R1`, `e021a7e`); dotnet не пересобирается на новом коммите без кода (`F1`, `Directory.Build.props`).
- Backend 99 -> 21-24 с (`B1`/`B2`): fixture-фикстуры, `xunit.runner.json`, `RuntimeChannelClient/Options`.
- EditMode 66 -> 31-41 с (`U2`/`U3`): Latency/Matchmaking/Audio ждут UniTask не по кадрам; `EditorSettings`, стрип-пакет; нестабильный `WebRtcPeerSocketFailureTests` устранён (`U1`, +5 тестов).
- Набор: backend 839, common 92, Verify 148, EditMode 615 (4 пропущено), PlayMode 71. В `main` и origin: `1cb330b`.

### Открыто
- Не исследовано: время этапов Unity кратно 5 с плюс 1 (26, 31, 36, 41), похоже на квант при выходе редактора.
