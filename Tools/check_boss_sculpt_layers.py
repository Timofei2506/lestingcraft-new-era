"""Guard: every PBR layer referenced by the authored voxel sculpts must exist in the
shared surface-layer table and ship as a real albedo/normal/mask image set."""
from pathlib import Path
import re, sys
R = Path(__file__).resolve().parents[1]
src = (R/'Assets/Scripts/Gameplay/BossSculpt.cs').read_text(encoding='utf-8')
layers = set(re.findall(r'Pal\("[^"]+",\s*"([^"]+)"', src))
names_line = re.search(r'Names=\{(.*?)\};', (R/'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text(encoding='utf-8'), re.S)
table = [n.strip().strip('"') for n in names_line.group(1).split(',')]
missing_table = sorted(l for l in layers if l not in table)
missing_files = []
for l in sorted(layers):
    if not list((R/'Assets/Art/PBR').glob(l+'_albedo.*')):
        missing_files.append(l)
if missing_table or missing_files:
    print('FAIL layers not in SurfaceLayers:', missing_table, 'without images:', missing_files)
    sys.exit(1)
print('PASS:', len(layers), 'sculpt PBR layers all present in SurfaceLayers and Assets/Art/PBR')
