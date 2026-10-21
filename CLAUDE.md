# Framework

Shared base of the game projects (mines-leader, peace-data-reborn, storytale-workshop, ...): client core, modules,
platforms, forked Unity packages, shared protocol and backend infrastructure.

Every project consumes this repository as a git submodule at `<project>/tools/framework`:
- Unity: `client/Packages/manifest.json` -> `"file:../../tools/framework/client/..."`
- backend: `ProjectReference` -> `../tools/framework/backend/...` (relative to the csproj)
- shared: `ProjectReference` / UPM `file:` -> `tools/framework/shared`

## Layout

```
shared/                 protocol and types used by both the Unity client and the backend
client/
  core/                 DI container (+ ContainerGenerator), reactive, lifetimes, scopes, common tools
  flow/                 base of game loop, loaders and mocks (concrete mocks and the startup scene stay in the project)
  modules/              optional modules: publish, catalogues, network, browser, audio, inputs, backend, ui, ...
  platforms/            platform implementations of modules/publish: web, website, itch, telegram, steam
  packages/             forks of third-party packages (Unity, MemoryPack, unity-mcp), see FORK.md in each
backend/
  Common/ Infrastructure/ Cluster/ Console/
tools/scripts/          scripts shared by the projects
```

## Rules

1. **No project code here.** Nothing in the framework references a game assembly, a game namespace, game assets or
   game paths. A project plugs in through interfaces, registration extensions and settings assets. Generated
   content (catalogues, `ContainerGraph.AssetMap.cs`, local storage keys, options assets) is written into the project.
2. **Modules are optional.** `core` knows nothing about modules; a module depends on `core` (and on other modules only
   when unavoidable). A project includes only the modules it lists in its manifest.
3. **Forks keep their history readable.** Each forked package starts with a pristine upstream commit; every local
   change is a separate commit with the reason, and `FORK.md` lists the patches. To update a fork: commit the new
   pristine version, then reapply the patch commits.
4. **Built artifacts next to their sources.** Code built outside Unity (ContainerGenerator, trimmed MemoryPack, native
   editor plugins) keeps its sources in `Source~/` of the package that ships the result; the build script writes into
   that package; the result is committed together with its `.meta`.
5. **Keep `.meta` files.** Assemblies reference each other by asmdef GUID; plugin importer settings (`RoslynAnalyzer`
   label, disabled platforms) live in `.meta`. Never regenerate them when moving files.
6. **Comments in English.** Prose with the user in Russian.
7. **Fail fast.** No silent fallbacks, no defensive null checks; unexpected state throws.
8. **A change here affects every project.** After a change, build at least the project you are working in; mention in
   the commit message when a change is breaking (`BREAKING:`).

## Working from a project

The submodule is a normal git repository: commit and push inside `tools/framework`, then commit the new submodule
pointer in the project. Other projects pick the change up with `git submodule update --remote tools/framework`.
