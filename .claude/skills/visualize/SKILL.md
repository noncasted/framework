---
name: visualize
description: For visual tasks - before changing the project, produce N distinct variants of the visual (sprites, icons, UI, palettes, effects, scene looks), compose them into one labelled contact sheet PNG (and a GIF when animated) and let the user pick. Use when the user runs /visualize [N] [conditions / style], or asks for visual options, variants, mockups or a choice of look before implementation.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /visualize, visualize, варианты визуала, покажи варианты, сделай N вариантов, мокапы, на выбор
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Visualize Skill

`/visualize [N] [conditions / style]` - the visual is **not** put into the project first. Make N variants, compose
them into one sheet, show it, and wait for the choice. Only the chosen variant goes into the project.

- `N` - number of variants, default 4 (more than 9 - split into several sheets of up to 9).
- conditions / style - everything else in the message: what to draw, palette, mood, references, limits. The subject
  may also come from the current task (`docs/tasks/current/<task>/`) or the conversation.

---

## Step 1 — Brief

1. Collect the constraints that always apply (do not repeat them to the user, just obey):
   - the look of the project: `.claude/project-skills.md` § visualize;
   - the real size in game (px, PPU) - variants are drawn at that size, the sheet only upscales them.
2. Find what exists: the current asset / screen (a render or the file), earlier research in
   `docs/media/research/<topic>/` and `docs/art`, so the variants do not repeat rejected ones.
3. Define the **axes of difference**: each variant must differ in a meaningful decision (silhouette, composition,
   colour scheme, frame style, motion), not in noise. Write one line per variant: label, idea, what it tests.
   With conditions given, all variants obey them and differ inside them.

## Step 2 — Output location

- Task in work (`docs/tasks/current/<task>/` exists and the visual belongs to it) -
  `docs/tasks/current/<task>/visualize/<topic>/`.
- Otherwise - `docs/media/research/<topic>/` (`<topic>` lowercase with underscores; if the folder exists, add
  `_v2`, `_v3` instead of overwriting).

Inside: `<topic>_variants.png` (+ `.gif`), the variant sources `<label>_<slug>.png` (or `<label>_<slug>/` frames),
the generator scripts if any (`*.py`, like the earlier research folders), and `README.md`.

## Step 3 — Make the variants

Pick the tool by the subject (several can mix):
- hand / exact pixel art (icons, UI frames, tiles, small sprites): a Python script (numpy, `magick`) or
  `mcp__pixellab__pixelart_workbench` - draw deterministically, keep the script next to the output;
- AI pixel art (characters, objects, concept): `mcp__pixellab__*` generators - then fix palette and size
  (`reduce_colors`, own palette mapping); check balance with `get_balance` before large batches;
- effects / animation: `mcp__pixellab__vfx_workbench`, frame folders;
- the look of a scene / screen in game: Unity renders (`.claude/docs/UNITY_EDITOR_WORKFLOW.md`: Play without
  `OpenScene`, screenshots to `client/Temp/Screenshots`), one render per variant setup. Variants of materials, lights
  or prefabs are made in memory or on temporary copies and **reverted** after the capture - the project stays
  unchanged until the choice.
- a scene built by `/recreate-scene`: make the variants by its Step 8 (edits of the spec / recipes / palette
  on temporary copies, an albedo render each, the reference in the first cell of the sheet).

Independent variants may be made by parallel agents (one variant each, the same brief from Step 1, disjoint output
files; only one agent may use the Unity editor).

Self-check each variant before composing: look at it at 1:1 and upscaled, compare with the brief and the in-game
reference; redo the weak ones instead of padding the sheet.

## Step 4 — Compose

```
python3 .claude/skills/visualize/compose.py --out <dir>/<topic>_variants --title "<topic>: <short brief>" \
    "A=<dir>/A_<slug>.png:<caption>" "B=<dir>/B_<slug>/:<caption>" ...
```

- A variant is a PNG, a GIF or a folder of frame PNGs. Any animated variant makes a synced `<topic>_variants.gif`
  next to the PNG (static variants hold still in it).
- `--scale` 0 (default) picks the largest integer upscale (point filter) that keeps a tile within 384 px; set it
  explicitly to match the in-game zoom. `--cols` for the grid, `--delay` for the GIF speed.
- Captions: 2-4 words of the idea, not the file name.
- When context matters (an icon on the HUD, a sprite on the arena floor), compose each variant on its real
  background first, so the sheet shows them in place.

Read the result PNG yourself before showing it: labels readable, nothing cropped, variants comparable in scale.

## Step 5 — README and choice

`README.md` in the output folder:

```markdown
# <topic> - варианты

Бриф: <условия, палитра, размер>
Лист: <topic>_variants.png[, <topic>_variants.gif]

| Вариант | Идея | Чем отличается | Исходник |
|---------|------|----------------|----------|
| A | ... | ... | A_<slug>.png |

Выбор: <пусто до ответа пользователя>
```

Then show the sheet (read the PNG so it is in the conversation), give one line per variant and a recommendation
with the reason, and ask which one to take (a mix is fine: "B, но рамка из D"). Do not implement before the answer.

Under `/auto` the user is not there: take the recommended variant, record the choice and the alternatives in the
decisions file (`auto` skill), and continue - the sheet stays for the user to overrule.

## Step 6 — After the choice

1. Write the choice into `README.md` ("Выбор: B, рамка из D").
2. Implement only the chosen variant in the project (assets, prefabs, settings), following the task's usual rules.
3. Verify it in game (render) against the variant on the sheet, and show the before / after.
4. Keep the sheet and the sources (the user may come back to the alternatives); delete only temporary renders from
   the scratchpad.

---

## Rules

- No project changes before the choice (temporary edits for renders are reverted).
- Variants differ in decisions, not in noise; each obeys the brief and the project's art rules.
- One sheet per choice, labelled A, B, C..., at a comparable scale; GIF when anything moves.
- Files go next to the task or to `docs/media/research/<topic>/`, never into `client/Assets` until chosen.
- Prose in Russian, file names and identifiers in English.
