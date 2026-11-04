# parallel_client_deploy - результат

## Изменённые файлы

| Файл | Шаг | Что |
|------|-----|-----|
| client/platforms/web/Editor/WebGlStripping.cs (+.meta) | 1 | новый: загрузка настроек stripping, throw при пустых |
| client/platforms/web/Editor/WebGlBuild.cs | 1 | вызывает WebGlStripping.Require() |
| client/platforms/web/Editor/Internal.Platforms.Web.Editor.asmdef, package.json | 1 | зависимость от stripping-tool |
| client/packages/com.unity.web.stripping-tool/Editor/StrippingProjectSettings.cs, FORK.md | 1 | патч форка: LoadSettings public |
| releases/unreleased.md | 1 | BREAKING: WebGL-сборка требует стрип |
| tools/scripts/deploy-client.sh, tools/scripts/deploy-client/*.sh | 2 | новый скрипт параллельного деплоя |
| /projects/pixel-swarm/.claude/deploy-client.json | 2 | значения игры (цели, проверки) |
| .claude/skills/deploy-client/SKILL.md | 3 | скилл вызывает deploy-client.sh, без MCP/редактора |
| /projects/pixel-swarm/.claude/project-skills.md | 3 | ссылка на deploy-client.json |
| releases/unreleased.md, CLAUDE.md | 3 | раздел для игр; deploy-client.json в списке файлов игры |
