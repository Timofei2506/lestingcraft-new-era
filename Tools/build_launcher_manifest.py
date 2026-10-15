"""Run only after native EXE smoke passes. No release/tag/upload credentials are used."""
import hashlib,json,pathlib,sys
root=pathlib.Path(sys.argv[1]).resolve();head=sys.argv[2];out=pathlib.Path(sys.argv[3]);files=[]
if len(head)!=40 or any(c not in '0123456789abcdef' for c in head):raise ValueError('Expected commit SHA')
for p in sorted(root.rglob('*')):
 if not p.is_file() or 'SmokeResults' in p.parts or p.suffix.lower() in ('.log','.pdb'):continue
 if p.is_symlink():raise ValueError('Symlinks forbidden')
 relative=p.relative_to(root).as_posix()
 with p.open('rb') as stream:sha=hashlib.file_digest(stream,'sha256').hexdigest()
 files.append({'path':relative,'size':p.stat().st_size,'sha256':sha})
if not any(f['path']=='LestingCraft.exe' for f in files):raise ValueError('EXE missing')
out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps({'schema':1,'build':head,'executable':'LestingCraft.exe','files':files},ensure_ascii=False),encoding='utf-8')
print('Launcher manifest:',len(files),'files,',sum(f['size'] for f in files),'bytes')
