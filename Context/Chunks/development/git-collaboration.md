---
chunk: git-collaboration
title: Two-person branches and worktrees
owns: []
related: [system, unity-project]
verifiedAtCommit: 8a7f1dd273e1c329ecd10e4219ebdef8bd06b620
lastVerified: 2026-08-07
---

## What this is

The game is a two-person project. Concurrent tasks use separate Git branches
and worktrees so contributors do not share one mutable checkout.

## Invariants

- Each concurrent task or contributor works in an isolated worktree on its own
  branch.
- Preserve the other contributor's commits and uncommitted work; do not reset,
  overwrite, or silently absorb unrelated changes.
- Integrate work through Git history rather than copying changed files between
  worktrees.
- Coordinate ownership before parallel edits to conflict-prone Unity assets,
  especially scenes, prefabs, package manifests, and project settings.

## Undecided workflow

Branch naming, the integration branch, merge versus rebase policy, pull-request
or review requirements, commit granularity, worktree location, and conflict
resolution rules have not been selected.

## How to extend

Before parallel implementation begins, replace the undecided list with the
agreed branch lifecycle and exact worktree commands. Once Unity is initialized,
record how `.meta` files and serialized assets are reviewed and merged.
