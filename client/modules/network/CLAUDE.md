# Network client

Module `network` (`Internal.Network`, namespace `Internal`): the root scope calls
`AddNetwork(NetworkOptions, ProtocolHash.Value)` (the options asset belongs to the project), connections come
with `AddNetworkConnection` / `AddSessionServices`. Entities of the local user with a backend id come to
`ListenRemote`, the ones its client created before a reconnect to `ListenRestored`. Also the offline local server
(`offline://` urls, `ILocalServers`), latency monitor and watchdog (the backend answers `Ping`), and
`RemoteStateMachine`. A request is a span of the running trace unless its message is `IUnprofiledRequest` (what repeats through the
whole game: the telemetry). `Internal.Network` has no uGUI / TMP: the debug overlay of the latency monitor is the assembly
`Internal.Network.Overlay` (only with `com.unity.ugui`, `FRAMEWORK_TMP`), plugged by the session scope of a game with
`AddNetworkLatencyOverlay(NetworkOverlayOptions)` next to `AddSessionServices`.

The sessions, the host client included: `backend/Session/CLAUDE.md`. Lobbies and matchmaking: `backend/Hosting/CLAUDE.md`.
