# An own parallel test runner for Unity

A task for later, set by the user on 2026-11-02. Not started.

## What is needed

A runner of EditMode tests for every game on the framework that runs the tests in parallel inside an already open editor.

## Why an own one

The search found no ready runner with parallel execution (2026-11-02). That does not prove there is none. Unity Test Framework 1.9.0 is built on its own copy of NUnit 3.5, and its sources have `ParallelScope` only as a property of `UnityTestExecutionContext`.

## What the measurements say

Measurements of pixel-swarm, 2026-11-02, the task `test_speed` of the game (`docs/tasks/complete/test_speed.md`). Taken once on a shared machine. The times are without the start of Unity: a task usually already runs in its own editor.

| Set | Tests | One thread, s | 4 threads, s | 8 threads, s |
|---|---:|---:|---:|---:|
| Logic without generation (`fast`) | 757 | 27.9 | 7.0 | 3.5 |
| Generation of locations (`generation`) | 464 | 752 | ~460 | ~410 |

- The `fast` row counts every test as parallel. It is an upper bound: part of the tests work with the Unity API.
- The `generation` row counts about 52% of the time as parallel, before the tests were made cheaper. It is an estimate from the profile table, not a measurement. The limit without a thread limit is about 360 s: the rest needs the main thread (`AssetDatabase`, prefabs, scenes).
- For comparison, a measurement over separate Unity processes (the whole run, 485 tests): 1 process 475 s, 2 processes 277 s, 3, 4 and 6 processes 282-287 s. More than two give nothing on a shared machine: the tests in each one slow down. The cause (CPU, memory, GPU) was not separated; a runner in one process will hit it too.

## What to check first

Whether there is any gain depends on this:

1. Whether the code of the tests works off the main thread. For the generation of pixel-swarm this is not checked: if `Generate` calls the Unity API, the gain on it is close to zero. Start with a measurement: run a typical heavy method from a background thread.
2. A run inside an open editor was checked on a copy without a window: 10 runs of the logic in a row without failures, but without one test (`PrefabScreenshotTests.Ui_IsCapturedInACanvas_AndPrintsTheFields` crashed the editor with SIGSEGV twice) and always on a fresh domain: a repeat in the same domain gives failures. The tests themselves take 34-41 s there against 27 s in a separate run, the cause was not found. Details: `TESTING.md` and `UNITY_EDITOR_WORKFLOW.md` of the game. With a window and in the shared editor it is not checked.

## What to decide

- **Which tests run on threads.** Recommendation: only the explicitly marked ones, everything else sequentially on the main thread. Guessing from the code is dangerous: a mistake gives flaky failures.
- **How to isolate state.** Static caches of tests and tests that change a shared asset do not work on threads without changes.
- **How to select tests.** The same `[Category]`, `[Explicit]` and filters as Unity. A trap of the Unity runner: a positive category and `-assemblyNames` without a filter run `[Explicit]` tests; an own runner must not do that.
- **The format of the result.** NUnit XML: the run scripts of the games rely on it.
- **The life cycle.** `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]` and `[Timeout]` work as in NUnit. `[UnityTest]` runs only on the main thread.

## Done when

- On the logic set of a game the runner is faster than a sequential run; the set and the results of the tests match Unity Test Framework by name.
- 10 runs in a row without flaky failures and without an editor crash.
- A failure or a hang of one test does not lose the results of the others.
- No code, values or paths of a game (rule 1 of `CLAUDE.md`).

## Risks

- The gain in absolute numbers may be small: at most 24 s on `fast`. In `test_speed` the `generation` set was sped up by cheaper tests and two processes, so the runner is worth taking only on new numbers.
- The cost of the runner: its own maintenance on top of an alpha version of the editor, where Test Framework has already shown crashes.
