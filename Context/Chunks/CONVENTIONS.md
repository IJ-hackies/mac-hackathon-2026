# Chunk Conventions

This is the project's selectively loaded working context. Code and assets are
ground truth; chunks preserve intent, invariants, decisions, and traps that are
not quickly obvious from files alone.

## Layout

- `Context/Chunks/INDEX.md` - root topic map and loading order.
- `Context/Chunks/system.md` - always-loaded identity and global invariants.
- `Context/Chunks/STATE.md` - always-loaded active hazards and open work.
- `Context/Chunks/CONVENTIONS.md` - this format and maintenance contract.
- `Context/Chunks/<topic>/INDEX.md` - a concise map of one topic.
- `Context/Chunks/<topic>/<chunk>.md` - focused project knowledge.
- `Context/operations.md` - verified commands, stack, and runtime information.

Use one topic-directory level only. This project does not use a task ledger.

## Chunk frontmatter

Every chunk except `INDEX.md` and `CONVENTIONS.md` carries:

```yaml
---
chunk: control-model
title: Human-readable title
owns:
  - "src/input/**"
related: [system]
verifiedAtCommit: <full-git-sha>
lastVerified: <YYYY-MM-DD>
---
```

- `chunk` must equal the lowercase kebab-case filename stem. `STATE.md` uses
  `chunk: state`.
- `owns` contains clean repository-relative paths or globs. Never store Git
  pathspec syntax such as `:(glob)` in frontmatter.
- More than one chunk may own a file when responsibilities genuinely overlap.
- `related` contains chunk IDs, not paths, and every ID must resolve.
- `STATE.md` also carries `landmines` and `openWork` ID lists matching its body.

## Body guidance

Use only the sections that add value:

```text
## What this is
## Key files
## Invariants
## How to extend
## Gotchas
```

Keep each chunk under 150 lines. Prefer contracts, design intent, lifecycle,
data flow, and failure modes over inventories that can be rediscovered quickly.
Do not present an undecided idea as an established fact.

## Freshness

For a chunk with owned files, compare those files between `verifiedAtCommit`
and `HEAD`, then inspect matching staged, unstaged, and untracked changes.
Convert `**` globs to Git pathspecs only when running Git commands.

- No owned-file changes: re-verification may advance the marker.
- Behavior or responsibility changed: update the chunk, then advance it.
- Unknown commit or changed owned files: treat the chunk as stale until checked.
- Uncommitted owned-file changes keep the chunk working-tree-current but not
  commit-fresh; report that state explicitly.

For design chunks with `owns: []`, update them when confirmed project decisions
change. Never invent source ownership merely to create a freshness signal.

## Definition of done

After a product change, update or re-verify every affected owning chunk. Every
changed product file must be owned by a chunk or explicitly recorded as an
intentional exclusion. Keep indexes, relationships, `STATE.md`, and
`operations.md` consistent.
