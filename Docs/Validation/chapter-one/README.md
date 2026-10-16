# Chapter-one validation scope

All results here refer to source/managed code and the local standalone Node server, not a rendered Unity game.

- `runtime-compile.log`: all runtime C# sources compiled against actual Unity managed assemblies using Roslyn; 0 errors, 11 warnings.
- `editmode-source-compile.log`: three EditMode source files compiled against actual Unity/NUnit assemblies; no editor stubs.
- `selected-managed-tests.log`: eight selected managed NUnit methods executed. Resource import, native allocators, engine tests and Burst were not executed.
- `server-tests.log`: 76 Node tests, including mock Worker SQL/WebSocket reconstruction. Not a live Durable Object test.
- `two-clients.log`: two real WebSocket clients completed the chapter, comparing shared story packets/world clocks. Programmatic positions/inputs are not gameplay/graphics evidence.
- `process-restart.log`: complete standalone process stop/start, private inventories/reward claims/offhand and common chapter restored.
- `legacy-fauna-two-clients.log`, `legacy-fauna-restart.log`: actual compatibility regression for rules6/content3.
- `story-core.log`, `inventory-tests.log`, `expanded-core.log`, `legacy-nature-core.log`, `skylight.log`: actual pure/managed core checks, no native engine/GPU execution.
- `parity.log`: 40,000 C#→JS vectors across content4 terrain, story reducer and preserved content3.
- `catalog-render-contracts.log`: asset consistency and shader-source contracts, NOT shader compilation or image-quality validation.
- `source-sha256.txt`: fingerprints of the checked runtime/test/content sources.
- `protected-artifacts.json`: old Windows artifact, existing screenshots and AI masters inventoried without replacing them.

Outstanding: actual Unity import/Burst/shader tests, Windows rendering/cameras/physics/UI/light/shadows/audio/performance, two native Unity clients and eight-player native sessions. No new build, release, deployment or screenshots are represented by these logs.
