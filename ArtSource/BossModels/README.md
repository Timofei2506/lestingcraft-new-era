# Planet-boss model candidates — NOT in game

The user explicitly requested that the first rejected voxel boss silhouettes be used **as a starting point**, with markedly better textured relief, translucent elements, anatomical eye assemblies and articulated body parts. This directory contains the current 3D **review candidates**, not approved encounters and not Unity screenshots.

- `Blockouts/*.json`: the historical blockout silhouettes, kept OUTSIDE `Assets/` and never loaded by the game. The first Unity JSON implementation was reverted after the art was rejected and its Windows smoke failed.
- `Candidates/Mars.blend`, `Venus.blend`, `Mercury.blend`: editable, separate mesh parts with linked source PBR images from `Assets/Art/PBR`; each file can be opened in Blender 4.3.2.
- `Candidates/*.glb`: actual binary glTF game-art candidates with embedded material images. These are not instantiated by Unity yet.
- `Candidates/*-candidate.png`: real Cycles render of each mesh. The Blender export has smaller rock detail, UV tiling of actual game materials, embedded face cornea/iris/pupil and separate articulated limb meshes; Venus has real transmissive shell meshes, Mercury mirror fins have been remodeled. **Judge the appearance yourself; CI technical PASS is not user art approval.**
- `Tools/build_boss_candidates.py`: deterministic reconstruction from the initial silhouettes. Run from repository root: `blender -b -t 4 --python-exit-code 22 --python Tools/build_boss_candidates.py`.
- `.github/workflows/boss-models.yml`: separately builds those candidate sources as FBX/GLB and front/side/back preview artifact with pinned Blender and `Tools/boss_art_ci.py`. No Windows game build and no boss spawn.

Only after explicit approval should any of these enter `Assets/`; importing into Unity will require separate shader/material, animation, LOD and executable validation. Boss encounters remain restricted to surfaces of other planets, never moons/orbits/transit/asteroid fields.
