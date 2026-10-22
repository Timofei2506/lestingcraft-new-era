import fs from 'node:fs';import {readyReleases,REPO} from './src/api.mjs';
const token=process.env.GITHUB_TOKEN;
const response=await fetch(`https://api.github.com/repos/${REPO}/releases?per_page=30`,{headers:{Accept:'application/vnd.github+json','User-Agent':'LestingCraft-release-feed',...(token?{Authorization:'Bearer '+token}:{})}});
if(!response.ok)throw new Error('GitHub release sync failed: '+response.status);
const releases=readyReleases(await response.json());
const file=new URL('./public/releases.json',import.meta.url);
if(fs.existsSync(file)){const old=JSON.parse(fs.readFileSync(file,'utf8'));if(JSON.stringify(old.releases)===JSON.stringify(releases)){console.log('Release index unchanged');process.exit(0);}}
const feed={available:true,releases,checkedAt:new Date().toISOString()};
fs.writeFileSync(new URL('./public/releases.json',import.meta.url),JSON.stringify(feed,null,2)+'\n');console.log('Synced complete platform releases:',feed.releases.length);
