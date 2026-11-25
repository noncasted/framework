# Render utilities of pixel-swarm that may move into the framework

A task for later, written on 2026-11-04 (the user). Not started: first decide where they belong in the framework and whether other games need them.

## Baking tilemaps into static meshes
- What: pixel-swarm step 39 (`03b14144`): `GamePlay/Arena/StaticTilemaps/{StaticTilemaps,TilemapMeshes,TilemapTextureMesh,TilemapMeshMaterials}.cs`,
  `StaticTilemapsSetup` in `LocationAssembler`, the keyword `_SPRITE_MESH`, the tests `StaticTilemapsTests` (tiles and sorting as in Chunk).
  When a location loads, the `TilemapRenderer`s are replaced by static meshes per material and sorting.
- Why: on Android (Mali, ANGLE) the tilemaps took ~25-30% of the render CPU (TilemapRendererGeometryJob 17%, culling of chunks), -3..-4 ms.
  Tilemap SRPBatch was tried: culling x3 worse.
- Question: where. Options: a module `rendering` / `tilemaps` (no new one exists), an editor tool (baking into an asset at build time instead of at runtime),
  part of a `LocationAssembler`-like tool of the framework, if there is one. Check which games have a `Tilemap`
  (`git -C <game> grep -l TilemapRenderer HEAD -- client/Assets`).

## Many quads in one mesh (QuadMesh)
- What: `GamePlay/Services/QuadMesh/QuadMesh.cs` (step 40, `7c7ea5c8`); the swarm shadows run on it (`SwarmShadows`, 10080 -> 1680 vertices) and
  the pixel particles (`PixelQuads`); sparks in one mesh (step 18). The proposal of step 43: the swarm bodies in one mesh instead of ~300 SpriteRenderers (-3..-4 ms on a phone).
- Traps: `Mesh.SetVertexBufferParams/SetVertexBufferData` crashes the WebGL player (memory access out of bounds) - only SetVertices/SetColors/
  SetUVs/SetIndices; `unity_SpriteColor` is 0 for a MeshRenderer -> the keyword `_SPRITE_MESH` in the shader.
- Question: the module (`animations`? a new `rendering`?) and whether other games need it.

## IL2CPP OptimizeSpeed
- pixel-swarm step 20 (`b9a7ebda`): wasm.br 3.51 -> 3.90 MB (+11%), build 201 -> 281 s; the CPU gain on devices is not measured.
- The user (2026-11-04): most likely dropped - the build size matters more. Decision: measure the CPU before / after on the stand; if there is no gain, return
  OptimizeSize in pixel-swarm. Do not move it into the framework.
