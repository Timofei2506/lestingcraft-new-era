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
        // Frontier sky effects (0 none, 1 aurora, 2 lightning, 3 spores, 4 void fractures,
        // 5 ember updraft). Set globally per realm; the skybox shader stays one draw call.
        _LCSkyEffect("Sky effect",Float)=0
        _LCSkyStrength("Sky effect strength",Float)=0
        _LCSkyParams("Sky effect params (time,freq,height,seed)",Vector)=(0,1,0,0)
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

            // ------------------------------------------------------------------
            // Frontier sky effects. One global block (_LCSkyEffect picks the mode,
            // _LCSkyParams carries timing and strength) so every realm reuses the same
            // skybox draw and the mode is switched by the presets instead of by a new
            // material per world.
            //   0 none · 1 aurora curtains · 2 lightning · 3 spore drift
            //   4 void fractures · 5 ember updraft · 6 sea haze
            // ------------------------------------------------------------------
            float _LCSkyEffect, _LCSkyStrength;
            float4 _LCSkyParams;   // x time, y frequency, z height, w seed
            float _LCStormFlash;   // 1 while the realm is a lightning realm
            float4 _LCSkyTint;     // realm horizon/equator tint, set by AtmosphereLighting

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Aurora: vertical curtains whose X coordinate is warped by time and by noise, with a
            // soft top and bottom edge so the band reads as light in the atmosphere, not a decal.
            half3 Aurora(float3 ray)
            {
                float t = _LCSkyParams.x * .045;
                float band = saturate((ray.y - .03) * 3.4) * saturate((.92 - ray.y) * 2.1);
                float2 uv = float2(ray.x / max(.16, ray.y + .35), ray.z / max(.16, ray.y + .35));
                float warp = ValueNoise(uv * 1.4 + float2(t * .35, t * .12)) * 1.5;
                float curtain = ValueNoise(float2(uv.x * 2.1 + warp, uv.y * .55 - t * .20));
                float ridge = pow(saturate(curtain * 1.35 - .22), 2.1);
                float veins = pow(saturate(ValueNoise(float2(uv.x * 7.0 + warp * 2.0, uv.y * 1.2 + t)) * 1.5 - .35), 3.0);
                float gy = saturate((ray.y - .10) * 1.6);
                half3 green = half3(.16, 1.00, .62);
                half3 teal  = half3(.10, .74, 1.00);
                half3 violet= half3(.52, .18, .96);
                half3 tint = lerp(green, teal, gy) + violet * pow(gy, 2.4) * .8;
                tint = lerp(tint, tint * .60 + _LCSkyTint.rgb * 1.10, .35);
                return tint * (ridge * .70 + veins * .55) * band * _LCSkyStrength;
            }

            // Lightning: a fixed few-cell branch network, gated by a pulsing strike window so the
            // sky flashes on a beat instead of strobing every frame. Cheap enough on mobile.
            half3 Lightning(float3 ray)
            {
                float strike = pow(saturate(sin(_LCSkyParams.x * 1.35 + _LCSkyParams.w) * .5 + .5), 14.0);
                if (strike < .01) return 0;
                float2 uv = float2(ray.x, ray.z) / max(.10, ray.y + .22);
                float2 cell = floor(uv * 3.0 + _LCSkyParams.w);
                float bolt = 0;
                for (int k = 0; k < 3; k++)
                {
                    float2 c = cell + float2(k * 3.7, k * -5.1);
                    float h = Hash21(c);
                    if (h > .58) continue;
                    float2 centre = float2(h, frac(h * 7.13)) * 2.0 - 1.0;
                    float2 d = uv - centre;
                    float line = saturate(1.0 - abs(d.x * 1.4 + d.y * .22) * 46.0);
                    bolt += line * (1.0 - saturate(abs(d.y) * 1.4));
                }
                half3 cold = lerp(half3(.82, .90, 1.0), _LCSkyTint.rgb + .55, .35);
                return cold * bolt * strike * _LCSkyStrength * 1.7 * max(.35, _LCStormFlash);
            }

            // Spore drift: slow luminous motes rising through the canopy light.
            half3 Spores(float3 ray)
            {
                float t = _LCSkyParams.x * .08;
                float2 uv = float2(ray.x, ray.z) / max(.25, ray.y + .5);
                float2 p = uv * 5.0 + float2(t * .6, -t * .9);
                float2 cell = floor(p);
                float h = Hash21(cell + _LCSkyParams.w);
                float2 centre = float2(h, frac(h * 5.7));
                float d = length(frac(p) - centre);
                float mote = saturate(1.0 - d * 7.0);
                half3 spore = lerp(half3(.72, 1.0, .46), _LCSkyTint.rgb * .8 + .30, .25);
                return spore * mote * (0.35 + h * .65) * _LCSkyStrength * saturate((ray.y - .05) * 2.2);
            }

            // Void fractures: thin bright seams across the black sky, like cracks in the world shell.
            half3 Fractures(float3 ray)
            {
                float t = _LCSkyParams.x * .02;
                float2 uv = float2(ray.x, ray.z) / max(.12, ray.y + .30);
                float2 warp = float2(ValueNoise(uv * 1.7 + t), ValueNoise(uv * 1.7 - t + 11.3));
                float n = ValueNoise(uv * 2.6 + warp * 2.2 + t * .4);
                float seam = pow(saturate(1.0 - abs(n - .5) * 12.0), 2.4);
                half3 violet = lerp(half3(.62, .34, 1.0), _LCSkyTint.rgb * 1.6 + .18, .30);
                return violet * seam * _LCSkyStrength;
            }

            // Ember updraft: warm sparks and heat shimmer against the ash horizon.
            half3 EmberUpdraft(float3 ray)
            {
                float t = _LCSkyParams.x * .16;
                float2 uv = float2(ray.x, ray.z) / max(.20, ray.y + .40);
                float2 p = uv * 4.0 + float2(t * .18, -t * 1.7);
                float2 cell = floor(p);
                float h = Hash21(cell + _LCSkyParams.w);
                float2 centre = float2(h, frac(h * 3.9));
                float d = length((frac(p) - centre) * float2(1.0, .6));
                float spark = saturate(1.0 - d * 6.0);
                float horizonBias = saturate(1.0 - ray.y * 2.4);
                half3 ember = lerp(half3(1.0, .46, .12), _LCSkyTint.rgb * 1.3 + .25, .30);
                return ember * spark * horizonBias * _LCSkyStrength;
            }

            // Sea haze: slow, wide veils of humid air stacked toward the horizon, with a faint
            // bioluminescent shimmer riding on them. Deliberately low-contrast: it must read as
            // air, not as clouds.
            half3 SeaHaze(float3 ray)
            {
                float t = _LCSkyParams.x * .05;
                float2 uv = float2(ray.x, ray.z) / max(.22, ray.y + .46);
                float veil = ValueNoise(uv * 1.15 + float2(t * .30, t * .07));
                veil += ValueNoise(uv * 2.7 - float2(t * .18, t * .05)) * .5;
                float horizonBias = saturate(1.0 - ray.y * 1.9);
                float shimmer = pow(saturate(ValueNoise(uv * 9.0 + float2(-t * .7, t * .3)) * 1.4 - .30), 3.0);
                half3 salt = lerp(half3(.66, .96, .98), _LCSkyTint.rgb + .22, .45);
                half3 glow = half3(.36, 1.0, .88);
                return (salt * veil * .55 + glow * shimmer * .45) * horizonBias * _LCSkyStrength;
            }

            half3 SkyEffects(float3 ray)
            {
                int mode = (int)clamp(_LCSkyEffect, 0.0, 6.0);
                if (mode == 0 || _LCSkyStrength <= 0.001) return 0;
                if (mode == 1) return Aurora(ray);
                if (mode == 2) return Lightning(ray);
                if (mode == 3) return Spores(ray);
                if (mode == 4) return Fractures(ray);
                if (mode == 5) return EmberUpdraft(ray);
                if (mode == 6) return SeaHaze(ray);
                return 0;
            }

            half4 Frag(V i):SV_Target
            {
                float3 ray = normalize(i.ray);
                half3 background = LC_SkyRadiance(ray);
                background += SkyEffects(ray);
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
