---
name: media-cleanup
description: Clean up after the session in docs/media and docs/media/research - delete the extra files it left and move its finished research to docs/media/research/complete. Use when the session has finished its task and created files in docs/media, when the user runs /media-cleanup, or says to clean up the media.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /media-cleanup, media-cleanup, почисти media, убери за собой в media
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Media Cleanup

Only the work of this session. A folder of another session or of an unfinished task stays as it is, even if it looks forgotten.
No `docs/media` or nothing of this session in it: say so and stop.

1. **Find the work.** `git status --short docs/media` and the files this session wrote or changed. Look at a file before deleting it.
2. **Keep the main results:** before/after, the sheet of variants, the README with the choice, data that code, tests or tools read
   (`git grep` the name of the folder).
3. **Delete the rest:** raw frames, logs, one-off shots, copies, rejected variants that the sheet already shows. A committed file
   stays in the git history; an untracked one is gone for good, so delete it only when it is one of the above.
4. **Loose files.** A file of this session directly in `docs/media` or `docs/media/research` goes into the folder of its topic or is
   deleted. `docs/media/reference` and the other folders outside `research` are not touched.
5. **Move the finished work.** `docs/media/research/<topic>` -> `docs/media/research/complete/<topic>`, with `git mv` for tracked
   files. A topic whose task is still going on stays in `research`.
6. **Rewrite the paths.** `git grep` the old path, relative paths of the scripts in the folder included, and fix every one. If tests
   read the folder, run them.
7. **Report** in one or two lines: what moved, what was deleted, what stayed and why. Commit only if the task asks for commits.
