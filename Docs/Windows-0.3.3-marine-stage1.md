# 0.3.3 — Marine / graphics overhaul, stage 1 (prerelease)

## Scope implemented in source
- Dense amethyst-purple crystal, wavelength-selective internal absorption/scattering, depth writing, directional and actual local-light glints. Glass keeps its separate transparent configuration.
- Blue shallow/deep water absorption, three analytic world-space wave bands (~0.2 m combined peak, weather dependent), geometric displacement and corresponding normals, moving shore foam/whitecaps. Prior small-wave/SSR implementation was not presented as newly invented. Water SSR: 16 Medium / 48 Ultra depth-march samples, sky fallback, no hardware ray tracing.
- Swimming, breath/immersion and underwater fog use the same analytic displaced surface and synchronized clock/weather, not a flat water cube. This is not a general realistic-physics overhaul: buoyant drops, rigid bodies and creature physics need further work.
- Distinct Ultra renderer with full-resolution 12-sample, wider-radius bilateral SSAO; Medium retains downsampled 8-sample SSAO. High-quality SMAA on Ultra, FXAA on Medium. No hidden auto-low. Near voxel geometry/PBR/colliders and preset view radii are not reduced.
- New generation-8 seas, shore sand and sandstone beds. Large contiguous basins are deterministic, integer-selected and mirrored in JS/C#. Protected story terrain retained; all versions 1..7, including unexplored cells, remain unchanged.
- Deterministic distant PBR landscape and sea LOD, 640 m Ultra / 256 m Medium, with full-detail streamed chunks unchanged. This is visible, non-colliding landscape LOD, not additional fully loaded/editable voxel chunks; far edits/trees/structure detail are not represented until streamed. Only new Lestingia content8 worlds use this LOD.
- Five campfires atop existing village posts replaced by framed shadowed warm lantern fixtures. South fixture moved to a genuinely supported post inside the village boundary (old z73 plan was outside its terrain region). Six lights total. Indoor campfires retained. Fixtures are architectural decoration, not yet placeable lantern/fence inventory blocks.
- Rules11/content8 for new rooms, 10/7 and older rooms accepted unchanged; old clients cannot join new marine rooms.

## Checks before native build
- Runtime C# compiled with Roslyn against actual Unity player reference assemblies; not Unity Editor execution.
- 129,198 managed generation checks, including 84,000 exact comparisons of versions1..7 against compiled 0.3.2 source, point/optimized-column parity for new seas, protected terrain and lamp support.
- 121 Node tests with 12,000 actual compiled C#/JS marine parity rows.
- Existing art/content/render-source contracts pass. These are not shader compiler/GPU/FPS evidence.

## Cloud blocker and native acceptance — NOT completed
Both cloud runs failed before any runner/build/deployment step: Windows 39006031981 and worker 39006036461. Actual GitHub check annotation: “The job was not started because recent account payments have failed or your spending limit needs to be increased. Please check the Billing & plans section in your settings”. No paid settings changed. No new Windows EXE produced, no new graphics frames captured/reviewed, no server11 deployed and no 0.3.3 publication. Existing public 0.3.2 remains unchanged. This is not a shader or game failure.

Still required: real Windows EXE under Direct3D11 WARP, shader compilation/no runtime errors, prior usability suite plus generated sea, clock-advanced wave frame, dense purple crystal, night village lanterns, full-radius streaming/distant LOD and displaced swimming boundary. Private screenshots only; no mirroring into public distribution.

## Deliberately later phases
House redesign, more placeable blocks/fences/torches/lanterns, proper mine rail/loot systems, starter balance, clearer story and epic cutscenes; broader physical simulation. No claim of photorealism, hardware RTX, hardware GPU performance, finished five-chapter story, or eight-native-player load testing.

## Public-image policy
Previously published screenshot folders removed from the current public branch; old game binaries retained. Current cleanup does not rewrite historical Git commits or erase third-party/GitHub caches. Future captures remain private.
