Shader "LestingCraft/Voxel"
{
    Properties { _Tint ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };
            fixed4 _Tint;
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex);
                float shade = .55 + .45 * saturate(dot(v.normal, normalize(float3(.35, .85, .4))));
                o.color = v.color * _Tint; o.color.rgb *= shade;
                UNITY_TRANSFER_FOG(o,o.vertex); return o;
            }
            fixed4 frag(v2f i) : SV_Target
            { fixed4 c = i.color; UNITY_APPLY_FOG(i.fogCoord,c); return c; }
            ENDCG
        }
    }
}
