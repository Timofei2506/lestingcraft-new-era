import {api,securityHeaders} from './api.mjs';
import {launcherFeed} from './launcher.mjs';
import {handleWaitlist} from './waitlist.mjs';
import {handleHit,handleStats} from './hits.mjs';
export default {
  async fetch(request,env,ctx) {
    const url=new URL(request.url);
    if(url.pathname==='/api/launcher/latest')return launcherFeed(request,env);
    if(url.pathname==='/api/waitlist')return handleWaitlist(request,env);
    if(url.pathname==='/api/hit')return handleHit(request,env);
    if(url.pathname==='/api/stats')return handleStats(request,env);
    if(url.pathname.startsWith('/api/')) {
      if(request.method!=='GET')return Response.json({error:'Method not allowed'},{status:405});
      if(!['/api/releases','/api/status','/api/worlds'].includes(url.pathname))return Response.json({error:'Not found'},{status:404});
      const key=new Request(url.origin+url.pathname+'?cache=v7-worlds');const cached=await caches.default.match(key);if(cached)return cached;
      try {
        const data=await api(url.pathname,env),ttl=url.pathname==='/api/releases'?300:url.pathname==='/api/worlds'?20:60;
        const response=Response.json(data,{headers:{...securityHeaders,'Cache-Control':`public, max-age=${ttl}`}});
        ctx.waitUntil(caches.default.put(key,response.clone()));return response;
      }catch(e) {console.warn('Site API failed',url.pathname,e.message);return Response.json({available:false,releases:[],message:'Не удалось проверить релизы. Попробуйте позже.'},{status:503,headers:{...securityHeaders,'Cache-Control':'no-store'}});}
    }
    const asset=await env.ASSETS.fetch(request);const response=new Response(asset.body,asset);
    for(const [k,v] of Object.entries(securityHeaders))response.headers.set(k,v);
    return response;
  }
};
