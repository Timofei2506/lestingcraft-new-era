// Honesty check: the site must not describe unfinished features as done or promise things we have not built.
// Blocks marked as planned (data-status="planned" and data-planned) are the only place such phrases may appear.
import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import path from 'node:path';
const root=path.resolve('public');
const pages=['index.html','press/index.html','privacy/index.html','404.html'];
const forbidden=[
  /пять\s+измерен|5\s+измерен/i,
  /портал/i,
  /босс/i,
  /60\s*FPS/i,
  /бесплатн/i,
  /без\s+ручной\s+настройк/i,
  /одинаков\w*\s+тайминг/i,
  /одним\s+автором/i,
  /early\s+access/i,
  /steam/i,
  /\b(скачать\s+сейчас|выпущен[аоы]?|вышла|вышел)\b/i,
  /\b(топ|лучш\w+\s+в\s+мире)\b/i,
];
const stripPlanned=html=>html
  .replace(/<div class="stage reveal" data-status="planned">[\s\S]*?<\/ul>\s*<\/div>/g,'')
  .replace(/<(\w+)[^>]*\sdata-planned[^>]*>[\s\S]*?<\/\1>/g,'')
  .replace(/<script[\s\S]*?<\/script>/g,'')
  .replace(/<style[\s\S]*?<\/style>/g,'');
for(const file of pages){
  test(`${file} has no forbidden claims outside planned blocks`,()=>{
    const text=stripPlanned(fs.readFileSync(path.join(root,file),'utf8'));
    for(const re of forbidden) assert.doesNotMatch(text,re,`${file}: ${re}`);
  });
}
test('planned items really are marked as planned',()=>{
  const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
  assert.match(html,/data-status="planned"/);
  assert.match(html,/Пять измерений с порталами/);
  assert.match(html,/Боссы/);
});
test('the old SPA catch-all is gone: unknown paths must not render the home page',()=>{
  const wrangler=fs.readFileSync(path.resolve('wrangler.toml'),'utf8');
  assert.match(wrangler,/not_found_handling\s*=\s*"404-page"/);
});
