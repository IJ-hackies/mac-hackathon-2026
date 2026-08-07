---
stack:
  - Unity 6000.3.10f1
  - Universal Render Pipeline 17.3.0
  - Input System 1.18.0
projectRoot: ./
---

# Operations

The repository root is also the Unity project root; open
`mac-hackathon-2026/` directly in Unity Hub. It uses the Universal Render
Pipeline with separate template PC and mobile renderer assets, Force Text asset
serialization, and the new Input System. `Assets/Scenes/SampleScene.unity` is
currently the only enabled build scene.

No repeatable project-level build or test command has been established, and
target platforms remain undecided. A one-off Unity batch-mode check using the
local package cache has successfully recognized the repository root, loaded
both prototype scenes, and confirmed the build-scene configuration; its
temporary verifier is not a shared project workflow. Development is shared by
two people and will use separate Git branches and worktrees for parallel tasks.
