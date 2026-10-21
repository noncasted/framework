# FORK: com.cysharp.unitask 2.5.11

Base: Cysharp/UniTask 2.5.11, `src/UniTask/Assets/Plugins/UniTask`.

## Patches

| File | Why |
|---|---|
| `**/*.meta` | folder GUIDs of the projects (they had UniTask in `Assets/Plugins`); asmdef GUIDs are upstream |
| `**/*.tt` | removed: T4 templates of code that is committed upstream |
| `Runtime/UniTask.asmdef` | no `UNITASK_PHYSICS_SUPPORT` / `UNITASK_PHYSICS2D_SUPPORT` versionDefines: the collision triggers kept the physics modules in the player; no project uses them |
