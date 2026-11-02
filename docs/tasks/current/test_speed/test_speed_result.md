## Ускорение тестов — Результат

База: 233,6 - 238,7 с последовательно. Цель: не больше 60 с.

| Шаг | Коммит | Стало | Файлы |
|---|---|---|---|
| R1 | `e021a7e` | Стена 122 с при нагрузке 28-84 | `tools/scripts/test-all.sh`, `CLAUDE.md`, `SceneTemplateSettings.json` |
| U1 | `9eaa477` | Нестабильный тест устранён, +5 тестов | `client/modules/network/Runtime/Sockets/WebRtc`, `WebRtcPeerSocketFailureTests.cs` |
| B1 | `a5caaa9`..`87a6b1d` | Backend 99 -> 30-35 с | `backend/Tests`, `RuntimeChannelClient.cs`, `RuntimeChannelOptions.cs`, `MESSAGING.md` |
| B2 | `1c29634`, `df47196`, `85de06f` | Backend 35-39 -> 21-24 с | `backend/Tests/Fixtures`, `xunit.runner.json`, `StateCollectionSyncTests.cs` |
| U2 | `3b1bffc`, `3163da7`, `6ed2582` | Три класса: 30,6 -> 2,3 с | `client/modules/network/Runtime/Latency`, тесты lobbies, network, core |
| U3 | `58d4ba6`..`ef539ed` | EditMode 66 -> 31-41 с, PlayMode 41 -> 21 с | `client/core/Tests/EditorTools`, `client/modules/network/Tests/Editor`, `com.unity.web.stripping-tool`, `EditorSettings.asset` |
| F1 | `3b71314`, `cd84700`, `1cb330b` | Стена 36,6 с, новый коммит не пересобирает dotnet | `Directory.Build.props`, `releases/unreleased.md`, `CLAUDE.md` |

## Итог

| Случай | Время, с |
|---|---:|
| База, последовательно | 234-239 |
| `tools/scripts/test-all.sh` | 36,6 |
| Смена API в `client/core` | 41,6 |
| Без `.build` | 51,4 |
| Первый запуск на новой копии `Library` | 73,7 |

Набор: backend 839, common 92, Verify 148, EditMode 615 (4 пропущено), PlayMode 71. В `main` и origin: `1cb330b`.

Не исследовано: время этапов Unity кратно 5 с плюс 1 (26, 31, 36, 41), похоже на квант при выходе редактора.
