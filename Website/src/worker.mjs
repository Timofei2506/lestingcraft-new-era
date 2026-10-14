import {api,securityHeaders} from './api.mjs';
export default {
  async fetch(request,env,ctx) {
    const url=new URL(request.url);
    if(url.pathname.startsWith('/api/')) {
      if(request.method!=='GET')return Response.json({error:'Method not allowed'},{status:405});
      if(!['/api/releases','/api/status'].includes(url.pathname))return Response.json({error:'Not found'},{status:404});
      const key=new Request(url.origin+url.pathname+'?cache=v4');const cached=await caches.default.match(key);if(cached)return cached;
      try {
        const data=await api(url.pathname,env),ttl=url.pathname==='/api/releases'?300:60;
        const response=Response.json(data,{headers:{...securityHeaders,'Cache-Control':`public, max-age=${ttl}`}});
        ctx.waitUntil(caches.default.put(key,response.clone()));return response;
      }catch(e) {console.warn('Site API failed',url.pathname,e.message);return Response.json({available:false,releases:[],message:'Не удалось проверить релизы. Попробуйте позже.'},{status:503,headers:{...securityHeaders,'Cache-Control':'no-store'}});}
    }
    const asset=await env.ASSETS.fetch(request);const response=new Response(asset.body,asset);
    for(const [k,v] of Object.entries(securityHeaders))response.headers.set(k,v);
    return response;
  }
};
