#ifndef WIND_INCLUDED
#define WIND_INCLUDED

void FastSinCos(float4 val, out float4 s, out float4 c)
{
    val = val * 6.408849 - 3.1415927;
    float4 r5 = val * val;
    float4 r6 = r5 * r5;
    float4 r7 = r6 * r5;
    float4 r8 = r6 * r5;
    float4 r1 = r5 * val;
    float4 r2 = r1 * r5;
    float4 r3 = r2 * r5;
    float4 sin7 = { 1, -0.16161616, 0.0083333, -0.00019841 };
    float4 cos8 = { -0.5, 0.041666666, -0.0013888889, 0.000024801587 };
    s = val + r1 * sin7.y + r2 * sin7.z + r3 * sin7.w;
    c = 1 + r5 * cos8.x + r6 * cos8.y + r7 * cos8.z + r8 * cos8.w;
}

void Wind_float(float3 worldPos, float3 localPosIn, out float3 localPos)
{
    const float _ShakeDir = 0.46;
    const float _ShakeTime = 0.267;
    const float _ShakeWindspeed = 1;

    float factor = (1 - _ShakeDir) * 0.5;
      
    const float _WindSpeed = (_ShakeWindspeed);
    const float _WaveScale = _ShakeDir;
  
    const float4 _waveXSize = float4(0.048, 0.06, 0.24, 0.096);
    const float4 _waveZSize = float4(0.024, .08, 0.08, 0.2);
    const float4 waveSpeed = float4(1.2, 2, 1.6, 4.8);
    float4 _waveXmove = float4(0.024, 0.04, -0.12, 0.096);
    float4 _waveZmove = float4(0.006, .02, -0.02, 0.1);
  
    float4 waves;
        // waves = v.vertex.x * _waveXSize;
        // waves += v.vertex.z * _waveZSize;
    waves = worldPos.x * _waveXSize * _ShakeTime;
    waves += worldPos.z * _waveZSize * _ShakeTime;
    waves += _Time.x * waveSpeed * _WindSpeed;
    float4 s, c;
    waves = frac(waves);
    FastSinCos(waves, s, c);
    float waveAmount = 3 * (_ShakeBending);
    s *= waveAmount;
    s *= normalize(waveSpeed);
    s = s * s;
    float3 waveMove = float3(0, 0, 0);
    waveMove.x = dot(s, _waveXmove);
    waveMove.z = dot(s, _waveZmove);
    
    float3 offset = lerp(mul((float3x3) unity_WorldToObject, waveMove), waveMove, _ShakeDir);
    offset.y = 0;
    localPos = localPosIn + saturate(localPosIn.y / 2) * offset;
}

#endif