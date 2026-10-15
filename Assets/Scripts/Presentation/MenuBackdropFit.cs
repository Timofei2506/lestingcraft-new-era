using UnityEngine;
using UnityEngine.UI;

namespace LestingCraft.Presentation
{
    public sealed class MenuBackdropFit : MonoBehaviour
    {
        RawImage image;
        void OnEnable(){image=GetComponent<RawImage>();Refresh();}
        void OnRectTransformDimensionsChange(){Refresh();}
        void Refresh()
        {
            if(!image)image=GetComponent<RawImage>();if(!image||!image.texture)return;
            var rect=image.rectTransform.rect;if(rect.width<=0||rect.height<=0)return;
            float screen=rect.width/rect.height,art=(float)image.texture.width/image.texture.height;
            image.uvRect=screen>art?new Rect(0,(1-art/screen)*.5f,1,art/screen):new Rect((1-screen/art)*.5f,0,screen/art,1);
        }
    }
}
