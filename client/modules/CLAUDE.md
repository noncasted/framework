# Modules

Assemblies `Internal.<Module>`, namespaces `Global.<Module>` (the global scope of the game registers
them). Each has an `Add*` extension of `IScopeBuilder`; prefabs and options come as arguments (the project owns
the assets and its generated `GlobalPrefabs` / `GlobalAssets`), so the project's global `Construct` calls them:

- `animations` (`Internal.Animations`, namespace `Internal`, no `Add*`): sprite animations of entities on the
  updater (`ForwardSpriteAnimation`, `RegisterSpriteForwardAnimation`), the
  `SpriteAnimationRenderer` component. `ISpriteAnimationData` stays in core (the sprite catalogue returns it).
- `backend` (`AddBackend`): HTTP get / post / media.
- `cameras` (`AddCamera(GlobalCamera prefab)`): camera of the global scope, `ICurrentCamera` (the camera of the
  scene set by the game: `Current`, the reactive `Value`, `SetOutputResolution` for the screen and the render texture
  of every camera set later), `ICameraUtils` (`ScreenToWorld` takes a perspective camera's point on its near plane).
- `audio` (`AddAudio(player, listener)`): pooled sounds with limits, crossfaded music on two sources. The scope
  must have an `IAudioVolume` (the settings of the game implement it). Button sounds are wired by the game. Sounds
  go on lines (`AudioLine.Music` / `SFX`, lines of the game with `AudioLine.Of<TVolume>`), may be trimmed
  (`SoundPlay.WithTrim`), clips loaded at runtime play as `clip.ToSound()`, music may be bound to a lifetime.
- `inputs` (`AddInput(InputOptions, EventSystem prefab)`): input scheme, gamepad by meaning, back / pause.
  Controls of a game (aim, sticks, touch layout) are options of the game. `InputManager.asset` of the project must
  hold the axes `Gamepad Axis 1..10` (type: joystick axis, axis N-1): `GamepadReader` throws at build without them.
  A platform without a gamepad layout (macOS, Android, iOS) gets `NoGamepadReader`.
- `ui` (`AddUI`, `AddLoadingScreen(prefab)`, `AddScreenSafeArea`, `AddClientOutdatedNotice(style)`): UI state
  machine and input constraints, design elements (+ editors), gamepad navigation (the event system
  prefab needs a `StandaloneInputModule`), safe area, loading screen, the notice of an outdated client
  (`IClientOutdatedNotice` of the core, shown by the network on close code 4001; built from code, the game gives
  the font, texts and colors in `ClientOutdatedNoticeStyle`; needs `IUINavigation`, so `AddUINavigation` too). `AddUI` = `AddUIStateMachine` + `AddUINavigation`: a game on `InputSystemUIInputModule` without a gamepad
  registers `AddUIStateMachine` only. Kinds of input constraints are instances of `InputConstraints` (`Game` in the
  framework, the project declares its own). Depends on `inputs` and `layout`.
- `layout` (`Internal.Layout`, `Internal.Layout.Editor`, namespace `Global.UI`): design layouts of `RectTransform`s
  (`DesignContainer`, `DesignGrid`, `DesignItem`) and their inspectors, without uGUI / TMP / inputs, so a game without
  `ui` (sprites only) uses them too.
- `publish`: platform abstractions (`ISaves`: `Load` once, awaited by `await builder.AddSaves(saves)` in the installer, `IPlatformAuth`, back button, inventory, invites, achievements). The project
  composes its publisher from the platforms; registrations under a runtime `if` around another installer are all
  built by the generator, so a platform choice is made with `#if` or inside one installer.
- `browser` (`Internal.Browser`): fullscreen, leave confirmation and clipboard of a web page.
- `network` (`Internal.Network`, namespace `Internal`): `network/CLAUDE.md`.
- `localization` (`AddLocalization(LocalizationOptions)`): texts in several languages from the table asset of the
  game (`Localization.json`, a column per `Language`), `TextKey` declared in code, `LocalizedText` with format
  arguments, the static `Localizer` (current language, `FromSystem`, `Next`), no uGUI / TMP. `LocalizedLabel` (keeps a
  TMP text in the current language) is the separate assembly `Internal.Localization.Tmp`, compiled only when the project has
  `com.unity.ugui` 2.x (`versionDefines` -> `FRAMEWORK_TMP`).
- `settings` (`AddSettings(SettingsOptions)`, `AddSettings<TSettings, TSave, TService>(SettingsOptions<TSave>)`):
  the settings of the player in the saves (`ISaves`): the volumes as the `IAudioVolume` of `audio`, the defaults from the options asset of the game; a game with settings of its own derives
  `SettingsSave`, `Settings<TSave>` (`OnPush`) and `SettingsOptions<TSave>` and injects its own interface over
  `ISettings<TSave>`. A language is a field of the save of the game (`OnLoad`, `OnPush`). Depends on `audio`, `publish`.
- `resolution` (`AddResolution()`, namespace `Global.Resolution`): the render resolution of a phone or a tablet on the web,
  Auto / Low / High (`ResolutionMode`; Auto is Low on iOS and iPadOS). The settings of the game implement `IResolutionSetting`.
  The build puts `WebTemplate~/render-resolution.js` first into the `<head>` (`RenderResolutionInjection`); the template calls
  `renderResolution.pixelRatio(canvas, max)` before `createUnityInstance` and `renderResolution.follow(module, canvas, max)` on a
  mobile page (device / k with k a whole number, the short side at least 360, `?dpr=native|<n>`). `AddResolutionTelemetry` in the
  scope of the telemetry: `settings.resolution`.
- `framerate` (`AddFrameRate(AutoFrameRateOptions)`, after `AddUpdater`, namespace `Global.FrameRate`): `Application.targetFrameRate`,
  strongest first: `?fps=native|<n>` of the page, the choice of the settings (`IFrameRateSetting`, Auto / 30 / 60), Auto: the rate
  of the screen on a desktop, on a phone `AutoFrameRateRule` (work of a frame above 12 ms for 8 s -> 30, below 6 ms for 30 s -> 60),
  remembered on the device (`FrameRate.AutoLow`). `AddFrameRateTelemetry`: the marks `fps.auto.<limit>`. The `*.Telemetry` assemblies
  of both compile only with the `telemetry` package (`FRAMEWORK_TELEMETRY`, a version define; `client/project` sets it).
- `lobbies` (`Internal.Lobbies`, namespace `Global.Lobbies`; `AddMatchmaking`, `AddLobbyNetworkService`):
  `IMatchmaking` (search, solo, lobby create / join / match join, tickets) over the meta socket the game registers
  as `IMetaSocket`; `LobbyNetworkService`, the client of the lobby service of a lobby session (a derived one adds the
  properties of the game in `CollectProperties`); `OfflineLobbyService` for the offline local server (the game
  gives `IOfflineLobbyMatches`); `SessionData`, `LobbyJoinResult`, `SessionSearchResult`. Depends on `network`.
- `telemetry` (`Internal.Telemetry`, namespace `Global.Telemetry`): metrics, errors and the startup trace of a client,
  `telemetry/CLAUDE.md`. Depends on `network`.
- `match-metrics`: what a game writes about a match, `.claude/docs/MATCH_METRICS.md`.
