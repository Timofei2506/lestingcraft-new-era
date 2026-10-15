// Private tester distribution from successful native builds. No releases, R2 or source-code proxy.
const ROOT='https://api.github.com/repos/Timofei2506/lestingcraft-new-era/';
const reply=(value,status=200)=>Response.json(value,{status,headers:{'Cache-Control':'no-store','X-Content-Type-Options':'nosniff'}});
export function archiveHost(value){try{const u=new URL(value);return u.protocol==='https:'&&!u.username&&!u.password&&(u.hostname.endsWith('.blob.core.windows.net')||u.hostname.endsWith('.actions.githubusercontent.com'));}catch{return false;}}
async function equal(a,b){const digest=x=>crypto.subtle.digest('SHA-256',new TextEncoder().encode(x));const [aa,bb]=await Promise.all([digest(a),digest(b)]);let d=0;const x=new Uint8Array(aa),y=new Uint8Array(bb);for(let i=0;i<x.length;i++)d|=x[i]^y[i];return d===0;}
export async function launcherFeed(request,env,fetcher=fetch){
 if(request.method!=='GET')return reply({message:'Method not allowed'},405);
 if(!env.LAUNCHER_READER||!env.LAUNCHER_ACCESS_KEY)return reply({message:'Тестовый канал ещё не настроен.'},503);
 const auth=request.headers.get('Authorization')||'';
 if(!auth.startsWith('Bearer ')||auth.length>512||!await equal(auth.slice(7),env.LAUNCHER_ACCESS_KEY))return reply({message:'Нужен код приглашения.'},401);
 const headers={Authorization:'Bearer '+env.LAUNCHER_READER,Accept:'application/vnd.github+json','User-Agent':'LestingCraft-launcher-feed/0.1'};
 async function get(path){const r=await fetcher(ROOT+path,{headers,signal:AbortSignal.timeout(12000)});if(!r.ok)throw Error('Private build feed unavailable');return r.json();}
 try{
  const data=await get('actions/workflows/build.yml/runs?branch=main&status=success&per_page=8');
  for(const run of data.workflow_runs||[]){
   if(run.conclusion!=='success'||run.head_branch!=='main'||!/^[a-f0-9]{40}$/.test(run.head_sha)||run.event!=='workflow_dispatch')continue;
   const {artifacts=[]}=await get(`actions/runs/${run.id}/artifacts?per_page=100`);
   const find=name=>artifacts.find(a=>a.name===name&&!a.expired&&a.size_in_bytes>0);
   const game=find('build-StandaloneWindows64'),manifest=find('launcher-manifest');if(!game||!manifest)continue;
   async function signed(id){if(!Number.isSafeInteger(id)||id<=0)throw Error('Invalid artifact');const r=await fetcher(ROOT+`actions/artifacts/${id}/zip`,{headers,redirect:'manual',signal:AbortSignal.timeout(12000)});const url=r.headers.get('Location');if(r.status!==302||!archiveHost(url))throw Error('Invalid artifact URL');return url;}
   const [archive,manifestArchive]=await Promise.all([signed(game.id),signed(manifest.id)]);
   return reply({schema:1,build:run.head_sha,archive,manifestArchive,channel:'invited-testers',publishedAt:run.updated_at});
  }
  return reply({message:'Проверенный пакет ещё готовится. Установленную игру можно запустить офлайн.'},503);
 }catch{return reply({message:'Канал временно недоступен. Установленная версия не затронута.'},503);}
}
