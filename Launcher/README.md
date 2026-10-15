# LestingCraft Launcher — Windows x64 (test)

Native WPF / .NET 8, self-contained single EXE. No Unity Hub/Editor, SDK, admin rights or browser engine on the user's machine. Build and native UI smoke run in GitHub Actions; no game release/tag is created.

## Update contract
- Hardcoded HTTPS Cloudflare endpoint `/api/launcher/latest`, fail-closed invitation access.
- Existing website Worker only; existing free GitHub Actions artifact storage. No R2, S3, Neon or paid signing service.
- Server-side `LAUNCHER_READER` is the GitHub credential; **never ship it in client code, manifest or artifact**. Prefer a fine-grained PAT for this repository with Actions read-only. If bootstrapped with an already authorized classic PAT, replace it with this limited credential soon; revoking it before replacement disables new downloads but not offline play.
- `LAUNCHER_ACCESS_KEY` is a separate random tester invitation, NOT an infrastructure credential. The private launcher archive can contain `test-access.key`; the client imports it into Windows DPAPI after successful check. Sharing the archive is sharing a tester invitation, not repository access. No public download link is added without owner confirmation.
- Only successful manually dispatched `Build Windows` runs on main with both game and SHA-256 manifest artifacts qualify. A failed EXE smoke can never become an update. Old successful builds without a manifest are skipped.
- Native build generates file hashes **after** smoke succeeds. Metadata and game ZIP redirects expire; the client refreshes them immediately before install.
- ZIP central directory is retrieved with HTTP Range, then only payloads of changed/corrupt files. Identical local files are SHA-256 verified and copied, including an existing manually downloaded folder. No claim of binary/block-level patches within a changed file.
- SHA-256 and uncompressed-size bounds, HTTPS host allowlist, path traversal/ADS/device-name/duplicate rejection, no ZIP64 or encrypted ZIP. Range 200 fallback is rejected rather than silently downloading the whole game.
- Staging version + atomic pointer commit. Cancellation, hash mismatch and network failure leave prior version selected. Prior version retained; rollback supported. Updates blocked while managed game is running. Unity LocalLow saves are outside the managed folder and never removed.
- Local game starts offline. Automatic self-update/signing of the launcher itself is **not implemented**; launcher updates are manual. Downloads are user-triggered; no arbitrary script execution.

## Storage and constraints
New game manifests retain for 14 days; game artifacts must still exist. Free GitHub quotas and artifact expiration apply. This is a tester distribution service, not permanent public release hosting. It cannot promise an old build can be reinstalled months later.

No version cleanup policy yet: old complete versions remain for rollback and consume disk. Failed staging folders are cleaned after normal cancellation/errors; OS/power-loss orphan staging may remain. An interrupted file is retried, not resumed byte-by-byte. SHA-256 verifies integrity from the trusted channel; it does not replace publisher code signing.

## Local/cloud checks
`dotnet run --project Launcher/Checks -c Release` — 31 executable checks, real ZIP deflate/stored/range transport, partial update, corruption/cancellation preservation, rollback, save isolation and unsafe-path rejection.
`dotnet build Launcher/Desktop` — WPF compile.
Workflow `launcher.yml` publishes Windows x64 standalone EXE, runs the actual EXE `--smoke`, and retains a native WPF screenshot. No game installation during this UI-only smoke.

`Configure invited launcher channel` puts encrypted GitHub Secrets into **existing website Worker** secret bindings. No repo visibility changes, game Release or new paid resource.
