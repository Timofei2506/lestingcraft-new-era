import http from 'node:http';import fs from 'node:fs';import path from 'node:path';import {api,securityHeaders} from './src/api.mjs';
import {handleHit,handleStats} from './src/hits.mjs';
// Local preview mirrors the production counter with an in-memory KV.
const mem=new Map(),localKV={async get(k){return mem.get(k)??null;},async put(k,v){mem.set(k,v);}};
const readBody=req=>new Promise(resolve=>{let data='';req.on('data',c=>{data+=c;if(data.length>4096)req.destroy();});req.on('end',()=>resolve(data));req.on('error',()=>resolve(''));});
// Local preview server. Mirrors production routing: /dir and /dir/ serve dir/index.html, unknown pages get 404.html with status 404.
const previewHeaders={...securityHeaders};
delete previewHeaders['X-Frame-Options'];
previewHeaders['Content-Security-Policy']=previewHeaders['Content-Security-Policy'].replace("; frame-ancestors 'self'",'');
const root=path.resolve('public'),port=Number(process.env.PORT||3900);
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.webp':'image/webp','.mp4':'video/mp4','.jpg':'image/jpeg','.svg':'image/svg+xml','.woff2':'font/woff2','.ttf':'font/ttf','.txt':'text/plain; charset=utf-8','.xml':'application/xml; charset=utf-8'};
const notFound=(res)=>{const file=path.join(root,'404.html');res.writeHead(404,{'Content-Type':types['.html'],...previewHeaders});fs.createReadStream(file).pipe(res);};
http.createServer(async(req,res)=>{
 const url=new URL(req.url,'http://site');
 if(url.pathname==='/api/worlds') {
  try {const data=await api('/api/worlds');res.writeHead(200,{'Content-Type':'application/json',...previewHeaders});res.end(JSON.stringify(data));}catch{res.writeHead(503,{'Content-Type':'application/json'});res.end(JSON.stringify({available:false,worlds:[],players:0}));}return;
 }
 if(url.pathname==='/api/hit'||url.pathname==='/api/stats') {
  const body=req.method==='POST'?await readBody(req):undefined;
  const probe=new Request('http://site'+url.pathname,{method:req.method,headers:{'Content-Type':req.headers['content-type']||'application/json','Content-Length':String(body?body.length:0)},body});
  const answer=url.pathname==='/api/hit'?await handleHit(probe,{WAITLIST:localKV}):await handleStats(probe,{WAITLIST:localKV});
  res.writeHead(answer.status,{'Content-Type':'application/json',...previewHeaders});res.end(await answer.text());return;
 }
 if(url.pathname.startsWith('/api/')) {
  try {const data=await api(url.pathname);res.writeHead(data?200:404,{'Content-Type':'application/json',...previewHeaders});res.end(JSON.stringify(data||{error:'Not found'}));}catch{res.writeHead(503,{'Content-Type':'application/json'});res.end(JSON.stringify({available:false,releases:[]}));}return;
 }
 let file;
 try {file=path.resolve(root,'.'+decodeURIComponent(url.pathname));}catch{res.writeHead(400);res.end();return;}
 if(!file.startsWith(root+path.sep)&&file!==root){res.writeHead(403);res.end();return;}
 if(fs.existsSync(file)&&fs.statSync(file).isDirectory()) {
  if(!url.pathname.endsWith('/')){res.writeHead(301,{Location:url.pathname+'/'});res.end();return;}
  file=path.join(file,'index.html');
 } else if(!fs.existsSync(file)&&!path.extname(file)&&fs.existsSync(path.join(file,'index.html'))) {
  res.writeHead(301,{Location:url.pathname+'/'});res.end();return;
 }
 if(!fs.existsSync(file)||fs.statSync(file).isDirectory()){notFound(res);return;}
 const mime=types[path.extname(file)]||'application/octet-stream',size=fs.statSync(file).size,range=req.headers.range;
 if(mime==='video/mp4'&&range){const m=/^bytes=(\d*)-(\d*)$/.exec(range);if(m){const start=Number(m[1]||0),end=m[2]?Math.min(Number(m[2]),size-1):size-1;if(start>=size){res.writeHead(416,{'Content-Range':`bytes */${size}`});res.end();return;}res.writeHead(206,{'Content-Type':mime,'Content-Length':end-start+1,'Content-Range':`bytes ${start}-${end}/${size}`,'Accept-Ranges':'bytes',...previewHeaders});fs.createReadStream(file,{start,end}).pipe(res);return;}}
 res.writeHead(200,{'Content-Type':mime,'Accept-Ranges':'bytes',...previewHeaders});fs.createReadStream(file).pipe(res);
}).listen(port,'0.0.0.0',()=>console.log('LestingCraft website on '+port));
