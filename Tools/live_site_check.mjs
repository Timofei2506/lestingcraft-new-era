// Проверка живого сайта в настоящем браузере: кнопки скачивания ведут на файлы,
// страницы не падают, на телефоне ничего не вылезает за экран.
// Запуск: node Tools/live_site_check.mjs [url]
import {chromium} from '../Website/node_modules/playwright-core/index.mjs';

const base = (process.argv[2] || 'https://lestingcraft.kroterallitir.workers.dev').replace(/\/+$/, '');
const pages = ['/', '/press/', '/play-with-friends/', '/privacy/', '/license/'];
const browser = await chromium.launch();
let failed = 0;

for (const [label, viewport] of [['desktop', {width: 1440, height: 900}], ['phone', {width: 390, height: 844}]]) {
  const page = await browser.newPage({viewport, deviceScaleFactor: 1});
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push('console: ' + m.text()); });

  for (const path of pages) {
    await page.goto(base + path, {waitUntil: 'load'});
    await page.waitForTimeout(700);
    const title = await page.title();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    const ok = title && overflow <= 1;
    if (!ok) failed++;
    console.log(`${ok ? 'PASS' : 'FAIL'} ${label.padEnd(7)} ${path.padEnd(22)} title="${title.slice(0, 48)}" overflow=${overflow}`);
  }

  await page.goto(base + '/#download', {waitUntil: 'load'});
  await page.waitForTimeout(1500);
  const links = await page.$$eval('[data-download]', els => els.map(e => ({
    platform: e.dataset.download,
    label: e.textContent.trim(),
    href: e.getAttribute('href'),
    enabled: !e.hasAttribute('aria-disabled'),
  })));
  for (const link of links) {
    const ok = link.enabled && /releases\/download\/v\d/.test(link.href || '');
    if (!ok) failed++;
    console.log(`${ok ? 'PASS' : 'FAIL'} ${label.padEnd(7)} download ${link.platform}: "${link.label}" → ${(link.href || '').split('/').slice(-2)[0]}`);
  }
  console.log(`${errors.length ? 'FAIL' : 'PASS'} ${label.padEnd(7)} console errors: ${errors.length}${errors.length ? ' → ' + errors.slice(0, 2).join(' | ') : ''}`);
  if (errors.length) failed++;
  await page.close();
}

await browser.close();
console.log(failed ? `\n${failed} проверка(и) не прошли` : '\nЖивой сайт в порядке');
process.exit(failed ? 1 : 0);
