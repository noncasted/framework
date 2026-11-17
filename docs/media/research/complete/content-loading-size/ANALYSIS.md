## Content loading on 7000.0.0a10: content directories against Addressables

Over `aggressive` (2,614): the sprite and the clip are taken out of the scene and loaded at runtime. Total = the
download and the files of StreamingAssets the page fetched (raw, as served).

| Variant | Download | StreamingAssets fetched | Total | Against `aggressive` |
|---|---:|---:|---:|---:|
| `content-directory` (engine API, preloaded into the .data) | 2,684 | - | **2,684** | **+70** |
| `content-directory-remote` (engine API, fetched when asked) | 2,689 | 30 | **2,719** | +105 |
| `content-directory-scenes` (two directories, one with a scene) | 2,695 | 622 (raw; 99 br) | **3,316** | +703 |
| `content-directory-single` (one directory, fetched per load) | 2,710 | 592 (raw; 84 br) | **3,302** | +689 |
| `addressables-directories` | 3,028 | 2 | **3,030** | +416 |
| `addressables-bundles` | 3,023 | 20 | **3,044** | +430 |

- **Addressables costs about 0.4 MB, whatever it loads from.** The managed code after the linker grows by 614-625 KiB
  raw: `Unity.ResourceManager` 148, `Unity.Addressables` 122, `System` 133 and `mscorlib` 66 (what they call of the
  BCL), the modules it depends on (`UnityWebRequest` 34, `AssetBundle` 21, `UnityWebRequestAssetBundle` 19,
  `JSONSerialize` 18). The wasm is +294-301 KiB br, the IL2CPP metadata +105-114 KiB br.
- **A content directory group of Addressables saves only the bundles (~14 KiB).** Its files go into the .data, but
  the settings and the catalog are still fetched with UnityWebRequest, and the package keeps its AssetBundle and web
  request code in the build: the cost of the code does not change.
- **The engine API alone costs 70-80 KiB**: `UnityEngine.ContentLoadModule` and its part of the BCL (`System` +68 KiB
  raw after the linker), +46-57 KiB of wasm. Preloaded, the files (32 KiB raw here) are in the .data under
  `/vfs_streamingassets/` through a WebGL preload manifest, read synchronously, no request: the content was there in
  13-17 ms against 128-135 ms through Addressables.
- **A content directory can stay on the server and be loaded when asked, without Addressables.**
  `RegisterContentDirectory` reads only a local path (the native file reads go through the emscripten file system), so
  the loader fetches the files into that file system (`WebFileDownload.jslib`: `fetch`, `FS.writeFile`) and registers
  the copy: 7 requests, the content in ~100 ms. The download code is ~13 KiB of wasm and ~20 KiB raw of managed code over
  the preloaded variant. The files are served raw by the stand (no `.br` next to them); a host that compresses them
  sends about half.
- **The cache of the page works**: the jslib keeps every response in the Cache API, the second opening of the page
  (`smokeReload`) made no request for the content (0 fetched, 7 from the cache, 25 ms). Safe because every file is named
  by its content hash; the one file that is not (`BuildManifestHash.txt`) is written by the loader from the index the
  build gave it. Old hashes stay in the cache until something deletes them.
- **Scenes work**: a content directory takes only ScriptableObjects as roots (`The RootAsset '...unity' is not a
  ScriptableObject`); a root with a `LoadableSceneId` field brings the scene in (`LoadableScenes` of the manifest), and
  after the directory is registered `SceneManager.LoadSceneAsync(LoadableSceneId, Additive)` loads it on WebGL.
- **No dependencies between directories, everything is copied**: the level directory got its own copies of the
  content asset, the sprite, the texture and the clip, both through the direct reference of the scene and through
  the `Loadable<T>` of its object. At runtime they are other objects (the sprite of the scene is not the shared one,
  its `Loadable` gives another content asset): the memory holds both. The copies are byte for byte the same files (the
  same hash), so a store keyed by the hash alone would download them once; this one keys by the URL of the directory
  and downloaded 17 files.
- **Deduplication is inside one directory only.** Unity promises it for one build of a directory ("Content
  Directories" of the manual: shared dependencies are stored once within a build; separate builds each get their own
  copy, so the advice is one root, or a few, for the whole project). One directory Game with a root that holds the
  `Loadable<T>` of the content and the `LoadableSceneId` of the level has 10 files instead of 17: the level and the
  content share the files of the sprite and the texture, and at runtime they are the same objects (the sprite of the
  scene is the sprite of the content, the `Loadable` of the scene gives the same content asset).
- **One directory can still be fetched per load.** The registration lists the files of the folder once (a file
  written later is not found: `CAHFileSystem failed to find a file for hash`), hangs on empty files and opens the
  files of the roots at once (`File is either empty or corrupted` on a placeholder). So the loader fetches the
  manifest and the files of the root, writes the other files as zeros of their real size (the sizes come from the
  build), registers, and before each load writes over the zeros the files that load needs: root 2, level 4, content 3
  of 10 (the rest is shared with what came before). The files are read when an object is loaded, not when the
  directory is registered. The list of the files of a load is computed by the build from the manifest (the
  dependencies of a serialized file, and the `cah:/<hash>` names of the resources inside it); the format is not
  documented as stable. The whole lot came from the Cache API on the second opening (0 fetched, 10 from the cache).
- **A scene copies the built-in resources it uses**: the default sprite material, its shader and the default
  reflection cubemap (`unity default resources` / builtin extra): 554 KiB raw, 65 KiB br in the level directory,
  though the player has them. A scene with no skybox saved 10 KiB of it. In the single directory the same file
  is 554 of its 592 KiB raw. Every directory with such a scene ships them again, and the shader is another object,
  so its programs are made again.
- **`Loadable<T>` is not a replacement for `AssetReference` across builds**: inside a directory it loads (the copy of
  that directory); in the player scene a `Loadable` of an asset that only a content directory has comes out of the
  player build with the local id 0 (`Failed to load LoadableObjectId(... LocalID: 0): no registered content directory
  contains it`). A reference of a game would be its own serialized pair: the directory and the root asset.
- What the remote directories still lack against Addressables, for a game: the index of the directories written by
  the build (here baked into the loader), a store keyed by the hash, a remote base URL (a CDN instead of StreamingAssets
  is only another prefix), the removal of old hashes from the cache, the unload (`UnregisterContentDirectory` and the
  files of the file system), and in the editor the directory registered from `Library` without the download.
- The content of the scene is tiny (a 32 px sprite and a 0.5 s clip), so the numbers are the cost of the loading
  code and of what the directories copy.

