"""Honest software preview of the EXACT shared boss/drone sculpt JSON (Tools/export_boss_sculpts.cs).
Not Unity/gameplay: geometry, albedo layers, tints, UV scale and emission come from the model data.
Lighting is approximate studio lighting; this is not a Unity URP/shadow/bloom validation.
Usage: python3 Tools/preview_boss_sculpts.py <json_dir> <out_dir> [name filters...]
"""
from pathlib import Path
import json, sys, math
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

R = Path(__file__).resolve().parents[1]
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else R / 'BossSculptJson'
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else R / 'ArtSource/BossModels/SculptPreviews'
OUT.mkdir(parents=True, exist_ok=True)
ONLY = sys.argv[3:]

C = np.array([[[1,0,0],[1,1,0],[1,1,1],[1,0,1]],[[0,0,1],[0,1,1],[0,1,0],[0,0,0]],[[0,1,1],[1,1,1],[1,1,0],[0,1,0]],[[0,0,0],[1,0,0],[1,0,1],[0,0,1]],[[1,0,1],[1,1,1],[0,1,1],[0,0,1]],[[0,0,0],[0,1,0],[1,1,0],[1,0,0]]], float)
N = np.array([[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]], int)

_texcache = {}
def albedo(name):
    if name in _texcache: return _texcache[name]
    hits = [f for f in (R/'Assets/Art/PBR').glob(name+'_albedo.*') if f.suffix != '.meta']
    if not hits:
        _texcache[name] = np.full((4,4,3), .5)
        return _texcache[name]
    img = Image.open(hits[0]).convert('RGB').resize((256,256))
    t = (np.asarray(img, float)/255)**2.2
    _texcache[name] = t
    return t

def render(data, W=760, H=940, yaw=0.62, pitch=0.16):
    unit = data['unit']; pal = data['palette']
    cells = {}
    for part in data['parts']:
        c = part['cells']
        for i in range(0, len(c), 4):
            cells[(c[i], c[i+1], c[i+2])] = c[i+3]
    bb = np.array(data['bbox']); mn, bbmx = bb[:3], bb[3:]
    center = (mn+bbmx)/2; radius = float(np.linalg.norm(bbmx-mn))/2
    cp, sp = math.cos(pitch), math.sin(pitch)
    eye = center + np.array([math.sin(yaw)*cp, sp, math.cos(yaw)*cp]) * radius*3.1
    view = eye-center; view /= np.linalg.norm(view)
    right = np.cross([0.,1,0], view); right /= np.linalg.norm(right)
    up = np.cross(view, right)
    light = np.array([-.5,.8,.62]); light /= np.linalg.norm(light)
    light2 = np.array([.7,.25,-.4]); light2 /= np.linalg.norm(light2)
    scale = min(W,H)*.82/(radius*2)
    rgb = np.zeros((H,W,3)); depth = np.full((H,W), np.inf)
    glow = np.zeros((H,W,3))
    yy, xx = np.mgrid[:H,:W]
    g = np.clip(1-(((xx-W*.5)/(W*.9))**2+((yy-H*.35)/(H*.95))**2)*.5, .18, 1)
    rgb += np.array([.055,.065,.08])*g[:,:,None]
    faces = 0
    for k, mat in cells.items():
        p = pal[mat]; tex = albedo(p['layer']); tint = np.array(p['tint'])**2.2
        emis = p['emission']
        for face in range(6):
            n = N[face]
            if tuple(np.array(k)+n) in cells or n@view <= 0: continue
            faces += 1
            verts = (np.array(k)+C[face])*unit
            q = verts-eye
            screen = np.column_stack([W*.5+q@right*scale, H*.5-q@up*scale, q@view])
            if face < 2: uv = verts[:,[2,1]]*.8
            elif face < 4: uv = verts[:,[0,2]]*.8
            else: uv = verts[:,[0,1]]*.8
            occ = 0
            for jump in (2,4,7):
                if tuple(np.array(k)+n*jump) in cells: occ += 1
            lam = max(0., float(n@light))
            lam2 = max(0., float(n@light2))
            lightness = .30 + lam*.95 + lam2*.28
            lightness *= 1-.12*occ
            for ids in ([0,1,2],[0,2,3]):
                pts = screen[ids]; tuv = uv[ids]; a,b,c = pts
                x0=max(0,int(np.floor(pts[:,0].min()))); x1=min(W-1,int(np.ceil(pts[:,0].max())))
                y0=max(0,int(np.floor(pts[:,1].min()))); y1=min(H-1,int(np.ceil(pts[:,1].max())))
                if x1<x0 or y1<y0: continue
                gy,gx = np.mgrid[y0:y1+1,x0:x1+1]; gx=gx+.5; gy=gy+.5
                den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
                if abs(den)<1e-9: continue
                w1=((b[1]-c[1])*(gx-c[0])+(c[0]-b[0])*(gy-c[1]))/den
                w2=((c[1]-a[1])*(gx-c[0])+(a[0]-c[0])*(gy-c[1]))/den
                w0=1-w1-w2
                inside=(w0>=-.001)&(w1>=-.001)&(w2>=-.001)
                if not inside.any(): continue
                z=w0*a[2]+w1*b[2]+w2*c[2]
                sub=depth[y0:y1+1,x0:x1+1]
                mask=inside&(z<sub)
                if not mask.any(): continue
                sub[mask]=z[mask]
                hgt, wid = y1-y0+1, x1-x0+1
                u = w0[:,:,None]*tuv[0][None,None,:] + w1[:,:,None]*tuv[1][None,None,:] + w2[:,:,None]*tuv[2][None,None,:]
                ti = np.clip((u%1)*255,0,255).astype(int)
                col = tex[ti[:,:,1],ti[:,:,0]]*tint
                lit = col*lightness
                if emis>0: lit = lit + col*emis*1.4
                out=rgb[y0:y1+1,x0:x1+1]
                out[mask]=lit[mask]
                if emis>0: glow[y0:y1+1,x0:x1+1][mask]=col[mask]*emis
    gb=Image.fromarray(np.clip(glow*255,0,255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(9))
    rgb+=np.asarray(gb,float)/255*.85
    rgb=np.clip(rgb,0,1)**(1/2.2)
    return Image.fromarray((rgb*255).astype(np.uint8)), faces

def main():
    files = sorted(SRC.glob('*.json'))
    if ONLY: files=[f for f in files if any(o in f.name for o in ONLY)]
    sheet=[]
    for f in files:
        data=json.loads(f.read_text())
        img,faces=render(data)
        out=OUT/(f.stem+'.png')
        img.save(out)
        sheet.append((f.stem,img))
        print(f.stem, faces, 'faces ->', out)
    cols=5
    tw,th=300,372
    rows=(len(sheet)+cols-1)//cols
    canvas=Image.new('RGB',(cols*tw,rows*th),(12,14,18))
    d=ImageDraw.Draw(canvas)
    for i,(name,img) in enumerate(sheet):
        x=(i%cols)*tw; y=(i//cols)*th
        canvas.paste(img.resize((tw,th-16)),(x,y))
        d.text((x+6,y+th-14),name.replace('boss-','').replace('drone-','drone ').replace('mob-','mob '),fill=(210,215,225))
    canvas.save(OUT/'contact-sheet.png')
    print('contact sheet ->', OUT/'contact-sheet.png')

main()
