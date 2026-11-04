# Render utilities of pixel-swarm that may move into the framework

Задача на будущее, записана 2026-11-04 (пользователь). Работа не начата: сначала решить, где во фреймворке им место и нужны ли они другим играм.

## Запекание тайлмапов в статические меши
- Что: pixel-swarm шаг 39 (`03b14144`): `GamePlay/Arena/StaticTilemaps/{StaticTilemaps,TilemapMeshes,TilemapTextureMesh,TilemapMeshMaterials}.cs`,
  `StaticTilemapsSetup` в `LocationAssembler`, keyword `_SPRITE_MESH`, тесты `StaticTilemapsTests` (тайлы и сортировка как у Chunk).
  При загрузке локации `TilemapRenderer` заменяются статическими мешами по материалу и сортировке.
- Зачем: на Android (Mali, ANGLE) тайлмапы стоили ~25-30% CPU рендера (TilemapRendererGeometryJob 17%, cull чанков), -3..-4 мс.
  Tilemap SRPBatch пробовали: cull x3 хуже.
- Вопрос: куда. Варианты: модуль `rendering` / `tilemaps` (нового нет), инструмент редактора (запекание в ассет при сборке вместо рантайма),
  часть `LocationAssembler`-подобного инструмента фреймворка, если такой будет. Проверить, у каких игр есть `Tilemap`
  (`git -C <game> grep -l TilemapRenderer HEAD -- client/Assets`).

## Много квадов одним мешем (QuadMesh)
- Что: `GamePlay/Services/QuadMesh/QuadMesh.cs` (шаг 40, `7c7ea5c8`), на нём тени роя (`SwarmShadows`, 10080 -> 1680 вершин) и
  пиксельные частицы (`PixelQuads`); искры одним мешем (шаг 18). Предложение шага 43: тела роя одним мешем вместо ~300 SpriteRenderer (-3..-4 мс телефон).
- Ловушки: `Mesh.SetVertexBufferParams/SetVertexBufferData` роняет WebGL-плеер (memory access out of bounds) - только SetVertices/SetColors/
  SetUVs/SetIndices; `unity_SpriteColor` = 0 у MeshRenderer -> keyword `_SPRITE_MESH` в шейдере.
- Вопрос: модуль (`animations`? новый `rendering`?) и нужно ли другим играм.

## IL2CPP OptimizeSpeed
- pixel-swarm шаг 20 (`b9a7ebda`): wasm.br 3.51 -> 3.90 МБ (+11%), сборка 201 -> 281 с; выигрыш CPU на устройствах не измерен.
- Пользователь (2026-11-04): скорее откажемся - размер сборки важнее. Решение: замерить CPU до/после на стенде; если выигрыша нет - вернуть
  OptimizeSize в pixel-swarm. Во фреймворк не переносить.
