using UnityEngine;
using UnityEngine.UI;

namespace LestingCraft.Presentation
{
    public sealed class MenuBackdropFit : MonoBehaviour
    {
        RawImage image;
        void OnEnable(){image=GetComponent<RawImage>();Refresh();}
        void OnRectTransformDimensionsChange(){Refresh();}
        Rect fit;bool hasFit;
        void Refresh()
        {
            if(!image)image=GetComponent<RawImage>();if(!image||!image.texture)return;
            var rect=image.rectTransform.rect;if(rect.width<=0||rect.height<=0)return;
            float screen=rect.width/rect.height,art=(float)image.texture.width/image.texture.height;
            fit=screen>art?new Rect(0,(1-art/screen)*.5f,1,art/screen):new Rect((1-screen/art)*.5f,0,screen/art,1);
            hasFit=true;image.uvRect=fit;
        }
        // Slow cinematic drift: the key art breathes instead of hanging like a wallpaper.
        void Update()
        {
            if(!hasFit||!image)return;
            float t=Time.unscaledTime;
            float zoom=1f-.05f*(.5f+.5f*Mathf.Sin(t*.05f));
            float w=Mathf.Clamp01(fit.width*zoom),h=Mathf.Clamp01(fit.height*zoom);
            float mx=(fit.width-w)*.5f,my=(fit.height-h)*.5f;
            float px=Mathf.Sin(t*.031f)*mx*.8f,py=Mathf.Sin(t*.023f+1.7f)*my*.8f;
            image.uvRect=new Rect(fit.x+mx+px,fit.y+my+py,w,h);
        }
    }
}
