// Applies SQL files to the LestingCraft Supabase project through the
// Management API. No credentials live in this repository:
//
//   SUPABASE_ACCESS_TOKEN=sbp_...        personal access token (Management API)
//   SUPABASE_PROJECT_REF=abcdefghijklmn   project ref, e.g. from the dashboard URL
//
//   node apply.mjs schema    # create/upgrade tables, functions and policies
//   node apply.mjs verify    # run the rolled-back end-to-end economy check
//   node apply.mjs schema verify
//
// This is the same code path CI uses; it never prints the token.
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const token = process.env.SUPABASE_ACCESS_TOKEN;
const project = process.env.SUPABASE_PROJECT_REF;
const files = process.argv.slice(2);

if (!token || !project) {
  console.error('Set SUPABASE_ACCESS_TOKEN and SUPABASE_PROJECT_REF (never commit them).');
  process.exit(2);
}
if (!files.length) {
  console.error('Usage: node apply.mjs schema [verify]');
  process.exit(2);
}

const endpoint = `https://api.supabase.com/v1/projects/${project}/database/query`;

for (const name of files) {
  const file = path.resolve(`${name}.sql`);
  if (!fs.existsSync(file)) {
    console.error(`Missing SQL file: ${file}`);
    process.exit(2);
  }
  const query = fs.readFileSync(file, 'utf8');
  const started = Date.now();

  const response = await fetch(endpoint, {
    method: 'POST',
    headers: {Authorization: `Bearer ${token}`, 'Content-Type': 'application/json'},
    body: JSON.stringify({query})
  });

  const text = await response.text();
  if (!response.ok) {
    console.error(`FAIL ${name}: HTTP ${response.status}`);
    console.error(text.slice(0, 4000));
    process.exit(1);
  }

  let rows = [];
  try { rows = JSON.parse(text); } catch {}
  console.log(`OK ${name}.sql (${Date.now() - started} ms)`);
  if (Array.isArray(rows) && rows.length) {
    for (const row of rows) console.log('  ', Object.values(row).join(' | '));
  }
}
