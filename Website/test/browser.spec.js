import {test,expect} from '@playwright/test';

const pages=['/','/press/','/privacy/'];

test.beforeEach(async({page})=>{
  await page.route('**/api/status',r=>r.fulfill({json:{online:true,server:'https://example.test',checkedAt:new Date().toISOString()}}));
});

test('every page renders without JavaScript errors or failed same-origin requests',async({page})=>{
  const errors=[];const failed=[];
  page.on('pageerror',e=>errors.push(e.message));
  page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
  page.on('requestfailed',r=>failed.push(r.url()));
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

test('server status reflects the API answer',async({page})=>{
  await page.goto('/');
  await expect(page.locator('#server-status')).toHaveAttribute('data-state','online');
  await expect(page.locator('#server-status span').last()).toHaveText('Сервер онлайн');
});

test('server status shows an honest unknown state when the API fails',async({page})=>{
  await page.unroute('**/api/status');
  await page.route('**/api/status',r=>r.abort());
  await page.goto('/');
  await expect(page.locator('#server-status')).toHaveAttribute('data-state','unknown');
  await expect(page.locator('#server-status span').last()).toHaveText('Статус недоступен');
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
