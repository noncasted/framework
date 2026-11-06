# План аудита: framerate_resolution (2026-11-06)

Область: `client/modules/framerate`, `client/modules/resolution`. Файлов `.cs`: 30.

| Пакет | Папки | Файлов | ID | Фокус |
|---|---|---|---|---|
| FRT | `client/modules/framerate` (Runtime, Telemetry, Tests) | 17 | FRT | порядок `Application.targetFrameRate` и правило Auto (гистерезис, окна 8/30 с); жизненный цикл подписки на `IFrameRateSetting.Value` и таймеров на `updater`; телеметрия: метки без сессии и лимиты; ветки `?fps=`, настройки, AutoFrameRateMemory; мёртвый код Request/* |
| RES | `client/modules/resolution` (Runtime, Web, Editor, Telemetry, Tests, WebTemplate~) | 13 | RES | `RenderResolution` и `WebRenderPixelRatio`: `renderResolution.pixelRatio/follow` из шаблона, DPR, целые k, короткая сторона; жизненный цикл follow и подписки на смену разрешения; `RenderResolutionInjection` (вставка скрипта в `<head>`, идемпотентность); телеграф `settings.resolution`; мёртвый код `NoRenderPixelRatio` |

Документы правил для обоих: `ARCHITECTURE_PRINCIPLES.md`, `CODE_STYLE_FULL.md`; по фокусу:
`COMMON_LIFETIMES.md`, `COMMON_REACTIVE_BASICS.md`, `WEBGL_TROUBLESHOOTING.md`, `ARCHITECTURE_PATTERNS.md`.

Кандидаты `audit.sh`: см. `candidates-FRT.txt`, `candidates-RES.txt` (dependency-check и проверка имён игры недоступны: в репозитории фреймворка нет `.claude/code-audit.json`).
