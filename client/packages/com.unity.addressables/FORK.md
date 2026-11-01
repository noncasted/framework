# FORK: com.unity.addressables 4.1.1

Base: registry package 4.1.1 (commit "Upstream com.unity.addressables 4.1.1").
Removed from the base: `Tests`, `Documentation~`, `Samples~`, osx/win binaries of `Editor/Tools~/AddressablesFormatConverter`
(only the legacy catalog format converter uses them; copy them from the registry package if a non-Linux machine needs it).

## Patches

| File | Why |
|---|---|
| `Editor/Build/DataBuilders/AddressableAssetsBundleBuildParameters.cs` | `ScriptCompilationOptions.DevelopmentBuild` is CS0619 on Unity 7000 and has no effect |
| `Editor/Build/DataBuilders/SchemaBuilders/BundledAssetSchemaBuilder.cs` | `AddIncludedTypes`: runtime `UnityEngine.Object` types from bundles go to link.xml with `preserve="nothing"` instead of `preserve="all"` (WebGL size) |
| `Runtime/ResourceManager/Util/TypeNameResolver.cs` | The editor has no handler in the process-wide `AppDomain.AssemblyLoad`, it looks through the loaded assemblies on a miss instead: the handler outlived the domain reload and kept each old script domain from unloading (200-300 MB of the editor per reload, measured). Players keep the event. Test: `client/core/Tests/Editor/Catalogues/TypeNameResolverUnloadTests.cs` |

See all patches: `git log --oneline -- client/packages/com.unity.addressables`.
