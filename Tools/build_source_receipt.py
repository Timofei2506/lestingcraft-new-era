"""Fingerprint actual build inputs; no Git object database, passwords, tokens or local caches."""
import hashlib,json,sys,os
from pathlib import Path
R=Path(__file__).resolve().parents[1]
paths=[]
for top in ['Assets','ArtSource','Packages','ProjectSettings','Tools','Server/src']:
 for p in (R/top).rglob('*'):
  if p.is_file() and not {'Generated','__pycache__','node_modules'}.intersection(p.relative_to(R).parts):paths.append(p)
paths.extend(p for name in ['Server/package.json','Server/package-lock.json','.github/workflows/build.yml'] if (p:=R/name).is_file())
files=[{'path':p.relative_to(R).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(set(paths))]
fingerprint=hashlib.sha256(json.dumps(files,sort_keys=True,separators=(',',':')).encode()).hexdigest()
out=Path(sys.argv[1]) if len(sys.argv)>1 else R/'Builds/Logs/source-receipt.json';out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps({'target':'Windows x64','rulesVersion':13,'contentVersion':8,'commit':os.environ.get('GITHUB_SHA','workspace-uncommitted'),'sourceFingerprint':fingerprint,'files':files},indent=2))
print('Windows source receipt:',len(files),'files;',fingerprint,'->',out)
