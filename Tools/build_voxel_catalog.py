from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,zipfile,base64,html
r=Path(__file__).resolve().parents[1];out=r.parent/'game-assets';out.mkdir(exist_ok=True);j=json.loads((r/'Assets/Resources/Models/VoxelItems.json').read_text());font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',19);small=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',13);large=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',32)
canvas=Image.new('RGB',(1500,2020),'#101d23');d=ImageDraw.Draw(canvas);d.text((35,25),'LESTINGCRAFT / VOXEL OBJECTS',font=large,fill='#e1c58b');d.text((35,72),'25 настоящих моделей · текстурные рендеры · единые исходники для Unity и иконок',font=font,fill='#c7d6d5')
cards=[]
for i,m in enumerate(j['models']):
 x=30+(i%5)*294;y=125+(i//5)*375;d.rounded_rectangle((x,y,x+280,y+355),radius=10,fill='#1a2a31',outline='#3d5055');im=Image.open(r/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png').resize((272,272),Image.Resampling.LANCZOS);canvas.paste(im,(x+4,y+8),im);d.text((x+14,y+286),m['name'],font=font,fill='#f1f2e6');d.text((x+14,y+320),f'#{m["id"]}  ·  {len(m["cells"])//4:,} voxels',font=small,fill='#94b1af')
 data=base64.b64encode((r/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png').read_bytes()).decode();cards.append(f'<figure><img src="data:image/png;base64,{data}" alt="{html.escape(m["name"])}"><figcaption>{html.escape(m["name"])}</figcaption><small>#{m["id"]} · {len(m["cells"])//4} кубиков</small></figure>')
canvas.save(out/'Voxel-objects-catalog.jpg',quality=92)
(out/'Voxel-objects-catalog.html').write_text('''<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>LestingCraft — voxel objects</title><style>body{background:#101d23;color:#e3ece5;font:16px system-ui;margin:32px}h1{color:#e1c58b}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:18px}figure{margin:0;background:#1a2a31;border:1px solid #3d5055;padding:18px;border-radius:12px}img{width:100%;transition:transform .2s}img:hover{transform:scale(1.07)}figcaption{font-size:20px}small,p{color:#99b7b4}</style><h1>LESTINGCRAFT / VOXEL OBJECTS</h1><p>Рендеры 25 настоящих блочных моделей. Один источник геометрии для иконки, Unity mesh и OBJ. Это каталог ассетов, не снимок игрового EXE.</p><main>'''+''.join(cards)+'</main>')
with zipfile.ZipFile(out/'LestingCraft-voxel-icons.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in (r/'Assets/Resources/VoxelIcons').glob('*.png'):z.write(p,p.name)
 z.writestr('README.txt','25 original 384x384 transparent PNG textured orthographic renders of LestingCraft voxel objects. Geometry shared with Unity held objects and OBJ export.\n')
print('Catalog and 25 icon ZIP updated')
