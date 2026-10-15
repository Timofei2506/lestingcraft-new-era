#ifndef LESTINGCRAFT_ENVIRONMENT_LIGHTING
#define LESTINGCRAFT_ENVIRONMENT_LIGHTING
float4 _EnvironmentZenith, _EnvironmentHorizon, _EnvironmentSunColor;
float4 _EnvironmentSunDirection, _EnvironmentMoonDirection;
float _EnvironmentDaylight, _EnvironmentTime, _GlobalWindStrength;
float LC_Hash(float3 p)
{
    p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);
}
float3 LC_SkyRadiance(float3 direction)
{
    float3 ray=normalize(direction);float h=saturate(ray.y);
    float3 sky=lerp(_EnvironmentHorizon.rgb,_EnvironmentZenith.rgb,pow(h,.45));
    float sun=saturate(dot(ray,_EnvironmentSunDirection.xyz));
    float daylight=saturate(_EnvironmentSunDirection.y*12+.5);
    sky+=_EnvironmentSunColor.rgb*(smoothstep(.999955,.999975,sun)*3+pow(sun,96)*.035)*daylight;
    float moon=saturate(dot(ray,_EnvironmentMoonDirection.xyz));
    float night=1-_EnvironmentDaylight;
    sky+=float3(.44,.57,.80)*smoothstep(.99988,.99993,moon)*night*1.2;
    // Sparse, stable stars in angular cells, fading towards the horizon/daylight.
    float2 starUV=float2(atan2(ray.x,ray.z)*.15915494+.5,asin(clamp(ray.y,-1,1))*.63661977)*float2(1100,550);
    float2 cell=floor(starUV),delta=frac(starUV)-.5;
    float star=step(.994,LC_Hash(float3(cell,17)))*exp(-dot(delta,delta)*95);
    sky+=star*night*night*saturate(ray.y*3)*float3(.65,.76,1)*1.2;
    return sky;
}
float3 LC_BladePosition(float3 positionWS,float rootHeight,float weight)
{
    float distanceFade=1-smoothstep(24,42,distance(positionWS.xz,_WorldSpaceCameraPos.xz));
    positionWS.y=rootHeight+(positionWS.y-rootHeight)*distanceFade;
    float gust=sin(dot(positionWS.xz,float2(.34,.19))-_EnvironmentTime*1.8);
    float flutter=sin(positionWS.x*2.1+positionWS.z*1.3+_EnvironmentTime*3.4)*.22;
    positionWS.xz+=float2(.8,.35)*(gust+flutter)*(.025+_GlobalWindStrength*.12)*weight*weight*distanceFade;
    return positionWS;
}
#endif
