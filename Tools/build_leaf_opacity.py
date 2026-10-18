"""Image-derived leaf openings + deterministic twig-scale gaps, not a new AI master."""
from PIL import Image,ImageFilter
import numpy as np
from pathlib import Path
root=Path(__file__).resolve().parents[1]
im=Image.open(root/'Assets/Art/PBR/leaves_albedo.png').convert('RGB').resize((512,512))
a=np.asarray(im,dtype=np.float32)/255;lum=a@np.array([.2126,.7152,.0722]);smooth=np.asarray(Image.fromarray(np.uint8(np.pad(lum,8,mode="wrap")*255)).filter(ImageFilter.GaussianBlur(2.0)),dtype=np.float32)[8:-8,8:-8]/255
# Dark image interstices follow the existing photographed foliage, with small coherent holes.
y,x=np.mgrid[0:512,0:512];gaps=np.sin(x*(2*np.pi*13/512)+np.sin(y*(2*np.pi*6/512))*1.1)*np.sin(y*(2*np.pi*15/512)+np.sin(x*(2*np.pi*5/512))*1.7)
score=smooth*.82+gaps*.030;threshold=np.quantile(score,.24);alpha=np.uint8(np.clip((score-threshold)*2048+127,0,255))
rgba=np.full((512,512,4),255,dtype=np.uint8);rgba[:,:,3]=alpha
out=root/'Assets/Resources/GeneratedLeafOpacity.png';Image.fromarray(rgba).save(out,optimize=True)
print(f'Leaf opacity: 512x512, open fraction {np.mean(alpha<128):.3f}; {out.stat().st_size} bytes')
