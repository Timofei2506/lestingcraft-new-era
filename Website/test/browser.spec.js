import {test,expect} from '@playwright/test';

const pages=['/','/press/','/privacy/','/license/'];

test.beforeEach(async({page})=>{
  await page.route('**/api/hit',r=>r.fulfill({json:{ok:true}}));
  await page.route('**/api/status',r=>r.fulfill({json:{online:true,checkedAt:new Date().toISOString()}}));
  await page.route('**/api/worlds',r=>r.fulfill({json:{available:true,worlds:[],players:0,checkedAt:new Date().toISOString()}}));
  await page.route('**/api/releases',r=>r.fulfill({json:{available:true,releases:[{tag:'v1.0.0-test',name:'Test build',prerelease:true,windows:{name:'build-StandaloneWindows64.zip',size:1024,url:'https://github.com/Timofei2506/lestingcraft-new-era/releases/download/v1/build-StandaloneWindows64.zip',digest:'sha256:windows'},android:{name:'LestingCraft-Android-arm64.apk',size:2048,url:'https://github.com/Timofei2506/lestingcraft-new-era/releases/download/v1/LestingCraft-Android-arm64.apk',digest:'sha256:android'}}]}}));
});

test('every page renders without JavaScript errors or failed same-origin requests',async({page})=>{
  const errors=[];const failed=[];
  page.on('pageerror',e=>errors.push(e.message));
  page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
  // Chromium aborts a preload=metadata video once it has the header — that is not a failure.
  page.on('requestfailed',r=>{if(r.failure()?.errorText==='net::ERR_ABORTED'&&/\.(mp4|webm)$/.test(r.url()))return;failed.push(r.url());});
  for(const route of pages){
    await page.goto(route);
    await expect(page.locator('main h1')).toBeVisible();
    await expect(page.locator('header .brand')).toBeVisible();
    await expect(page.locator('html')).not.toHaveClass(/no-js/);
  }
  expect(errors).toEqual([]);
  expect(failed.filter(u=>u.startsWith(new URL(page.url()).origin))).toEqual([]);
});

test('unknown page returns the 404 page',async({page})=>{
  const response=await page.goto('/this-page-does-not-exist');
  expect(response.status()).toBe(404);
  await expect(page.locator('main h1')).toHaveText('Такой страницы нет');
});

test('no horizontal scroll on a phone-width screen',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  for(const route of pages){
    await page.goto(route);
    const overflow=await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth);
    expect(overflow,route).toBeLessThanOrEqual(1);
  }
});

test('mobile menu opens, navigates and closes with Escape',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await page.goto('/');
  const toggle=page.locator('#menu-toggle');
  await expect(toggle).toHaveAttribute('aria-expanded','false');
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-expanded','true');
  await expect(page.locator('#nav-links')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(toggle).toHaveAttribute('aria-expanded','false');
});

test('cooperative status is generic and reflects the API answer without exposing an address',async({page})=>{
  await page.goto('/');
  const status=page.locator('#server-status');
  await expect(status).toHaveAttribute('data-state','online');
  await expect(status.locator('span').last()).toHaveText('Кооператив доступен');
  await expect(status).not.toContainText(/workers\.dev|https?:|lestingcraft-coop/i);
});

test('cooperative status shows an honest unknown state when the API fails',async({page})=>{
  await page.unroute('**/api/status');
  await page.route('**/api/status',r=>r.abort());
  await page.goto('/');
  await expect(page.locator('#server-status')).toHaveAttribute('data-state','unknown');
  await expect(page.locator('#server-status span').last()).toHaveText('Статус кооператива недоступен');
});

test('download page exposes one-click Windows and Android links from the complete release',async({page})=>{
  await page.goto('/#download');
  await expect(page.locator('[data-download="windows"]')).toHaveAttribute('href',/build-StandaloneWindows64\.zip$/);
  await expect(page.locator('[data-download="android"]')).toHaveAttribute('href',/LestingCraft-Android-arm64\.apk$/);
  await expect(page.locator('#download-status')).toHaveAttribute('data-state','ready');
  await expect(page.locator('.download-warning')).toContainText('временную подпись');
});

test('download buttons stay disabled when no complete platform release is available',async({page})=>{
  await page.unroute('**/api/releases');
  await page.route('**/api/releases',r=>r.fulfill({json:{available:true,releases:[]}}));
  await page.goto('/#download');
  await expect(page.locator('#download-status')).toHaveAttribute('data-state','empty');
  await expect(page.locator('[data-download="windows"]')).toHaveAttribute('aria-disabled','true');
  await expect(page.locator('[data-download="android"]')).toHaveAttribute('aria-disabled','true');
});

test('gallery opens a lightbox and closes it with Escape',async({page})=>{
  await page.goto('/');
  const shot=page.locator('[data-shot]').first();
  await shot.scrollIntoViewIfNeeded();
  await shot.click();
  const dialog=page.locator('#lightbox');
  await expect(dialog).toHaveJSProperty('open',true);
  await expect(page.locator('#lb-img')).toHaveAttribute('src',/\/media\/world-hud\.webp$/);
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveJSProperty('open',false);
});

test.describe('waitlist form',()=>{
  const fill=async(page,{email='player@example.com',consent=true,note=''}={})=>{
    await page.locator('#waitlist input[name="email"]').fill(email);
    if(note)await page.locator('#waitlist textarea[name="note"]').fill(note);
    if(consent)await page.locator('#waitlist input[name="consent"]').check();
  };

  test('invalid address is rejected in the browser without a request',async({page})=>{
    let requests=0;
    await page.route('**/api/waitlist',r=>{requests++;return r.fulfill({status:200,json:{ok:true}});});
    await page.goto('/');
    await fill(page,{email:'not-an-email'});
    await page.locator('#waitlist button[type="submit"]').click();
    await expect(page.locator('#form-status')).toContainText('Проверьте адрес почты');
    expect(requests).toBe(0);
  });

  test('consent is required',async({page})=>{
    await page.goto('/');
    await fill(page,{consent:false});
    await page.locator('#waitlist button[type="submit"]').click();
    await expect(page.locator('#form-status')).toContainText('Нужно согласие');
  });

  test('successful submit sends the note and shows the confirmation',async({page})=>{
    let body=null;
    await page.route('**/api/waitlist',async r=>{body=r.request().postDataJSON();await r.fulfill({status:200,json:{ok:true,message:'Готово! Напишем, когда откроем тестовый доступ.'}});});
    await page.goto('/#waitlist');
    await fill(page,{note:'Пресса'});
    await page.locator('#waitlist button[type="submit"]').click();
    await expect(page.locator('#form-status')).toContainText('Готово!');
    expect(body.email).toBe('player@example.com');
    expect(body.note).toBe('Пресса');
    expect(body.consent).toBe(true);
    expect(body.website).toBe('');
  });

  test('503 from the server says the list is not open yet',async({page})=>{
    await page.route('**/api/waitlist',r=>r.fulfill({status:503,json:{ok:false,message:'Лист ожидания скоро откроется. Попробуйте позже.'}}));
    await page.goto('/');
    await fill(page);
    await page.locator('#waitlist button[type="submit"]').click();
    await expect(page.locator('#form-status')).toContainText('скоро откроется');
    await expect(page.locator('#form-status')).toHaveAttribute('data-kind','soon');
  });

  test('network failure is reported, not hidden',async({page})=>{
    await page.route('**/api/waitlist',r=>r.abort());
    await page.goto('/');
    await fill(page);
    await page.locator('#waitlist button[type="submit"]').click();
    await expect(page.locator('#form-status')).toContainText('Нет связи');
  });
});

test('a visit is counted once per tab and reports a known source',async({page})=>{
  const hits=[];await page.route('**/api/hit',async r=>{hits.push(r.request().postDataJSON());await r.fulfill({json:{ok:true}});});
  await page.goto('/?ref=friend');
  await page.waitForTimeout(600);
  await page.reload();
  await page.waitForTimeout(600);
  expect(hits).toEqual([{source:'friend'}]);
});

test('the share button copies a tracked link',async({page,context})=>{
  await context.grantPermissions(['clipboard-read','clipboard-write']);
  await page.goto('/');
  const button=page.locator('[data-share]').first();
  await button.click();
  await page.waitForTimeout(300);
  const text=await page.evaluate(()=>navigator.clipboard.readText());
  expect(text).toContain('/?ref=share');
});

test('open worlds are listed with names, not codes in the open',async({page})=>{
  await page.route('**/api/worlds',r=>r.fulfill({json:{available:true,players:2,worlds:[
    {name:'Вечерний лес',players:2,maxPlayers:8,code:'a'.repeat(32),started:true,offline:false}
  ]}}));
  await page.goto('/#coop');
  await page.waitForTimeout(500);
  await expect(page.locator('#worlds-summary')).toHaveText(/Открыто миров: 1 · игроков в них: 2/);
  await expect(page.locator('#worlds-list li b')).toHaveText('Вечерний лес');
  expect(await page.locator('#worlds-list').innerText()).not.toContain('aaaaaaaa');
});

test('empty world list says so honestly instead of hiding',async({page})=>{
  await page.goto('/#coop');
  await page.waitForTimeout(400);
  await expect(page.locator('#worlds-summary')).toContainText('Сейчас никто не открыл мир');
});

test('a ready download button says what it downloads and its line fits a phone',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await page.goto('/#download');
  await expect(page.locator('[data-download="windows"]')).toHaveText('Скачать для Windows');
  await expect(page.locator('[data-download="android"]')).toHaveText('Скачать APK для Android');
  const overflow=await page.evaluate(()=>{
    const box=document.getElementById('download-builds');
    return [...box.querySelectorAll('*')].filter(el=>el.scrollWidth>el.clientWidth+2&&getComputedStyle(el).overflow==='visible').map(el=>el.className||el.tagName);
  });
  expect(overflow).toEqual([]);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1)).toBe(true);
});
