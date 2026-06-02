#ifndef FADE_INCLUDED
#define FADE_INCLUDED

void FadeCore_float(float3 worldPos, float3 camPos, float3 worldNormal, float nearFadeDistance, float nearFadeMinAlpha, out float darkness, out float alpha, out float3 viewDir)
{
    float3 eyeVec = worldPos - camPos;
    float eyeVecLen = length(eyeVec);
    
    viewDir = eyeVec / eyeVecLen;
    
    // fade out flat polygons
    float flat = abs(dot(viewDir * 2, worldNormal));
    alpha = 1;
    alpha -= 1 - saturate(flat / (0.0001 + 10 * _HideFlat));
    
    // dark by flatness
    darkness = saturate(flat / (2 * _HideFlatDarkness));
    
    // fade out by distance
    float dist = 1 - eyeVecLen / _ViewDist;
    alpha -= 1 - saturate(dist * 5);
    alpha *= 2;

    // fade when the camera gets close to foliage
    if (nearFadeDistance > 0)
    {
        float nearFade = lerp(saturate(nearFadeMinAlpha), 1, saturate(eyeVecLen / nearFadeDistance));
        alpha *= nearFade;
    }
}

void Fade_float(float3 worldPos, float3 camPos, float3 worldNormal, out float darkness, out float alpha, out float3 viewDir)
{
    FadeCore_float(worldPos, camPos, worldNormal, 0, 0.25, darkness, alpha, viewDir);
}

void Fade_float(float3 worldPos, float3 camPos, float3 worldNormal, float nearFadeDistance, float nearFadeMinAlpha, out float darkness, out float alpha, out float3 viewDir)
{
    FadeCore_float(worldPos, camPos, worldNormal, nearFadeDistance, nearFadeMinAlpha, darkness, alpha, viewDir);
}

void AlphaCutOff_float(float AlphaIn, out float Alpha)
{
    Alpha = AlphaIn > _CutOff ? 1 : 0;
}

void ColorVariance_float(float3 pos, float crossFade, float alpha, float ao, out float4 color)
{
    color = lerp(_Color, _AltColor, frac((pos.x + pos.y) * 17)) * crossFade * ao;
    color.a = alpha;
}

void SplitAlpha_float(float4 colorIn, float2 uv, float texAlphaIn, out float3 color, out float alpha)
{
    color = colorIn.rgb;
    alpha = texAlphaIn <= _CutOff ? 0 : colorIn.a;
    
#ifdef _DOUBLE
    if (uv.x < 1)
    {
        color.rgb *= float3(0.1, 0.06, 0.04);
        alpha = colorIn.a;
    }
#endif
}

void Flower_float(float3 colorIn, out float3 color)
{
    float flower = saturate(2 * (2 * colorIn.r - colorIn.g - colorIn.b));
    color = lerp(colorIn, colorIn.r * _FlowerColor, flower * flower * _FlowerColor.a);
}

#endif
