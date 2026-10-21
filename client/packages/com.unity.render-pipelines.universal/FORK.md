# FORK: com.unity.render-pipelines.universal 17.7.0

Base: built-in package of Unity 7000.0.0a10. Removed from the base: `Tests`, `Documentation~`, `Samples~`.
Only for projects on URP; projects with their own SRP do not reference it.

## Patches

| File | Why |
|---|---|
| `Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef` | no `ENABLE_UIELEMENTS_MODULE` versionDefine |
