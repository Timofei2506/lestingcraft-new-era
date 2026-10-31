# Architecture and concurrent meshing — 31 October 2026

## Shipped in this stage

### New single-player worlds: content 11
New local saves/worlds use content 11; existing saves retain their stored version, and the loader accepts versions 1–11. This is **not a network-protocol bump**. GameRules remains 14; rules-15 server world-boss/fauna integration is still pending. Network rooms keep their server-approved content version. Do not change rules/content constants alone to claim full multiplayer migration.

The ten expedition structure types now have separately authored content-11 geometry:
- village: four timber-framed houses with glazed windows, shingled roofs, hearths/chimneys, beds, bookshelves, workstations, well pavilion and street lanterns;
- border castle: curtain walls, crenels, four stair towers, gatehouse, two-storey keep with a traversable staircase and upper gallery;
- ruined manor, broken watchtower and abandoned shelters: distinct damage/openings and usable interiors;
- guardian obelisk: stepped dais, approach, perimeter pillars and luminous core;
- mining camp: braced adit, descent, hut, work area and rail approach;
- stone circle, ruined library and caravan post: distinct footprints, open circulation and authored details.
New mines include a rail spine and illuminated work alcoves; legacy mine blocks are unchanged.

`Tools/WorldGen/architecture.py` is the only new geometry source. `Tools/build_architecture.py` compiles it to deduplicated vertical runs shared by C# and JavaScript. Runtime sampling visits only the few runs in the current column, not every box in a building. This keeps the architectural detail from multiplying the chunk-generation cost.

The C# frozen structure catalog now also contains all ten existing server plans (previously only five). Version-aware placement is passed through both the chunk job and point sampler, NPC site selection and discovery tracking. Old plans 0–4 remain byte-identical. The old content-9 C# uranium path is retained for existing local saves; content >=10 uses the server uranium hash/range.

The old expedition authoring tool now refuses to rewrite frozen legacy plans and delegates catalog compilation instead of silently deleting the newer five plans.

### Streaming
Bounded concurrent mesh work (2–4 routines/tasks) replaces the single mesh task. Snapshots and delegate state remain private to each build; Unity mesh upload and collider assignment remain on the main thread. Dirty revisions discard stale results. World switches/teardown dispose routines, observe faults, and never recycle a snapshot still read by a worker. Synchronous showcase pumping drains the same workers, rather than using a second generation path.

Online wildlife display no longer silently discards content-8 species 12–19 and content-9 species 20–29. This is a display limit fix, not the unfinished rules-15 species migration.

## Verification
- Server: 245 unit tests pass (including four architecture/gating/mine tests).
- 150,000 C# / JS / independent ordered-box geometry probes pass.
- All marked entrances have >=2-block width and 3-block clear height.
- A two-block-clear voxel topology path reaches the keep gallery from the entry via the stair. This is not a CharacterController physics run.
- Frozen legacy plan counts and reproducible generated tables checked.
- Existing pure C# core and art/content/render source contracts pass.
- Added Unity EditMode regression: eight concurrent greedy meshes must exactly match serial vertices/normals/UVs/colors/tangents/triangles and leave source chunks unchanged. Execution requires Unity CI; local checks do not substitute for it.

## Still pending
- Native Unity validation and target-device streaming timings, including teleport/unload stress.
- Rules-15/16 network migration with all species and world-boss handling in lockstep.
- Story-location architecture and planetary landmark overhaul.
- Detailed mob/boss/planet visual work, full PBR/cloud pipeline upgrade and complete screen layout redesign.
