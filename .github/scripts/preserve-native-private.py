"""Run only inside the PRIVATE verification job. No public screenshot artifacts."""
import os,json,hashlib,zipfile,subprocess,urllib.request,urllib.parse
from pathlib import Path
repo=os.environ['GITHUB_REPOSITORY'];token=os.environ['GH_TOKEN'];sha=os.environ['GITHUB_SHA'];run=os.environ['GITHUB_RUN_ID'];base='https://api.github.com/repos/'+repo

def api(method,url,data=None,raw=None,content='application/json'):
 body=raw if raw is not None else json.dumps(data).encode() if data is not None else None
 req=urllib.request.Request(url if url.startswith('https://') else base+url,data=body,method=method,headers={'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json','Content-Type':content,'User-Agent':'LestingCraft-private-native-QA'})
 with urllib.request.urlopen(req,timeout=180) as response:
  b=response.read();return json.loads(b) if b else None
assert api('GET','')['private'] is True,'Never store native verification images in public repositories'
files=[]
for folder in [Path('Builds/Logs'),Path('Builds/StandaloneWindows64/SmokeResults')]:
 if folder.exists():files.extend(p for p in folder.rglob('*') if p.is_file() and p.stat().st_size<20_000_000)
commit=api('GET','/git/commits/'+sha);tree=[]
import base64
for p in files:
 b=api('POST','/git/blobs',{'content':base64.b64encode(p.read_bytes()).decode(),'encoding':'base64'})
 tree.append({'path':'Docs/PrivateNative/'+run+'/'+p.as_posix(),'mode':'100644','type':'blob','sha':b['sha']})
if tree:
 t=api('POST','/git/trees',{'base_tree':commit['tree']['sha'],'tree':tree});c=api('POST','/git/commits',{'message':'Private native verification evidence '+run,'tree':t['sha'],'parents':[sha]});api('POST','/git/refs',{'ref':'refs/heads/qa/native-'+run,'sha':c['sha']});print('Private native evidence preserved: qa/native-'+run)
log=Path('Builds/Logs/windows-smoke.log');text=log.read_text(errors='replace') if log.exists() else ''
if '[Smoke] PASS' not in text or '[Smoke] FAIL' in text:
 print('No release candidate: native verification has not passed.');raise SystemExit(0)
root=Path('Builds/StandaloneWindows64');archive=Path('Builds/LestingCraft-Windows-x64-0.3.3-marine-stage1-candidate.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for p in sorted(root.rglob('*')):
  if p.is_file() and 'SmokeResults' not in p.parts and p.suffix.lower() not in ('.log','.pdb'):z.write(p,p.relative_to(root).as_posix())
release=api('POST','/releases',{'tag_name':'qa-marine-stage1-'+run,'target_commitish':sha,'name':'PRIVATE native candidate 0.3.3 / '+run,'body':'Actual Windows EXE passed native WARP smoke. Manual visual acceptance still required. No public publication authorized by this job.','draft':True,'prerelease':True})
for p in [archive,Path('Builds/LauncherFeed/game-manifest.json')]:
 if not p.exists():continue
 url=release['upload_url'].split('{')[0]+'?name='+urllib.parse.quote(p.name);api('POST',url,raw=p.read_bytes(),content='application/octet-stream')
print('PRIVATE_CANDIDATE_READY release='+str(release['id'])+' SHA256='+hashlib.file_digest(archive.open('rb'),'sha256').hexdigest())
