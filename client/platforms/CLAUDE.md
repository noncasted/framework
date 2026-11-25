# Platforms

`itch` (`await AddItchSaves()`: saves in PlayerPrefs, loaded by `AddSaves` of `publish` while the scope is built), `web` (`AddWebLoadingScreen`, `AddWebSafeArea`,
`WebShell.UseMobileCheck`, which also turns Run In Background off on a mobile page; needs `window.loadingScreen` and, for the shell, `TemplateData/web-shell.js` in the
WebGL template; register only under `#if UNITY_WEBGL && !UNITY_EDITOR`; editor: `Internal.WebGlBuild.Build`,
the batchmode build with the active build profile inside `PlayerBuildSteps.Run`, exit code 1 on any failure; every WebGL
build gets `WebTemplate~/audio-session.js` first in the `<head>` of its `index.html` (`WebAudioSessionInjection`:
`navigator.audioSession.type = 'playback'`, so the silent mode of iOS does not mute the sound; `BuiltPageScripts` of the core
puts such scripts, the startup trace of `telemetry` too), `steam` (`AddSteam(SteamOptions, PlatformOptions)`, the `NoPlatform*` variants of `publish` off Steam: app id, Web API identity
of the backend auth ticket, the connect key of invites; `#if UNITY_STANDALONE`, needs Steamworks.NET), `telegram`
(`AddTelegramAPI(PlatformOptions, TelegramEditorOptions)`: the bridge to `window.telegramShell` of the Telegram WebGL template, a debug one in the editor that signs the init data with `TelegramBotToken` of the local backend settings file the game names in `TelegramEditorOptions`; auth and back button are alternatives the game picks).
