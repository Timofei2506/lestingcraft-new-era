import {chromium} from '@playwright/test';import fs from 'node:fs';import path from 'node:path';
// Builds indexable HTML for all informational routes. Browser JS still refreshes live APIs.
const root=path.resolve('public'),origin=process.env.PRERENDER_ORIGIN||'http://127.0.0.1:3900';
const routes=['/','/worlds','/worlds/lestingia','/worlds/ashlands','/worlds/skyreach','/worlds/echocrypt','/worlds/chaosrift','/game','/downloads','/roadmap','/journal','/journal/world-lab','/journal/cooperative','/journal/new-home','/multiplayer','/hosting','/faq','/community','/press','/privacy'];
const browser=await chromium.launch({headless:true}),page=await browser.newPage({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
// Avoid spending public GitHub rate limits on 20 repeated build-time requests.
await page.route('**/api/releases',r=>r.fulfill({json:{available:true,releases:[]}}));await page.route('**/api/status',r=>r.fulfill({json:{online:false}}));
for(const route of routes){
 await page.goto(origin+route,{waitUntil:'networkidle'});await page.locator('main h1').waitFor();
 await page.evaluate(route=>{
  document.querySelector('meta[property="og:title"]').content=document.title;
  const main=document.querySelector('main');const h=main.querySelector('h1');const p=main.querySelector('.page-heading>p,.hero-content>p,.world-detail-hero p');
  if(p)document.querySelector('meta[name="description"]').content=p.textContent;
  document.querySelectorAll('link[rel="canonical"],meta[property="og:url"]').forEach(el=>el.remove());
  const link=document.createElement('link');link.rel='canonical';link.href='https://lestingcraft.kroterallitir.workers.dev'+route;document.head.append(link);
  const og=document.createElement('meta');og.setAttribute('property','og:url');og.content=link.href;document.head.append(og);
  document.querySelectorAll('[data-server-status]').forEach(el=>el.textContent='Проверяем доступность…');document.querySelectorAll('[data-server-dot]').forEach(el=>el.classList.remove('online'));
  const ns=document.createElement('noscript');ns.innerHTML='<p style="padding:20px;text-align:center">Для вкладок, поиска и актуальной проверки сборок нужен JavaScript. Релизы можно проверить напрямую в <a href="https://github.com/Timofei2506/lestingcraft-new-era/releases">GitHub Releases</a>.</p>';main.prepend(ns);
 },route);
 const destination=route==='/'?path.join(root,'index.html'):path.join(root,route.slice(1),'index.html');fs.mkdirSync(path.dirname(destination),{recursive:true});fs.writeFileSync(destination,'<!doctype html>\n'+await page.locator('html').evaluate(el=>el.outerHTML));console.log('Rendered',route);
}
await browser.close();
