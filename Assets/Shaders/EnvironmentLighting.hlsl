#ifndef LESTINGCRAFT_ENVIRONMENT_LIGHTING
#define LESTINGCRAFT_ENVIRONMENT_LIGHTING
float4 _EnvironmentZenith, _EnvironmentHorizon, _EnvironmentSunColor;
float4 _EnvironmentSunDirection, _EnvironmentMoonDirection;
float _EnvironmentDaylight, _EnvironmentTime, _GlobalWindStrength;

float LC_Hash(float3 p)
{
    p = frac(p * .1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float LC_IGN(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(.06711056, .00583715))));
}

// Analytic single-scattering sky radiance with clean Rayleigh/Mie balance and crisp horizon.
float3 LC_SkyRadiance(float3 direction)
{
    float3 ray = normalize(direction);
    float h = saturate(ray.y);
    float3 sunDirection = _EnvironmentSunDirection.xyz;
    float mu = clamp(dot(ray, sunDirection), -1, 1);
    float sunElevation = sunDirection.y;
    float daylight = saturate(_EnvironmentDaylight);
    float atmosphere = saturate(max(_EnvironmentHorizon.r,
        max(_EnvironmentHorizon.g, _EnvironmentHorizon.b)) * 12);
    float3 sky = lerp(_EnvironmentHorizon.rgb, _EnvironmentZenith.rgb, pow(h, .52));

    float opticalMass = rcp(max(.08, h + .16 * pow(1 - h, 2)));
    float solarMass = rcp(max(.07, sunElevation + .16 * pow(1 - saturate(sunElevation), 2)));
    float3 betaRayleigh = float3(.0058, .0135, .0331);
    float3 transmission = exp(-(betaRayleigh * 5.5 + .007) * solarMass);
    float rayleighPhase = .0596831 * (1 + mu * mu);
    float g = .76;
    float miePhase = .0795775 * (1 - g * g) /
        pow(max(.035, 1 + g * g - 2 * g * mu), 1.5);
    float3 scattering = (1 - exp(-betaRayleigh * opticalMass * 2.6)) *
        transmission * rayleighPhase * 1.15;
    scattering += transmission * miePhase * .0075 * (1 - exp(-opticalMass * .10));
    sky += scattering * daylight * atmosphere;

    float twilight = (1 - smoothstep(.02, .22, abs(sunElevation + .035))) * atmosphere;
    float band = exp(-pow((ray.y - .08) * 8.5, 2));
    sky += float3(1.02, .44, .18) * band * twilight * (.42 + .58 * saturate(mu));
    float upperBand = exp(-pow((ray.y - .28) * 4.8, 2));
    sky += float3(.16, .11, .28) * upperBand * twilight * .48;
    sky *= 1 - float3(.04, .08, .02) * band * twilight;

    float sun = saturate(mu);
    float solarVisibility = smoothstep(-.025, .025, sunElevation);
    float disk = smoothstep(.999984, .999994, sun);
    float aureole = pow(sun, 72) * .085 + pow(sun, 280) * .085 + pow(sun, 4096) * .42;
    float3 warmGlow = lerp(_EnvironmentSunColor.rgb,
        _EnvironmentSunColor.rgb * float3(1.14, .80, .52), saturate(1 - sunElevation * 2.4));
    sky += warmGlow * (disk * 22 + aureole * atmosphere) * solarVisibility;

    float lowSun = saturate(1 - sunElevation * 3.0);
    float pillar = exp(-abs(ray.y - .26f) * 6.0f) * pow(saturate(mu * .5 + .5), 20) * lowSun * atmosphere;
    sky += warmGlow * pillar * .24f * solarVisibility;
    float horizonGlow = exp(-abs(ray.y - .015) * 6.5) * pow(saturate(mu * .5 + .5), 7);
    sky += warmGlow * horizonGlow * .18 * lowSun * daylight * atmosphere;

    float moon = saturate(dot(ray, _EnvironmentMoonDirection.xyz));
    float night = 1 - daylight;
    sky += float3(.46, .59, .82) * smoothstep(.9999845, .999995, moon) * night * 1.25;

    float2 angular = float2(atan2(ray.x, ray.z) * .15915494 + .5,
        asin(clamp(ray.y, -1, 1)) * .63661977);
    float2 starUV = angular * float2(1100, 550);
    float2 cell = floor(starUV), delta = frac(starUV) - .5;
    float hash = LC_Hash(float3(cell, 17));
    float footprint = max(length(ddx(starUV)), length(ddy(starUV)));
    float radius = max(.11, min(.55, footprint * .42));
    float star = step(.994, hash) * exp(-dot(delta, delta) / (radius * radius)) *
        min(1, .025 / (radius * radius));
    float twinkle = .88 + .12 * sin(_EnvironmentTime * (1.2 + hash * 2) + hash * 95);
    float3 starColor = lerp(float3(.60, .74, 1), float3(1, .78, .55), LC_Hash(float3(cell, 31)));
    float horizonFade = saturate(ray.y * 3);
    sky += star * twinkle * starColor * night * night * horizonFade * 2;
    float star2 = step(.9986, LC_Hash(float3(cell * 2 + 11, 53))) *
        exp(-dot(delta * 1.8f, delta * 1.8f) / (radius * radius)) * min(1, .018f / (radius * radius));
    sky += star2 * twinkle * starColor * night * night * horizonFade * 1.4f;

    float galacticPlane = dot(ray, normalize(float3(.35, .78, -.51)));
    float bandShape = exp(-galacticPlane * galacticPlane * 95);
    float dust = .58 + .23 * sin(angular.x * 41 + sin(angular.y * 27)) +
        .12 * sin(angular.x * 113 + angular.y * 29);
    sky += float3(.018, .020, .034) * bandShape * saturate(dust) *
        night * night * horizonFade;
    return max(0, sky);
}

float _LCGrassRange;
float3 LC_BladePosition(float3 positionWS, float rootHeight, float weight)
{
    float distanceFade = 1 - smoothstep(max(20, _LCGrassRange * .65),
        max(42, _LCGrassRange), distance(positionWS.xz, _WorldSpaceCameraPos.xz));
    positionWS.y = rootHeight + (positionWS.y - rootHeight) * distanceFade;
    float gust = sin(dot(positionWS.xz, float2(.34, .19)) - _EnvironmentTime * 1.8);
    gust *= .75 + .25 * sin(dot(positionWS.xz, float2(.07, -.09)) - _EnvironmentTime * .43);
    float flutter = sin(positionWS.x * 2.1 + positionWS.z * 1.3 + _EnvironmentTime * 3.4) * .22;
    flutter += sin(positionWS.z * 7.3 - _EnvironmentTime * 8.1) * .045 * weight;
    positionWS.xz += float2(.8, .35) * (gust + flutter) *
        (.025 + _GlobalWindStrength * .12) * weight * weight * distanceFade;
    return positionWS;
}
#endif
