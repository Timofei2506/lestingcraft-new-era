## Обновление rules 8 / content 5

Новые миры/оружие/алмазы/структуры и сервис аккаунтов: `../Docs/Экспедиция-алмазы-аккаунты.md`. Требуется Node 22. Аккаунты `/api/accounts/*`: SQLite DO `AccountDirectory` / отдельный приватный JSON на standalone. Не публикуйте серверную папку данных. Миграция Cloudflare `v2-accounts`. Пароли и коды не присылайте в чат. Публичное развёртывание этого изменения ещё не выполнялось.


## Current Unity client: rules7, content4, host-start lobby

New room POSTs from the current client must include `version:1`, `rulesVersion:7`, `mode:0..3`, `dimension:0`, a signed 32-bit `seed`, and `ownerKey` (the creator's private 32-character lowercase hexadecimal player identity). Keep that identity private; share the room code, not the identity. The server strips `ownerKey` and wildlife internals from public create/welcome responses and rosters.

All newly created multiplayer rooms wait in a lobby. The `lobby` roster packet includes `hostId`; only that identity may send `lobby_start`. The same packet starts every client, then story-mode clients send `story_ready` after their worlds are loaded. Story packets carry the shared phase/line/mechanisms/boss state and the server world clock. Per-player inventory/reward claims remain private. Existing content1/2/3 rooms retain their terrain version; current rules7 clients can still join compatible older rooms. Do not silently convert old rooms to content4.

`npm test` includes chapter/lobby/reconstruction/claims/path-clearance contracts. `TEST_ENDPOINT=http://127.0.0.1:8797 node test/story-integration.js` runs two actual WebSocket clients through the complete chapter. Set `TEST_RECEIPT` to a temporary file; stop/restart the standalone server with the same `DATA_DIR`, then run the script with `--reconnect`. This tests the protocol, not Unity rendering or Cloudflare deployment. The same full-process scenario is authored in `.github/workflows/server.yml`.
