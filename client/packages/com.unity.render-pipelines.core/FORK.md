# FORK: com.unity.render-pipelines.core 17.7.0

Base: built-in package of Unity 7000.0.0a10 (`Editor/Data/Resources/PackageManager/BuiltInPackages`).
Removed from the base: `Tests`, `Documentation~`, `Samples~`.

## Patches

| File | Why |
|---|---|
| `Runtime/*.asmdef`, `Editor/*.asmdef` | no `ENABLE_PHYSICS_MODULE` versionDefine: the player does not pull the physics module through RP Core |
