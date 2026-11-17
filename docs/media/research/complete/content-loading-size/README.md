# Content loading size (2026-11-09, Unity 7000.0.0a10)

Measurements of the branch content-loading-size (deleted 2026-11-17): Addressables bundles and content directories
over the aggressive stripping variant, with and without the size optimization (`*-size`). Content directories were
frozen; AssetBundles went to main in 0.8. RESULTS.md is the generated report of the branch (uncommitted rows included),
ANALYSIS.md the section the branch added, results/ and variants/ the raw data and the variant definitions.

Commits of the branch:

- 3ac4cbb2 [Measurements] Synchronous Loadable.Load() on the web: a directory preloaded into the .data and the files of a fetched one
- a8f3b692 [Measurements] One content directory fetched per load: deduplication inside a directory, root first, zero placeholders
- 94458ce6 [Measurements] Content directories: scenes, dependencies, Loadable, the cache of the page
- fdf4d977 [Measurements] Content directory fetched at runtime on 7000.0.0a10
- ffd86c77 [Client] [Measurements] Content loading over aggressive: Addressables bundles and content directories
