"""Turn owner-approved voxel geometry into Unity TextAsset binary resources.
The approved voxel RLE stays in ArtSource/Space/Approved. No concept image is imported.
Run from repository root: python3 Tools/build_approved_space_models.py
"""
import json,struct,hashlib
from pathlib import Path

source=Path('ArtSource/Space/Approved')
target=Path('Assets/Resources/Space')
target.mkdir(parents=True,exist_ok=True)

for name in ('Shuttle','LaunchPad','Station'):
    raw=(source/(name+'.json')).read_bytes();d=json.loads(raw)
    size=d['size'];palette=d['palette'];runs=d['runs']
    assert len(size)==3 and all(isinstance(n,int) and 1<=n<=128 for n in size)
    assert 2<=len(palette)<=255 and palette[0]['name'] in ('air','vacuum')
    assert sum(count for _,count in runs)==size[0]*size[1]*size[2]
    assert len(runs)<1_000_000 and all(isinstance(i,int) and 0<=i<len(palette) and isinstance(count,int) and count>0 for i,count in runs)
    assert all('crawler' not in str(entry).lower() and 'transporter' not in str(entry).lower() for entry in palette)
    body=bytearray(b'LCVM'+struct.pack('<IIII',1,*size)+struct.pack('<H',len(palette)))
    for entry in palette:
        text=entry['name'].encode('utf8');color=entry['hex']
        assert len(text)<256 and len(color)==7 and color.startswith('#')
        body+=bytes.fromhex(color[1:])+bytes((255,len(text)))+text
    body+=struct.pack('<I',len(runs))
    for value,count in runs:body+=struct.pack('<BI',value,count)
    (target/(name+'.bytes')).write_bytes(body)
    print(name,'voxels',sum(count for i,count in runs if i),'RLE',len(runs),'bytes',len(body),'sha256',hashlib.sha256(raw).hexdigest())
