---
task: file_splits
updated: 2026-11-02
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [1] | [x] | `baseline.md`: Verify 149/149, EditMode 361 passed + 4 skipped, PlayMode 12/12, md5 `4924df6a...` | — |
| [2] | [ ] | — | — |
| [3] | [x] | `93b20e9`: EditMode 406 + 4 skipped, PlayMode 56/56; проверено по `final.*.xml`, файлы до 165 строк | — |
| [4] | [ ] | — | — |
| [5] | [ ] | — | — |
| [6] | [ ] | — | — |
| [7] | [ ] | — | — |
| [8] | [ ] | — | — |
| [9] | [ ] | — | — |

## Заметки

### [2026-11-02] Находки шага 3
- Нестабильный тест `WebRtcPeerSocketTests.OtherSideTerminated_ClosedIsRaised` (`client/modules/network/Tests/Editor/WebRtcPeerSocketTests.cs:77`): libdatachannel пишет `DTLS alert: unknown CA` в лог ошибок, 1 падение на 8 полных прогонов EditMode. С разрезами не связан.
- В `client/project/ProjectSettings/InputManager.asset` нет осей `Gamepad Axis`, поэтому `GamepadReader.Substitute` и `ReadInput` в проекте фреймворка не проверить.
- Коммит `0d69baf` создан раньше проверки своего прогона. Повторный прогон на этом коммите зелёный.

### [2026-11-02] Решения оркестратора
- Работа идёт в ветке `file_splits`, не в `main`. Коммиты по одному классу задача требует сама.
- Работа идёт в worktree `.claude/worktrees/file_splits`: в основном дереве параллельно идёт `public_setters`.
- Unity только один за раз, в этом worktree. Свободно около 11 ГБ, редактор pixel-swarm занимает память, а `control copy` рассчитан на проект `client`: в фреймворке нет `.claude/control.json`, проект здесь `client/project`.
- Параллельно с Unity идёт только генератор: это чистый dotnet в своём worktree.
