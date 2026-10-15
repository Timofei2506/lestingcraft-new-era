export const REPO = 'Timofei2506/lestingcraft-new-era';
export const SERVER = 'https://lestingcraft-coop.kroterallitir.workers.dev';
export function readyReleases(releases) {
  if (!Array.isArray(releases)) throw new Error('Invalid release response');
  return releases.filter(r => !r.draft && !r.prerelease).map(r => {
    const assets = (r.assets || []).filter(a => a.state === 'uploaded' && a.size > 0 && a.browser_download_url?.startsWith(`https://github.com/${REPO}/releases/download/`));
    const windows = assets.find(a => a.name === 'build-StandaloneWindows64.zip');
    if (!windows) return null;
    const asset = a => ({ name:a.name, size:a.size, url:a.browser_download_url, digest:a.digest || null });
    return {tag:r.tag_name,name:r.name || r.tag_name,date:r.published_at,url:`https://github.com/${REPO}/releases/tag/${encodeURIComponent(r.tag_name)}`,windows:asset(windows)};
  }).filter(Boolean);
}
async function timedFetch(url,options={},ms=8000) {
  const controller=new AbortController();const timer=setTimeout(()=>controller.abort(),ms);
  try{return await fetch(url,{...options,signal:controller.signal});}finally{clearTimeout(timer);}
}
export async function api(path,env={}) {
  if (path === '/api/releases') {
    try {
      const response=await timedFetch(`https://api.github.com/repos/${REPO}/releases?per_page=30`,{headers:{Accept:'application/vnd.github+json','User-Agent':'LestingCraft-site/1.0'}});
      if(!response.ok)throw new Error('Release upstream status '+response.status);
      return {available:true,releases:readyReleases(await response.json()),checkedAt:new Date().toISOString(),source:'github-api'};
    } catch {
      // Shared Cloudflare egress can exhaust unauthenticated GitHub API limits.
      // An Actions-generated public index is the fallback; no PAT lives in the site.
      let response;
      try {response=await timedFetch(`https://raw.githubusercontent.com/${REPO}/main/Website/public/releases.json`);}catch{}
      if(!response?.ok && env.ASSETS)response=await env.ASSETS.fetch(new Request('https://site.internal/releases.json'));
      if(!response?.ok)throw new Error('Release index unavailable');
      const feed=await response.json();
      if(feed.available!==true||!Array.isArray(feed.releases)||!feed.checkedAt)throw new Error('Invalid verified index');
      return {...feed,source:'verified-index'};
    }
  }
  if (path === '/api/status') {
    let online=false;
    try {const r=env.COOP?await env.COOP.fetch(new Request(SERVER+'/health')):await timedFetch(SERVER+'/health',{},5000);if(r.ok){const d=await r.json();online=d.ok===true&&d.version===1;}else console.warn('Health upstream status',r.status,'binding=',Boolean(env.COOP),(await r.text()).slice(0,150));}catch(e){console.warn('Health fetch failed',e.message);}
    return {online,server:SERVER,checkedAt:new Date().toISOString()};
  }
  return null;
}
export const securityHeaders = {
  'X-Content-Type-Options':'nosniff',
  'Referrer-Policy':'strict-origin-when-cross-origin',
  'X-Frame-Options':'SAMEORIGIN',
  'Content-Security-Policy':"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'self'; base-uri 'self'; object-src 'none'; form-action 'self'",
  'Permissions-Policy':'camera=(), microphone=(), geolocation=()'
};
