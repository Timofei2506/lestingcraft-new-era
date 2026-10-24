// Единый отчёт «сколько людей реально дошло до игры».
// Источники — только настоящие счётчики, никакой накрутки:
//   1. GitHub Releases — сколько раз скачали каждый файл (download_count);
//   2. Supabase — сколько аккаунтов создано в игре (profiles);
//   3. кооперативный сервер — жив ли и какая версия правил.
//
// Запуск:
//   GITHUB_TOKEN=... SUPABASE_KEY=$(cat ~/.secrets/supabase.service.key) \
//   SUPABASE_URL=https://<ref>.supabase.co node Tools/players_report.mjs
import fs from 'node:fs';

const repo = process.env.GITHUB_REPO || 'Timofei2506/lestingcraft-new-era';
const githubToken = process.env.GITHUB_TOKEN || '';
const supabaseUrl = (process.env.SUPABASE_URL || '').replace(/\/+$/, '');
const supabaseKey = process.env.SUPABASE_KEY || '';
const coop = (process.env.COOP_ENDPOINT || 'https://lestingcraft-coop.kroterallitir.workers.dev').replace(/\/+$/, '');
const site = (process.env.SITE || 'https://lestingcraft.kroterallitir.workers.dev').replace(/\/+$/, '');

const line = (label, value) => console.log(`${label.padEnd(34, '.')} ${value}`);

async function json(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) throw new Error(`${url} -> HTTP ${response.status}`);
  return response.json();
}

async function github() {
  const headers = {Accept: 'application/vnd.github+json'};
  if (githubToken) headers.Authorization = `Bearer ${githubToken}`;
  const releases = await json(`https://api.github.com/repos/${repo}/releases?per_page=20`, {headers});
  let total = 0;
  const rows = [];
  for (const release of releases) {
    for (const asset of release.assets) {
      total += asset.download_count;
      rows.push([release.tag_name, asset.name, asset.download_count]);
    }
  }
  const repoInfo = await json(`https://api.github.com/repos/${repo}`, {headers});
  return {total, rows, stars: repoInfo.stargazers_count, watchers: repoInfo.subscribers_count};
}

async function count(table, column, headers) {
  const response = await fetch(`${supabaseUrl}/rest/v1/${table}?select=${column}&limit=1`, {
    headers: {...headers, Prefer: 'count=exact', Range: '0-0'},
  });
  if (!response.ok) throw new Error(`${table} -> HTTP ${response.status}`);
  const range = response.headers.get('content-range') || '*/0';
  return Number(range.split('/')[1] || 0);
}

async function supabase() {
  if (!supabaseUrl || !supabaseKey) return null;
  const headers = {apikey: supabaseKey, Authorization: `Bearer ${supabaseKey}`};
  return {
    profiles: await count('profiles', 'id', headers),
    wallets: await count('wallets', 'profile_id', headers),
    lots: await count('market_listings', 'id', headers),
  };
}

async function server() {
  const health = await json(`${coop}/health`);
  const status = await json(`${site}/api/status`).catch(() => ({}));
  return {health, online: status.online};
}

const report = {};
try { report.github = await github(); } catch (error) { console.log(`GitHub: ${error.message}`); }
try { report.supabase = await supabase(); } catch (error) { console.log(`Supabase: ${error.message}`); }
try { report.server = await server(); } catch (error) { console.log(`Server: ${error.message}`); }

console.log(`\n== LestingCraft · отчёт ${new Date().toISOString()} ==\n`);
if (report.github) {
  line('Скачиваний всего', report.github.total);
  for (const [tag, name, count] of report.github.rows) line(`  ${tag} ${name}`, count);
  line('Звёзд / подписчиков', `${report.github.stars} / ${report.github.watchers}`);
}
if (report.supabase) {
  line('Аккаунтов создано в игре', report.supabase.profiles ?? '?');
  line('Кошельков / лотов', `${report.supabase.wallets ?? '?'} / ${report.supabase.lots ?? '?'}`);
}
if (report.server) {
  line('Сервер жив', `${report.server.health.ok} (rules ${report.server.health.rulesVersion}, commit ${String(report.server.health.commit).slice(0, 7)})`);
  line('Сайт считает сервер онлайн', report.server.online);
}

const out = process.env.REPORT_OUT;
if (out) {
  fs.mkdirSync(new URL('.', `file://${out}`).pathname, {recursive: true});
  fs.writeFileSync(out, JSON.stringify({at: new Date().toISOString(), ...report}, null, 2));
  console.log(`\nJSON: ${out}`);
}
