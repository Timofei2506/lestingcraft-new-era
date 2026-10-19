# Planetary boss models · asset milestone

Three **actual authored 3D game assets**, not concept art: `Assets/Resources/Models/PlanetBoss{Mars,Venus,Mercury}.json`. The Unity `PlanetBossVisual.Build(parent, planet)` loader creates surface-culled voxel meshes with palette-driven existing PBR texture-array layers, tint and emission, separate transform/pivot + per-part collider for each moving part, and restrained procedural idle animation. Geometry is created using the game's existing `GuardianSculpture.CreateMesh`, not one cube GameObject per voxel.

| Planet | Model | Physical extent, m (W×H×D) | Rig parts | Authored voxels |
|---|---|---:|---:|---:|
| Mars | Кузнец разлома | 8.06 × 11.70 × 3.64 | 8 | 5,737 |
| Venus | Кислотная владычица | 8.06 × 14.04 × 11.18 | 12 | 6,350 |
| Mercury | Солнечный скарабей | 8.25 × 11.75 × 10.50 | 15 | 9,777 |

## How to inspect

- The supplied PNGs in `/home/user/work/boss-models/` are **offline software projections of the exact authored JSON cells**. They are not Unity screenshots or AI-generated concept art; the actual Unity PBR shader/lighting and transforms can look different.
- In a Unity build, enter an **offline Creative** world and type `/boss-models` in chat. This instantiates a temporary **QA model gallery**. This gallery is not a gameplay boss encounter on Lestingia, nor a planetary spawn or space-transit encounter.
- `python Tools/check_planet_boss_models.py` checks palettes, cell indices, extent, attached 6-connected geometry, grounded feet and per-part motion. `python Tools/preview_planet_boss_models.py` regenerates the previews. Windows BuildSmokeProbe loads all three assets with the actual Unity mesh builder, inspects shader support, counts and part colliders; `.github/workflows/build.yml` expects `[Smoke] PLANET_BOSS_MODELS` when smoke completes.

**Scope / honest status:** Real game-renderable models and an idle rig, **not yet living bosses**: no combat AI, moveset, damage, planet terrain or natural gameplay spawns. Do not add them as encounters before the planet levels and user model sign-off. The revised Discovery shuttle exterior is still a separate geometry draft outside the Unity asset pipeline; the launch complex and ISS remain separate pending previews/approval. Space beyond the Solar System is not implemented by this model milestone.
