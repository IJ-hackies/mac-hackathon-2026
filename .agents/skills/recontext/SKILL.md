---
name: recontext
description: Load the minimum relevant project context before working in this repository. Use when starting or resuming implementation, planning, debugging, review, or any task that needs the game's architecture, decisions, open work, asset rules, or operating commands.
---

# Recontext

Orient the current task without loading the whole context tree.

## Procedure

1. Stop and report a missing context system if `Context/Chunks/INDEX.md` does
   not exist. Do not create it from this skill.
2. Load, in order: `Context/Chunks/INDEX.md`, `system.md`, `STATE.md`, and
   `CONVENTIONS.md`.
3. Compare each loaded chunk's `verifiedAtCommit` with `HEAD`. For a differing
   or unknown commit, inspect committed, staged, unstaged, and untracked changes
   matching its `owns` paths. Treat code and assets as ground truth. Report
   possible staleness; never repair it during recontext.
4. Route the task through the root index to the relevant topic index and
   focused chunks. Load direct `related` chunks only when they materially
   affect the task. Do not crawl relationships transitively.
5. Read `Context/operations.md` when the task involves running, building,
   testing, importing, or choosing tools.
6. Report loaded chunks, stale chunks, relevant invariants, active open work,
   and contradictions. Keep the report concise.

## Rules

- Recontext is read-only. Do not edit context or product files.
- Do not load every chunk by default.
- Do not guess missing architecture. Use the source tree and mark undecided
  items as undecided.
- This project has no task ledger; do not look for or create one.
