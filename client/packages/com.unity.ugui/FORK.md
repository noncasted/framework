# FORK: com.unity.ugui 7.0.0

Base: built-in package of Unity 7000.0.0a10. Removed from the base: `Tests`, `Documentation~`, `Samples~`.

## Patches

| File | Why |
|---|---|
| `Runtime/UGUI/UnityEngine.UI.asmdef` | no `PACKAGE_PHYSICS`, `PACKAGE_PHYSICS2D`, `PACKAGE_ANIMATION`, `PACKAGE_UITOOLKIT` versionDefines: UGUI does not pull these modules into the player. Physics raycasters and `Animator`-driven selectable transitions are compiled out; restore the define if a project needs them |
