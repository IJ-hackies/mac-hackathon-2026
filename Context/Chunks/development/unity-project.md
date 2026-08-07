---
chunk: unity-project
title: Unity project foundation
owns: []
related: [system, control-model, git-collaboration]
verifiedAtCommit: 8a7f1dd273e1c329ecd10e4219ebdef8bd06b620
lastVerified: 2026-08-07
---

## What this is

Unity is the confirmed engine for the 2.5D space game. A Unity project has not
yet been initialized, so there are no engine-owned files or verified editor
commands to record.

## Undecided bootstrap choices

- Unity editor version and whether it is pinned through Unity Hub.
- Render pipeline and the visual approach used to achieve 2.5D presentation.
- Whether gameplay physics use Unity's 2D or 3D systems.
- Input package and the abstraction for cooperative versus single-player maps.
- Initial platform targets, package set, assembly layout, testing, and builds.

## How to extend

When the project is created, record the chosen version and packages, verified
open/build/test commands, repository layout, serialization settings, and
project-wide conventions. Add the concrete `Assets`, `Packages`, and
`ProjectSettings` paths to appropriate focused chunks rather than assigning the
entire Unity tree here by default.
