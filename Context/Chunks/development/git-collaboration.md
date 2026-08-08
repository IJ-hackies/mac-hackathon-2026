---
chunk: git-collaboration
title: Two-person branches and worktrees
owns: []
related: [system, unity-project]
verifiedAtCommit: db81cd848e59c29f89795a89d512b044041e215a
lastVerified: 2026-08-08
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
- Commit every Unity `.meta` file with its corresponding asset and preserve the
  project's Force Text serialization setting.
- Do not commit Unity caches, logs, local user settings, generated IDE files, or
  build outputs excluded by the repository root `.gitignore`.
- Coordinate ownership before parallel edits to conflict-prone Unity assets,
  especially scenes, prefabs, package manifests, and project settings.

## Undecided workflow

Branch naming, the integration branch, merge versus rebase policy, pull-request
or review requirements, commit granularity, worktree location, and conflict
resolution rules have not been selected.

## How to extend

Before parallel implementation begins, replace the undecided list with the
agreed branch lifecycle and exact worktree commands, including how serialized
Unity assets are reviewed and merged.
