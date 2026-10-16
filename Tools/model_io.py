"""Lossless shared resource storage; avoids duplicate 1MB+ generated JSON in snapshots.
Authoring source remains build_voxel_items.py / build_expedition_items.py.
Unity Resources address Models/VoxelItems is unchanged (.bytes is a gzip TextAsset).
"""
from pathlib import Path
import json,gzip

def load_catalog(root):
 p=Path(root)/'Assets/Resources/Models/VoxelItems.bytes'
 return json.loads(gzip.decompress(p.read_bytes()).decode()) if p.exists() else json.loads((p.with_suffix('.json')).read_text())
def save_catalog(root,data):
 p=Path(root)/'Assets/Resources/Models/VoxelItems.bytes';raw=json.dumps(data,ensure_ascii=False,separators=(',',':')).encode();p.write_bytes(gzip.compress(raw,compresslevel=9,mtime=0));legacy=p.with_suffix('.json')
 if legacy.exists():legacy.unlink()
 if legacy.with_suffix('.json.meta').exists():legacy.with_suffix('.json.meta').rename(p.with_suffix('.bytes.meta'))
