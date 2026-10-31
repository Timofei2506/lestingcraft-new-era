# Rendering / streaming foundation — 31 October 2026

## Implemented
- Bounded concurrent Burst chunk-generation queue (2–8 jobs), explicit completion/disposal on world switch and teardown.
- One-chunk data halo and neighbor gating before meshing; avoid procedural main-thread border resampling.
- Reuse mesh snapshot storage and reduce snapshot budget checks from every row to every layer.
- Startup streaming CPU budget 10 ms vs steady-state 4 ms. These are cooperative budgets, not hard frame limits.
- Global HP is authoritative: no limb-death bypass or limb movement/action penalties. Bandages heal 22 HP. Legacy body payload remains readable.
- Compact standard HP HUD, corrected marketplace/friends child coordinates, updated smoke and health tests.
- New generated menu illustration (not gameplay evidence), analytic sky/ground specular fallback, double-sided shadow casters, reduced normal bias, object snow/macro albedo correction.
- Two cloud layers, longer horizon march, dual-lobe scattering approximation and two cloud shadow samples. Requires native visual and GPU timing validation.
- Slate/amber desktop and website palette alignment; website keeps real existing gameplay screenshots.
- Removed audited obsolete material sources, iteration logs and design prototypes; removed 11 stale remote branches. SHA recovery manifest is adjacent.

## Validation
- Server: 241 Node tests pass after npm ci (the initial three failures were missing local packages).
- Website: 41 Node tests pass.
- Python art/content/render source checks pass.
- Pure C# core workflow compilation/assertions pass locally under Mono.
- No Unity editor/GPU available locally. No claim of photorealistic target match or instant loading is established by source checks.

## Remaining work (not claimed complete)
- Concurrent mesh-worker pool and target-hardware chunk streaming measurements.
- Lockstep content-10/rules-15 migration: client rules remain 14 until structures, all 40 species, save gates and world-boss integration are migrated together. A version-number-only bump would be unsafe.
- Architectural upgrades across settlements, story landmarks and planetary sites, and detailed fauna/boss/planet overhaul.
- Full PBR source rebuild, multi-channel Perlin-Worley cloud noise, aerial perspective / water / avatar GPU validation and target-reference matching.
- Full launcher/site/menu layout redesign beyond this palette and HUD foundation.
- Native Unity smoke screenshots and Windows build confirmation.
