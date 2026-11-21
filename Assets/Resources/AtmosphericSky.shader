Shader "LestingCraft/AtmosphericSky"
{
    Properties
    {
        _CloudNoise("Cloud noise",3D)="white"{}
        _CloudOffset("Wind advection",Vector)=(0,0,0,0)
        _CloudCoverage("Coverage",Float)=.4
        _CloudBase("Base",Float)=144
        _CloudSteps("Cloud steps",Float)=22
        _HorizonGlow("Horizon glow",Float)=1.0
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            TEXTURE3D(_CloudNoise); SAMPLER(sampler_CloudNoise);
            float _CloudCoverage, _CloudBase, _CloudSteps, _LCCloudSteps;
            float4 _CloudOffset;
            float _HorizonGlow;
            struct A { float4 vertex:POSITION; };
            struct V { float4 vertex:SV_POSITION; float3 ray:TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                o.vertex=TransformObjectToHClip(i.vertex.xyz);
                o.ray=i.vertex.xyz;
                return o;
            }

            // Curved planetary cloud dome intersection keeps horizon march bounded and free of flat-slab moire.
            float DomeHit(float camY, float3 ray, float h)
            {
                const float R = 4800.0;
                float ry = R + camY;
                float rh = R + h;
                float b = ry * ray.y;
                float c = ry * ry - rh * rh;
                float disc = b * b - c;
                if (disc <= 0.0) return -1.0;
                return max(0.0, -b + sqrt(disc));
            }

            float Density(float3 p, float hLocal, float along, bool detail)
            {
                float low = saturate(hLocal / 10.0) * saturate((48.0 - hLocal) / 16.0);
                float mid = saturate((hLocal - 44.0) / 12.0) * saturate((86.0 - hLocal) / 15.0) * .40;
                float high = saturate((hLocal - 88.0) / 9.0) * saturate((122.0 - hLocal) / 13.0) * .26;
                float profile = max(max(low, mid), high);
                if (profile <= 0) return 0;

                float3 shifted = p + _CloudOffset.xyz;
                float2 rotated = mul(float2x2(.848, -.530, .530, .848), shifted.xz);
                float3 uv = float3(rotated.x, hLocal * .82, rotated.y) * .0028;
                float base = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, uv, 0).r;
                float threshold = .62 - _CloudCoverage * .30;
                if (base < threshold - .08) return 0;

                float shape = base;
                if (detail)
                {
                    // Distance-attenuate high-frequency octaves so non-mipmapped 64^3 noise never aliases near the horizon.
                    float fineWeight = 1.0 - smoothstep(1800.0, 4200.0, along);
                    float microWeight = 1.0 - smoothstep(900.0, 2200.0, along);
                    float fine = fineWeight > .01
                        ? SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, uv * 2.53 + float3(.49, .13, .67), 0).r
                        : base;
                    float micro = microWeight > .01
                        ? SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, uv * 5.17 + float3(.81, .22, .91), 0).r
                        : fine;
                    float fMix = .22 * fineWeight;
                    float mMix = .09 * microWeight;
                    shape = base * (1.0 - fMix - mMix) + fine * fMix + micro * mMix;
                    shape -= .045 * fineWeight * (1.0 - fine) * saturate((shape - threshold) * 7.0);
                }
                return saturate((shape - threshold) * 5.2) * profile;
            }

            half4 Frag(V i):SV_Target
            {
                float3 ray = normalize(i.ray);
                half3 background = LC_SkyRadiance(ray);
                float dither = (LC_IGN(i.vertex.xy) + LC_IGN(i.vertex.yx + 17.3) - 1.0) * (0.8 / 255.0);
                if (ray.y < 0)
                {
                    half3 ground = lerp(_EnvironmentHorizon.rgb * .88, _EnvironmentZenith.rgb * .38, saturate(-ray.y * 2.4));
                    return half4(max(0, ground + dither), 1);
                }
                if (ray.y < .008 || _CloudCoverage < .015)
                    return half4(max(0, background + dither), 1);

                const float R = 4800.0;
                float camY = _WorldSpaceCameraPos.y;
                float start = DomeHit(camY, ray, _CloudBase);
                float end = DomeHit(camY, ray, _CloudBase + 122.0);
                end = min(end, start + 2600.0);
                if (start < 0 || end <= start)
                    return half4(max(0, background + dither), 1);

                float3 sunDir = _EnvironmentSunDirection.y > -.025
                    ? _EnvironmentSunDirection.xyz : _EnvironmentMoonDirection.xyz;
                float mu = clamp(dot(ray, sunDir), -1, 1);
                int baseSteps = (int)clamp(_LCCloudSteps > 0 ? _LCCloudSteps : _CloudSteps, 16, 56);
                // Increase step density toward low grazing angles where path length is longest!
                int count = clamp((int)ceil(baseSteps * lerp(1.25, .90, saturate(ray.y * 2.5))), 16, 56);
                float stepLength = (end - start) / count;
                float transmittance = 1;
                half3 clouds = 0;
                float jitter = .25 + .50 * LC_IGN(i.vertex.xy);

                half3 direct = lerp(half3(.055, .075, .13),
                    lerp(_EnvironmentSunColor.rgb, _EnvironmentSunColor.rgb * half3(1.06, .90, .74), saturate(1 - _EnvironmentSunDirection.y * 3)),
                    _EnvironmentDaylight);
                half3 ambient = lerp(_EnvironmentHorizon.rgb, _EnvironmentZenith.rgb, .36) *
                    lerp(.38, .74, _EnvironmentDaylight);
                float forward = .85 * (1 - .70 * .70) / pow(max(.05, 1 + .70 * .70 - 2 * .70 * mu), 1.5)
                    + .15 * (1 - .25 * .25) / pow(max(.05, 1 + .25 * .25 + 2 * .25 * mu), 1.5);

                [loop] for (int n = 0; n < 56; n++)
                {
                    if (n >= count || transmittance < .02) break;
                    float along = start + (n + jitter) * stepLength;
                    float3 p = _WorldSpaceCameraPos + ray * along;
                    // Local altitude above spherical planetary surface
                    float3 sp = float3(ray.x * along, R + camY + ray.y * along, ray.z * along);
                    float hLocal = length(sp) - R - _CloudBase;
                    float d = Density(p, hLocal, along, true);
                    if (d < .003) continue;

                    float s1 = Density(p + sunDir * 24, hLocal + sunDir.y * 24, along, false);
                    float s2 = Density(p + sunDir * 64, hLocal + sunDir.y * 64, along, false) * .58;
                    float sunlight = exp(-(s1 + s2) * 3.4);
                    float powder = 1 - exp(-d * stepLength * .14);
                    float alpha = 1 - exp(-d * stepLength * .088);
                    float silver = min(forward, 6.0) * (1 - d) * sunlight * powder * (.36 + .52 * saturate(mu)) * saturate(mu + .15);
                    float interior = saturate(hLocal / 40.0);
                    half3 ambientLayer = ambient * lerp(.58, 1.12, interior);
                    half3 warm = lerp(half3(1, 1, 1), _EnvironmentSunColor.rgb * float3(1.16, .84, .56),
                        saturate(1 - _EnvironmentSunDirection.y * 3.0));
                    half3 lighting = ambientLayer + direct * (sunlight * .60 + min(forward, 6.0) * .04) + silver * warm * .65;
                    lighting = lerp(lighting, background, (1 - exp(-along * .00028)) * .82);
                    clouds += lighting * alpha * transmittance;
                    transmittance *= 1 - alpha;
                }

                float horizon = smoothstep(.008, .065, ray.y);
                half3 glow = half3(0, 0, 0);
                float lowSunFactor = saturate(1 - _EnvironmentSunDirection.y * 2.6);
                if (_HorizonGlow > 0 && ray.y < .38 && lowSunFactor > .001)
                {
                    float band = exp(-pow((ray.y - .065) * 9.0, 2));
                    float sunSide = saturate(dot(ray, sunDir));
                    glow = half3(1.02, .60, .28) * band * pow(sunSide, 3.2) * _EnvironmentDaylight * .42 * lowSunFactor * _HorizonGlow;
                }
                half3 result = lerp(background, clouds + background * transmittance, horizon) + glow;
                return half4(max(0, result + dither), 1);
            }
            ENDHLSL
        }
    }
}
