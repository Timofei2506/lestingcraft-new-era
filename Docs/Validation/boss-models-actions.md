# Boss models: separate Blender build in GitHub Actions

**Status: art candidates for user inspection, not approved playable bosses.** The user allows voxel foundations from the first rejected Mars/Venus/Mercury silhouettes **only if** the new models have convincing anatomical detail, genuine textured geometric relief, transparent materials where appropriate, articulated body parts, convincing eyes and localized effects. The first untextured models (`2a43bc1`) remain reverted (`30171ec`); none of these bosses spawns in the game.

## Source and reproducibility

- `ArtSource/BossModels/Blockouts/{Mars,Venus,Mercury}.json`: the **old silhouette data explicitly requested as the starting point**. Not Unity assets and not final art.
- `Tools/build_boss_candidates.py`: authors three independently pivoted multi-part game meshes by increasing block resolution, carving surface relief/strata and remodeling some anatomical features. Uses *the game's real PBR atlas sources* (`Assets/Art/PBR/`): albedo, normal and mask; adds eye sockets/corneas/irises/pupils, Venus transmissive glass chambers, metallic mirrored Mercury fins and controlled local emissive channels. Generates editable `ArtSource/BossModels/Candidates/*.blend`, `*.glb` and diagnostic previews.
- `.github/workflows/boss-models.yml`: **separate from the Windows game build**. The source-file push trigger loads the candidate `.blend` files with pinned Blender 4.3.2, verifies their checksum-matched exporter, and runs `Tools/boss_art_ci.py` for Mars, Venus, Mercury. Output is a GitHub Actions artifact with `.fbx`, `.glb`, neutral front/side/back 3D renders and a manifest. Nothing enters `Assets/` until the user reviews it.

The CI checker verifies articulated parts, UVs, genuine linked material textures, normal maps, translucency on Venus and 3D eye elements. Passing these checks is **not** a guarantee of appearance or of Unity readiness. The candidate renders must still be inspected by the user; after approval, imported FBX materials/transparent shaders must be tested in actual Unity and the Windows executable. There are no combat animations, AI, planet content or natural boss spawns yet. Boss encounters remain restricted to **planetary surfaces other than Lestingia or the Moon**.

The previously reported Unity smoke failed on `ArgumentException` (duplicate voxel coordinate) because the old JSON geometry was passed through the Guardian mesher. This review pipeline exports authored mesh parts via Blender instead; do not revive the old JSON runtime loader.
