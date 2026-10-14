import http from 'node:http';import fs from 'node:fs';import path from 'node:path';import {api,securityHeaders} from './src/api.mjs';
const previewHeaders={...securityHeaders};
delete previewHeaders['X-Frame-Options'];
previewHeaders['Content-Security-Policy']=previewHeaders['Content-Security-Policy'].replace("; frame-ancestors 'self'",'');
const root=path.resolve('public'),port=Number(process.env.PORT||3900);
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json','.webp':'image/webp','.jpg':'image/jpeg','.svg':'image/svg+xml','.woff2':'font/woff2','.ttf':'font/ttf','.txt':'text/plain; charset=utf-8'};
http.createServer(async(req,res)=>{
 const url=new URL(req.url,'http://site');
 if(url.pathname.startsWith('/api/')) {
  try {const data=await api(url.pathname);res.writeHead(data?200:404,{'Content-Type':'application/json',...previewHeaders});res.end(JSON.stringify(data||{error:'Not found'}));}catch{res.writeHead(503,{'Content-Type':'application/json'});res.end(JSON.stringify({available:false,releases:[]}));}return;
 }
 let file;
 try {file=path.resolve(root,'.'+decodeURIComponent(url.pathname));}catch{res.writeHead(400);res.end();return;}
 if(!file.startsWith(root+path.sep)&&file!==root){res.writeHead(403);res.end();return;}
 if(fs.existsSync(file)&&fs.statSync(file).isDirectory()&&fs.existsSync(path.join(file,'index.html')))file=path.join(file,'index.html');
 if(!fs.existsSync(file)||fs.statSync(file).isDirectory()) {
  if(path.extname(file)){res.writeHead(404);res.end('Not found');return;}
  file=path.join(root,'index.html');
 }
 res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream',...previewHeaders});fs.createReadStream(file).pipe(res);
}).listen(port,'0.0.0.0',()=>console.log('LestingCraft website on '+port));
