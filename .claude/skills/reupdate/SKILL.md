---
name: reupdate
description: Synchronize this repository's Context chunk system with current project files and confirmed design decisions. Use after implementation or asset changes, or when asked to refresh context, repair drift, validate ownership or indexes, update operations, or clean active project state.
---

# Reupdate

Keep `Context/` concise, current, and traceable to the project.

## Procedure

1. Read `Context/Chunks/INDEX.md`, `system.md`, `STATE.md`,
   `CONVENTIONS.md`, `Context/operations.md`, and all chunk frontmatter.
2. Record `HEAD` and inspect committed, staged, unstaged, and untracked changes.
   Preserve unrelated user work.
3. For each chunk with `owns` entries, compare matching files from its
   `verifiedAtCommit` to `HEAD`, then inspect matching staged, unstaged, and
   untracked files. Convert clean `**` globs to Git pathspec syntax at command
   time. An unknown verification commit makes the chunk stale.
4. For each stale chunk, read the owned files:
   - Update the body when behavior, responsibility, intent, or a trap changed.
   - Re-verify without a body rewrite when the recorded contract still holds.
   - With uncommitted owned changes, keep the marker at a known commit and
     report the chunk as working-tree-current rather than commit-fresh.
5. Update `owns: []` design chunks only from confirmed user decisions or
   project evidence. Never manufacture ownership or resolve ambiguity by
   guessing.
6. Compute changed product files not matched by any `owns` glob. Assign each to
   an existing chunk, create a focused chunk in the right topic, or explicitly
   record the recurring category as intentionally unowned in `system.md`.
7. Run all integrity checks:
   - Every topic chunk appears in exactly one correct topic index.
   - Every index link resolves and topic nesting is one level deep.
   - Every `related` ID resolves to an existing chunk.
   - `chunk` IDs match filename stems.
   - Every chunk is at most 150 lines.
   - `STATE.md` contains only active hazards and open work, with frontmatter IDs
     matching its body.
8. Update `Context/operations.md` only with commands and stack facts verified
   from the project. Omit unknown fields instead of guessing.
9. Re-read edited files, repeat integrity checks, and report updated,
   re-verified, created, stale, owned, and intentionally unowned items.

## Rules

- Edit only under `Context/` unless the user explicitly requests broader work.
- Product files remain ground truth when context disagrees.
- Preserve concise design intent; do not copy source listings into chunks.
- Do not create or maintain a task ledger.
- Do not commit, push, or switch branches unless explicitly requested.
