using UnityEngine;
using UnityEngine.UI;

namespace LestingCraft.Presentation
{
    /// <summary>UI chrome does not depend on an imported PNG being available at startup.</summary>
    public static class MenuArtwork
    {
        static Sprite rounded,outline;static Texture2D shade;
        public static Sprite Rounded=>rounded?rounded:rounded=CreateRounded(false);
        public static Sprite Outline=>outline?outline:outline=CreateRounded(true);
        static Sprite CreateRounded(bool border)
        {
            const int size=64;const float radius=16;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=border?"UI generated border":"UI generated rounded chrome",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var q=new Vector2(Mathf.Abs(x-31.5f)-15.5f,Mathf.Abs(y-31.5f)-15.5f);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                float alpha=Mathf.Clamp01(.5f-distance);if(border)alpha*=Mathf.Clamp01(distance+1.6f);
                pixels[y*size+x]=new Color32(255,255,255,(byte)(alpha*255));
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
            return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(18,18,18,18));
        }
        public static Texture2D Backdrop
        {
            get
            {
                var texture=Resources.Load<Texture2D>("Generated/MenuBackdrop");
                if(!texture)texture=Resources.Load<Texture2D>("UI/MenuBackground");
                if(!texture){var sprite=Resources.Load<Sprite>("UI/MenuBackground");if(sprite)texture=sprite.texture;}
                return texture;
            }
        }
        public static Texture2D Shade
        {
            get
            {
                if(shade)return shade;
                shade=new Texture2D(128,8,TextureFormat.RGBA32,false){name="UI cinematic readability gradient",wrapMode=TextureWrapMode.Clamp};
                for(int y=0;y<8;y++)for(int x=0;x<128;x++)
                {
                    float t=x/127f,edge=Mathf.Pow(Mathf.Abs(y/7f-.5f)*2,2)*.12f;
                    float alpha=Mathf.Lerp(.91f,.08f,Mathf.SmoothStep(0,1,t))+edge;
                    shade.SetPixel(x,y,new Color(.013f,.025f,.031f,Mathf.Clamp01(alpha)));
                }
                shade.Apply();return shade;
            }
        }
    }
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
