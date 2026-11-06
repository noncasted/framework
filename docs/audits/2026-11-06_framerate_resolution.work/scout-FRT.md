Package: client/modules/framerate, files: 17

### FRT-1 FrameWorkMeter.EffectiveWindow: the first `End` after `Run` can report the machine uptime as the frame work
- Where: `client/modules/framerate/Runtime/Auto/FrameWorkMeter.cs:33-45`, `client/modules/framerate/Runtime/Auto/AutoFrameRate.cs:35-49`
- Rule: bug
- Severity: minor
- Evidence: reading
- Claim: `Run` installs the `Begin` marker mid-frame (the setup runs after `EarlyUpdate` has already passed) but resets only `LastWorkMs`; `_started` keeps its previous value, `0` on the first run and the last frame of the previous run on a re-entry into Auto. The `End` marker of that same frame therefore computes `(Stopwatch.GetTimestamp() - _started) * _msPerTick`, a duration of seconds or the whole machine uptime, and stores it in `LastWorkMs`. That bogus value is skipped only when the updater's first `OnUpdate` happens after it; between them the rule is fed this value as a genuine frame.
- Scenario: the player switches the setting to Auto at 60 fps with ~4 ms frames. Frame N: `Run`, `End` reports e.g. 1.7e6 ms. Frame N+1 `OnUpdate` feeds `workMs = 1.7e6` with a 16 ms interval, so the window's average is enormous and counts as a past window; `_held` already advances one second towards the 8 s drop before real frames are measured. On a device that is genuinely over 12 ms the drop therefore happens one window early; with a lucky ordering of the first `OnUpdate` the value is instead discarded, so the behaviour is timing-dependent.
- Proof lines: `FrameWorkMeter.cs:33` - `Run` sets `LastWorkMs = 0` but never `_started`; `FrameWorkMeter.cs:42` - `End` subtracts `_started` unconditionally; `FrameWorkMeter.cs:44` - `_started` is a `long` field with the default `0`; `AutoFrameRate.cs:43` - `OnUpdate` feeds whatever `LastWorkMs` holds; `AutoFrameRateRule.cs:33` - only the very first feed or a long gap is skipped, not a value whose origin is the bogus frame.
- Could be wrong if: the scope setup always runs before `EarlyUpdate` of the frame (so `_started` is always fresh) — I could not observe Unity's frame ordering for `OnSetupCompletion`; the code itself has no guard against the ordering it depends on.
- Fix: at the end of `End`, or in `Begin`, mark the sample valid (`_started = 0` sentinel and `End` returns without writing `LastWorkMs` when `_started == 0`), or reset `_started` in `Run` and have `End` skip until the first `Begin` of the new installation.

### FRT-2 AutoFrameRateOptions.Validate is reachable only through the Auto rule
- Where: `client/modules/framerate/Runtime/Auto/AutoFrameRateOptions.cs:44-57`, `client/modules/framerate/Runtime/Auto/AutoFrameRateRule.cs:10-13`, `client/modules/framerate/Runtime/FrameRateLimit.cs:34-39`
- Rule: P8 (fail fast)
- Severity: minor
- Evidence: reading
- Claim: `Validate()` is the only check of the option numbers, and it is called only from the `AutoFrameRateRule` constructor, which exists only when the setting is Auto *and* the device is mobile. `FrameRateLimit` uses `_options.Low` / `_options.High` for the fixed choices, so an options asset with `_low >= _high` (or `_riseWorkMs >= _dropWorkMs`) silently applies the two choices swapped on every desktop, in the editor and for any player who picks 30/60 explicitly, and only throws much later on a phone in Auto mode.
- Scenario: a game sets Low=60, High=30 by mistake. `FrameRateMode.Low` applies 60 and `FrameRateMode.High` applies 30 for all players; nothing throws and the button shows the opposite of what happens. The same asset would fail fast only after a phone enters Auto.
- Proof lines: `FrameRateLimit.cs:38-39` - the request path returns without ever calling `Validate`; `FrameRateLimit.cs:47-49` - `_options.Low`/`_options.High` are applied without validation; `AutoFrameRateRule.cs:12` - `options.Validate()` is the only call site; `AutoFrameRate.cs:32` - the rule is created only in `Run`, which `FrameRateLimit.cs:70` calls only for Auto on a mobile device.
- Could be wrong if: an editor validator on the options asset calls `Validate` — I found none in the package or in `client/core`.
- Fix: call `options.Validate()` once in `FrameRateLimit.OnSetupCompletion` (before any branch) or in `AddFrameRate`, so a wrong asset throws at scene start on every platform.

### FRT-3 Public const placed between the constructor and the private fields
- Where: `client/modules/framerate/Runtime/FrameRateLimit.cs:21`
- Rule: CODE_STYLE_FULL § Member Order
- Severity: minor
- Evidence: reading
- Claim: the declared order is constructor (1), private fields (2), public members (3), but `public const int Native` stands directly after the constructor and before the four `private readonly` fields.
- Scenario: reading a member list against the documented template no longer works by position; the one rule the guide spells out with a numbered example is broken by the file it is used in.
- Proof lines: `FrameRateLimit.cs:11` - constructor starts; `FrameRateLimit.cs:21` - `public const int Native = -1;`; `FrameRateLimit.cs:23-27` - the private readonly fields follow.
- Could be wrong if: a const is not counted as a field for the order — the guide's own template does not separate them, and no other framework type I saw puts a public const before the fields.
- Fix: move `Native` below the `private readonly` field block (next to the other public members), or above the constructor together with the fields.

### Not checked
- `Runtime/Request/WebFrameRate.jslib` was read; its C# side (`WebFrameRateRequest`, `NoFrameRateRequest`, `IFrameRateRequest`) was read and cross-checked against the pixel-swarm original — `?fps=native|<n>` priority, `NoRequest = 0` sentinel and the standalone `JsLibStub` branch are all deliberate and match, no finding.
- `AutoFrameRateMemory`, `IAutoFrameRate`, `FrameRateMode`, `IFrameRateSetting`, `FrameRateExtensions`, both telemetry files, both test files: read; no rule break found (the `new AutoFrameRateMemory().Forget()` hits in `audit.sh` are the class's own PlayerPrefs method, not the UniTask `.Forget()` extension, so not a style finding; the telemetry assembly guard `FRAMEWORK_TELEMETRY` via `defineConstraints` + `versionDefines` matches the `resolution` module and the `client/project` defines).
- Not verified by a build or a Unity run (audit is read-only); the `AutoFrameRateRule` arithmetic was checked by reading only, not by executing the tests.
