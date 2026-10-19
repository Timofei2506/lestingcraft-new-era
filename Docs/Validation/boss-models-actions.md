# Boss models in GitHub Actions — separate from the Windows game build

The user allows **voxel geometry if exceptionally well made**, with real varied textured relief, transparent elements where applicable, expressive eyes with iris/pupil and anatomically distinct articulated parts. Mars, Venus and Mercury remain **planet-surface-only** bosses. Old meshes at `2a43bc1` were rejected and reverted at `30171ec`; the trial re-render of these same shapes with PBR materials **was also rejected internally**. Neither is approved game art.

`.github/workflows/boss-models.yml` is the separate **model art build**. It takes *authored and approved* Blender source files from `ArtSource/BossModels/Approved/{Mars,Venus,Mercury}.blend` and produces separate actual `.fbx`, `.glb`, front/side/back PNG renders and a geometry/material manifest per boss. Outputs are uploaded as a GitHub Actions artifact for review; the workflow does not add boss encounters or content to the Unity build.

## Fail closed, not fake success

There are currently **no approved Blender sculpts**, so this workflow cannot yet produce a valid boss build. It refuses to run on old voxel JSON, generated placeholders or 2D concepts. Do **not** dispatch it just to obtain a failed Actions badge. Once models exist, adding the three `.blend` source files triggers the build automatically; `workflow_dispatch` can also be run manually.

`Tools/boss_art_ci.py` checks:
- distinctly named anatomy: four arms on Mars, six legs and a reservoir on Venus, six legs and four mirror plates on Mercury, with separate eye components;
- textured 3D relief / sufficient polygons, UV mapping, at least four materials and at least three normal-mapped materials;
- at least nine image textures per boss and no unresolved image files;
- actual transparent/transmissive Venus reservoir material, not an opaque green tint;
- source mesh exported to FBX and GLB and the **same source geometry** rendered from three viewpoints.

These are *technical* gates, not a claim of artistic quality. The user must approve the actual preview and the game-engine appearance before importing a boss. CI executes Blender; it cannot transform a weak blockout into a detailed professional sculpt on its own.
