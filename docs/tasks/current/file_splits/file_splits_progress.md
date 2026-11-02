---
task: file_splits
updated: 2026-11-02
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [1] | [x] | `baseline.md`: Verify 149/149, EditMode 361 passed + 4 skipped, PlayMode 12/12, md5 `4924df6a...` | — |
| [2] | [x] | `file_splits_gen` @ `ca6cc23`: Verify 149/149, снимок и вывод кейсов равны эталону (перепроверено оркестратором), 4 игры без отличий (со слов агента) | — |
| [3] | [x] | `93b20e9`: EditMode 406 + 4 skipped, PlayMode 56/56; проверено по `final.*.xml`, файлы до 165 строк | — |
| [4] | [x] | EditMode 412 + 4 skipped, PlayMode 71/71; после слияния со 2 и main см. заметку | — |
| [5] | [ ] | — | — |
| [6] | [ ] | — | — |
| [7] | [ ] | — | — |
| [8] | [ ] | — | — |
| [9] | [ ] | — | — |

## Заметки

### [2026-11-02] Слияние шагов 2 и 4 с main
- `file_splits` перебазирован на `main` (коммиты `public_setters`). Единственный конфликт: сеттеры `GamepadState` перенесены в вынесенный `GamepadState.cs` и `ReadInput`.
- `file_splits_gen` перебазирован на `file_splits`, слияние fast-forward.
- Старый (`4004262`) и новый генератор на одном дереве дают одинаковый вывод, md5 манифеста `24993616...`. `harness/baseline_manifest.md5` обновлён на него: прежний эталон устарел из-за новой регистрации в `network`.
- Первый прогон EditMode после слияния уронил `AudioPlayerTests.UpdaterPaused_MusicStillFadesIn` на логе Unity о `Assets/common` и `Assets/Common` (различие только в регистре), код теста ни при чём. Лог пишется при импорте и может попасть в любой тест. Это особенность `client/project`.

### [2026-11-02] Находка шага 4
Подтверждён дефект N8: исключение отчёта завершает цикл пингов до конца сессии (`NetworkLatencyMonitor.cs:81`, источник `GuestLatencyReport.cs:18`, `HostLatencyReport.cs:20`). Поведение сохранено, закреплено тестом `KnownDefect_FailedReportStopsTheProbes`.

### [2026-11-02] Находки шага 2
- Ветка `file_splits_gen` вливается в `file_splits` после шага 4: Unity этого worktree занят, смена dll вызвала бы перекомпиляцию посреди прогона.
- Тела методов перенесены скриптом, границы выбраны по графу ссылок между членами. Агент читал не все классы, поэтому на шаге 9 границы смотрит независимый агент.
- `client/core/Runtime/Tools/Container/Runtime/ServiceRegistration.cs:8` называет `GraphWalker.IsRegistrationLike`, теперь это `InstallerClassifier`. Исправит шаг 8.
- Обобщённый корень даёт hint name с `<`: `Scope/Plan/ScopePlanBuilder.cs:45`, CINGR001. Поведение старое.

### [2026-11-02] Находки шага 3
- Нестабильный тест `WebRtcPeerSocketTests.OtherSideTerminated_ClosedIsRaised` (`client/modules/network/Tests/Editor/WebRtcPeerSocketTests.cs:77`): libdatachannel пишет `DTLS alert: unknown CA` в лог ошибок, 1 падение на 8 полных прогонов EditMode. С разрезами не связан.
- В `client/project/ProjectSettings/InputManager.asset` нет осей `Gamepad Axis`, поэтому `GamepadReader.Substitute` и `ReadInput` в проекте фреймворка не проверить.
- Коммит `0d69baf` создан раньше проверки своего прогона. Повторный прогон на этом коммите зелёный.

### [2026-11-02] Решения оркестратора
- Работа идёт в ветке `file_splits`, не в `main`. Коммиты по одному классу задача требует сама.
- Работа идёт в worktree `.claude/worktrees/file_splits`: в основном дереве параллельно идёт `public_setters`.
- Unity только один за раз, в этом worktree. Свободно около 11 ГБ, редактор pixel-swarm занимает память, а `control copy` рассчитан на проект `client`: в фреймворке нет `.claude/control.json`, проект здесь `client/project`.
- Параллельно с Unity идёт только генератор: это чистый dotnet в своём worktree.
