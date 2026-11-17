---
name: update-framework
description: Update the framework submodule of a game to a release - find the pending releases, move the pointer, carry out the steps of their notes, build, test and commit. Use when the user runs /update-framework [version | main], asks to update or pull the framework, asks what must change in the project after a framework change, or when the code needs something a newer framework has.
---

# Update the framework

Run from the root of the project (not in the framework itself). The argument is a version (`0.2.0`), `main` for the head of
the framework, nothing for the latest release. The notes are `releases/<version>.md` of the framework: read them, do not
reconstruct the steps from commits.

## 1. Where the project stands

```
git -C tools/framework fetch --tags origin
TARGET=$(git -C tools/framework tag -l 'v*' --sort=-v:refname | head -1)   # or v<version>, or origin/main
git -C tools/framework show "${TARGET}:.claude/skills/update-framework/status.sh" | bash -s -- "$TARGET"
```

`status.sh` comes from the target, so a pin older than this skill works too. Act on what it prints:

- `pending: none` and no `unreleased`: nothing to update. Do step 6 if `skills` lists anything, report, stop.
- `foreign`: the pin holds commits with no identical patch in the target. With a `same subject` twin, compare the files of the commit
  with the target (`git -C tools/framework diff <commit> $TARGET -- <files of the commit>`, and the code it added must still be there);
  nothing lost: go on. Otherwise stop and report them: they must reach `main` of the framework first.
- `dirty` in the submodule: look at the diff. `git -C tools/framework stash push -m "before update-framework"` keeps it; name the
  stash in the report. Never discard it.
- `checkout DIFFERS`: the submodule is not on the pointer of the project. The pointer counts; say so in the report.
- Uncommitted changes in the project may belong to another session: leave them alone and stage only your own files.

## 2. Read the notes

Oldest first, each pending release and, for `origin/main`, the unreleased steps:

```
git -C tools/framework show "${TARGET}:releases/<version>.md"
git -C tools/framework show "${TARGET}:releases/unreleased.md"
```

The notes hold the steps (each with a search command), the changes of behaviour to check, the section for pins older than the base.
`N of M commits are already in the pin` means part of the steps is done: the search of such a step finds nothing, which is fine.

## 3. Move the pointer

```
git -C tools/framework checkout --detach $TARGET
```

Unity and `dotnet` now see the new framework. If `client/Packages/manifest.json` needs a new module, the notes say so.

## 4. Carry out the steps

For each step, in order: run its search from the root of the project; no hits means nothing to do, write the step down as skipped; on
hits make the change it describes. Rules of the framework docs apply to the changes (`tools/framework/.claude/docs/`). Do not
work around a removed API with a local copy of it.

## 5. Build and test

1. Backend: `dotnet build backend/backend.slnx` (the solution of the project if it is named otherwise).
2. Client: compile in the editor through the `control` skill when the project has it, otherwise `Unity -batchmode -projectPath client
   -quit -logFile -` and read the errors in the log. A changed `ContainerGenerator.dll` recompiles every assembly: the first compile is long.
3. Tests: `tools/scripts/run-tests.sh` when the project has it, otherwise the test projects of the backend and EditMode tests of Unity.
4. Run the checks of behaviour from the notes that an automatic test does not cover. What only a person can check goes to the report.

An error no step explains: fix it in the game, then write it in the report under "missing in the notes" with the search and the
change, so that the notes get fixed. A framework bug: stop and report it, do not patch the submodule from here without being asked.
A failing test stays a failure in the report even when it looks unrelated; check it on the old pointer before calling it old.

## 6. Link the shared skills

The `skills` part of `status.sh` compares the skills of the target with `.claude/skills` of the project (a new skill of the
framework needs no step in the notes to reach a game):

- `MISSING links`: ask the user which of them the game takes (one question, every name with its description from its `SKILL.md`;
  in `/auto` link all of them). Link each taken one:
  `ln -s ../../tools/framework/.claude/skills/<name> .claude/skills/<name>`. Its `SKILL.md` names what the game must give it
  (`.claude/project-skills.md` § `<name>`, a json of the game, a permission): add that, or write it in the report as left to do.
  The refused ones go to `.claude/project-skills.md`, section `## update-framework`, on one line `` Not linked: `a`, `b` `` (the
  reason after the names), so the next update does not ask again.
- `COPIES instead of links`: a folder of the game under the name of a shared skill. Diff it with the skill of the framework; what
  only the game has goes to `.claude/project-skills.md` § `<name>`, then the folder is replaced by the link.
- `BROKEN links`: the skill left the framework; the notes name its replacement. Remove the link.
- `links elsewhere`: leave them, name them in the report.

## 7. Commit

Commit the pointer and the fixes together only when the build and the tests pass, and only your own files:

```
[Framework] <version>: <what changed in the game>
```

Do not push unless asked. If the build or tests stay red, do not commit: leave the work in the tree and report what is left.

## 8. Report

- From which pin to which version, how many commits.
- Steps done and skipped, by number.
- Skills linked, refused (`Not linked`), copies replaced.
- Build and test results, with the failures.
- What a person must check, what is missing in the notes, the stash if one was made.
