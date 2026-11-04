## web_bench_tools: стенды и отладка web-клиента во фреймворке

### Что сделано
- `tools/scripts/web-bench/stand/`: headless Chromium через CDP. Эмуляция телефона, замедление CPU, хост прода подменён локальной TLS-сборкой, профиль, GL-счёт и запись, скриншоты, сценарий `window.<api>`, A/B `batch.sh` на ненагруженной машине.
- Сторож сети: meta и все сторонние хосты отрезаны, сокеты и запросы записаны в `summary.meta`, ответ внешнего хоста даёт exit 3.
- `profile/`: CLI `profile.py top|callers|kids|selfunder|phases|cmp|fit` и `debug-wasm.sh` для имён функций.
- `ios/` (Safari по USB, перезапуск при залипшем WebGL, скриншоты, пробники iPad), `lan/` (раздача телефонам и remote-log), `android/` (adb + CDP).
- Скилл `device-lab`. В pixel-swarm: `.claude/device-lab.json`, rows-файл, раздел в project-skills, ссылка на скилл.

### Ключевые файлы
- `.claude/skills/device-lab/SKILL.md` (+ `stand.md`, `profile.md`, `ios.md`, `lan.md`, `android.md`)
- `tools/scripts/web-bench/stand/run.mjs`, `stand/network.mjs`, `stand/batch.sh`
- `tools/scripts/web-bench/profile/profile.py`
- `tools/scripts/web-bench/ios/iosctl.py`
- `~/.config/device-lab/devices.json` (машина), `<game>/.claude/device-lab.json` (игра)

### Заметки
- Сборка pixel-swarm из редактора открывает meta-сокет на хост страницы, а не на `metaHost`: проверяй `summary.meta.sockets` первого прогона.
- Порог load 12 даёт ±30% на одинаковых сборках. Для мелкой разницы нужны порог 6–8, 3 прогона на точку и сравнение busy/кадр, а не времени кадра (60 fps упирается в rAF).
- Профиль с именами функций получается только от сборки с её собственным debug wasm (копия `deploy-client` с `KEEP_COPY=1 --no-deploy`).
- На железе (iPhone, iPad, Android) на 2026-11-04 не проверено; для Android нет adb.
