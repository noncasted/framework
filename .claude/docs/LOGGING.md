# Logging

The logger of the client: `client/core/Runtime/Logging` (assembly `Internal`, namespace `Internal`). The code of a game logs
through it, runtime and Editor, not through `Debug.Log*` (release 0.15.0, step 4). What stays on `Debug.Log*`: a call with a context
object (`Debug.Log(text, gameObject)`, the console of the editor highlights it) and the code of an `ILogSink`.

## Getting a log

```csharp
public class PeerHost
{
    public PeerHost(ILog<PeerHost> log, ...)   // given by every scope: no registration
    {
        _log = log;
    }
}

public class SoundPool : MonoBehaviour
{
    [Inject]
    public void Construct(ILog<SoundPool> log) { _log = log; }   // the same in an [Inject] method
}

internal static class JsWebSocketCallbacks
{
    private static readonly ILog _log = Logs.Of(typeof(JsWebSocketCallbacks));   // a static class: no type argument
}

public class ProfilerTraceStorage                         // made with new, outside the container
{
    private static readonly ILog _log = Logs.Of<ProfilerTraceStorage>();
}
```

The ContainerGenerator binds a parameter `ILog<T>` to `Logs.Of<T>()` (one cached log per type). The category is the full name
of the type (`Global.Network.PeerHost`, nested types after a dot, generic arguments by their short names); the console line is
`[PeerHost] message`, so no `[Network]` prefixes in the texts.

## Writing

A field of a class named `Logs` hides the static class: `global::Internal.Logs.Of<X>()`. A `static readonly ILog` field that the
automatic cleanup of statics of Unity rejects (`UAC0036`) takes `[NoAutoStaticsCleanup]`.

```csharp
_log.Debug("Frame {Frame} sent {Bytes} bytes", frame, bytes);   // cut out of a release build
_log.Info("Joined the room {Code} in {Ms} ms", code, ms);
_log.Warning("Slow load of {Key}", key);
_log.Error("The host {Id} is gone", hostId);
_log.Error(exception, "Join {Code} failed", code);
_log.Warning(exception, "Retrying the socket {Url}", url);
_log.Exception(exception);                                     // the exception alone
```

- The text is a **template**: `{Name}` placeholders take the arguments by their order (0 to 4 of them), `{{` / `}}` are braces.
  The number of placeholders must be the number of arguments: anything else throws `FormatException` when the level is on.
  No `$"..."`, no concatenation: the template is the identity of the call site in the telemetry (one group whatever the
  arguments) and the arguments are never sent.
- The level is checked before anything is formatted: a log below its level costs one check and allocates nothing (the
  arguments are generic, no boxing; `LogsTests.BelowTheLevel_NothingIsWrittenNorAllocated`). Keep the arguments cheap to
  compute: they are evaluated before the call (`_log.Info("{A}", Expensive())` runs `Expensive` even when off).
- `Debug` is `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("FRAMEWORK_LOG_DEBUG")]`: in a
  release player the call and its arguments are not compiled at all. A release build that needs it defines `FRAMEWORK_LOG_DEBUG`.
- Below `Warning` a player writes without the stack (`LogOption.NoStacktrace`); the editor keeps it.
- Any thread.

## Levels

`OptionsContainer` of the game holds `LogOptions` (Project Tools, Options tab, Log Options): the level of every category without
a rule for development builds and the editor, the one for release builds, the mark level, and rules by prefix
(`LogRule`: a namespace or a type, matched by whole segments, the longest wins) with the same three levels.
`OptionsContainer.Register` applies them (`Logs.Configure`); what logs before the root scope runs on the defaults of
`LogOptions` (development `Info`, release `Warning`, no marks).

## Telemetry

With `modules/telemetry` in a session (`TelemetryLoggerErrors`, `LogMarkTelemetryCollector`):
- a `Warning` / `Error` the console shows is an error of the session with its `Category` and its template as the message; an
  exception adds `<Type>: <text>` and its stack. The line the logger writes into the log of Unity is not taken again
  (`Logs.IsWriting`); errors of Unity, of the browser and of third-party code still come through the log of Unity, without a
  category.
- a record at or above the mark level of its category is a mark `log.<level>` with the detail `<ShortName>: <template>`, the
  time of the call; at most 64 between two samples.

## Sinks

`Logs.AddSink(lifetime, ILogSink)`: every record of a level that is on (`LogRecord`: category, level, template, message,
exception, `IsShown`, `IsMark`), on the thread of the call. A sink must not log itself; one that throws is isolated.
