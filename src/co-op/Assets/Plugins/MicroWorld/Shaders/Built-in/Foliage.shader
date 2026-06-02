Shader "MicroWorld/Foliage"
{
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _AltColor ("Alt Color", Color) = (0.9,0.9,0.9,1)
    _FlowerColor ("Flower Color", Color) = (1,0,0,0)
    [NoScaleOffset] _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
    [NoScaleOffset] _BumpMap ("Bumpmap", 2D) = "bump" {} // in SPECULAR setup only
    _CutOff ("Alpha cutoff", Range (0.0, 1)) = 0.01
    [Space]
    _ViewDist ("View Distance", float) = 5000.0
    [Space]
    _HideFlat ("Hide Flat Polygons", Range(0, 0.2)) = 0.05
    _HideFlatDarkness ("Flat Polygon Darkness", Range(0.001, 1)) = 0.1
    [Space]
    _AO ("AO", Range (0, 1.0)) = 0.2
    _Brightness ("Brightness", Range (0.2, 5)) = 1
    _Smoothness ("Smoothness", Range (0, 1.0)) = 0// in SPECULAR setup only
    _ShadowCast ("Shadow Cast", Range(0, 1)) = 1
    _ShakeBending ("Shake Bending", Range (0, 1.0)) = 0.2
    [Toggle] _DOUBLE ("Double Material", Float) = 0
    [KeywordEnum(Transfluency, Specular, Ambient)] _MODE ("Mode", float) = 0
}
SubShader
{
    //Tags {"Queue" = "AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }
    //Tags {"IgnoreProjector"="True" "RenderType"="TreeLeaf"}
    Tags {"IgnoreProjector"="True" "RenderType"="TreeTransparentCutout"}
    
    LOD 200
    Cull Off
  
    CGPROGRAM
    #pragma target 3.0
    #pragma surface surf BlinnPhong alphatest:_CutOff vertex:vert addshadow nolightmap noforwardadd// WrapLambert BlinnPhong Standard 
    #pragma shader_feature _DOUBLE_ON
    #pragma shader_feature _MODE_TRANSFLUENCY _MODE_SPECULAR _MODE_AMBIENT
    #pragma multi_compile _ LOD_FADE_CROSSFADE
    //#pragma only_renderers d3d9 d3d11 glcore gles gles3 metal // forced Forward rendering
    //#pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
    #include "UnityPBSLighting.cginc"
    #include "UnityCG.cginc"
    #include "AutoLight.cginc"

    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    fixed4 _AltColor;
    fixed4 _FlowerColor;
    float _ViewDist;
    float _ShakeBending;
    float _Smoothness;
    float dist;
    float _GrassAlpha;
    float _AO;
    float _ShadowCast;
    float _ShadowReceive;
    float _ShadowViewDep;
    float _Brightness;
    float _AmbientLight;
    float _HideFlat;
    float _HideFlatDarkness;

    struct Input {
        float3 worldPos;
        fixed4 color : COLOR;
        float2 uv_MainTex: TEXCOORD0;
        float4 screenPos : VPOS;
    };

    void FastSinCos (float4 val, out float4 s, out float4 c) {
        val = val * 6.408849 - 3.1415927;
        float4 r5 = val * val;
        float4 r6 = r5 * r5;
        float4 r7 = r6 * r5;
        float4 r8 = r6 * r5;
        float4 r1 = r5 * val;
        float4 r2 = r1 * r5;
        float4 r3 = r2 * r5;
        float4 sin7 = {1, -0.16161616, 0.0083333, -0.00019841} ;
        float4 cos8  = {-0.5, 0.041666666, -0.0013888889, 0.000024801587} ;
        s =  val + r1 * sin7.y + r2 * sin7.z + r3 * sin7.w;
        c = 1 + r5 * cos8.x + r6 * cos8.y + r7 * cos8.z + r8 * cos8.w;
    }

    float2 Wind(float3 worldPos)
    {
        const float _ShakeDir = 0.46;
        const float _ShakeTime = 0.267;
        const float _ShakeWindspeed = 1;

        float factor = (1 - _ShakeDir) * 0.5;
      
        const float _WindSpeed  = (_ShakeWindspeed  );   
        const float _WaveScale = _ShakeDir;
  
        const float4 _waveXSize = float4(0.048, 0.06, 0.24, 0.096);
        const float4 _waveZSize = float4 (0.024, .08, 0.08, 0.2);
        const float4 waveSpeed = float4 (1.2, 2, 1.6, 4.8);
        float4 _waveXmove = float4(0.024, 0.04, -0.12, 0.096);
        float4 _waveZmove = float4 (0.006, .02, -0.02, 0.1);
  
        float4 waves;
        // waves = v.vertex.x * _waveXSize;
        // waves += v.vertex.z * _waveZSize;
        waves = worldPos.x * _waveXSize * _ShakeTime;
        waves += worldPos.z * _waveZSize * _ShakeTime;
        waves += _Time.x * waveSpeed *_WindSpeed;
        float4 s, c;
        waves = frac (waves);
        FastSinCos (waves, s,c);
        float waveAmount = 3 * ( _ShakeBending);
        s *= waveAmount;
        s *= normalize (waveSpeed);
        s = s * s;
        float3 waveMove = float3 (0,0,0);
        waveMove.x = dot (s, _waveXmove);
        waveMove.z = dot (s, _waveZmove);
        return lerp(mul((float3x3)unity_WorldToObject, waveMove).xz, waveMove, _ShakeDir);
    }

    void vert (inout appdata_full v)
    {
        float3 worldNormal = mul ((float3x3)unity_ObjectToWorld, v.normal);
        float3 worldPos = mul (unity_ObjectToWorld, v.vertex).xyz;
        v.vertex.xz += saturate(v.vertex.y / 2) * Wind(worldPos);
  
        float3 baseWorldPos = unity_ObjectToWorld._m03_m13_m23;
        v.color = lerp(_Color, _AltColor, frac((baseWorldPos.x + baseWorldPos.y) * 17));
        v.color.a = 1;
        v.color.rgb *= saturate(lerp(1, v.texcoord.y, _AO));
        //v.color.rgb *= lightSign;// encode light direction in color
    
        float3 eyeVec = _WorldSpaceCameraPos - worldPos;
        float eyeVecLen = length(eyeVec);

        // fade out flat polygons
        float flat = abs(dot(eyeVec / eyeVecLen * 2, worldNormal));
        //#ifndef UNITY_PASS_SHADOWCASTER
        v.color.a -= 1 - saturate( flat / (0.0001 + 10 * _HideFlat));
        //#endif
        v.color.rgb *= saturate( flat / (2 * _HideFlatDarkness));
        
        // fade on distance
        float dist = 1 - eyeVecLen / _ViewDist;
        v.color.a -= 1 - saturate(dist * 5);
        v.color.a = saturate(v.color.a);

        // translucency
        #ifdef _MODE_AMBIENT
            v.normal = mul (unity_WorldToObject, _WorldSpaceLightPos0).rgb;
        #else
            float lightSign = sign(dot (worldNormal, _WorldSpaceLightPos0.rgb));
            v.normal *= lightSign; // face to main light
        #endif
    }

    sampler2D _DitherMaskLOD2D;

    void UnityApplyDitherCrossFade2(float2 vpos, float fade)
    {
        vpos /= 4; // the dither mask texture is 4x4
        vpos.y = frac(vpos.y) * 0.0625 /* 1/16 */ + fade ;//+ unity_LODFade.y // quantized lod fade by 16 levels 
        clip(tex2D(_DitherMaskLOD2D, vpos).a - 0.5);
    }

    void surf (Input IN, inout SurfaceOutput o) //SurfaceOutputStandard SurfaceOutput
    {
        // double material
        fixed2 uv = IN.uv_MainTex;

        #ifdef _DOUBLE_ON
            uv.x *= 2;
            fixed4 c = tex2D (_MainTex, uv);
            if (uv.x >= 1)
                c.a = saturate(c.a + IN.color.a - 1);
            else
            {
                c.rgb *= float3(0.1, 0.06, 0.04);
                c.a = 1;
            }
        #else
            fixed4 c = tex2D (_MainTex, uv);
            c.a = saturate(c.a + IN.color.a - 1);
        #endif

        // Custom shadow strength adjustment
#ifdef UNITY_PASS_SHADOWCASTER
        o.Alpha = c.a * _ShadowCast;
#else
        // cross fade
        float2 coords = IN.screenPos.xy / IN.screenPos.w * _ScreenParams;
        #ifdef LOD_FADE_CROSSFADE
            UnityApplyDitherCrossFade(coords);
        #endif

        if (IN.color.a < 0.9)
            UnityApplyDitherCrossFade2(coords, saturate(IN.color.a) * 0.92);

        // flower Color
        float flower = saturate(1*(4 * c.r - c.g - c.b));
        c.rgb = lerp(c.rgb, c.r * _FlowerColor, flower * flower * _FlowerColor.a);

        //
        c.rgb *= IN.color.rgb * _Brightness;

        //
        o.Alpha = c.a;
        o.Albedo = c.rgb;

        o.Normal = UnpackNormal (tex2D (_BumpMap, uv));

        #ifdef _MODE_SPECULAR
            #if UNITY_PASS_DEFERRED
                o.Gloss = _Smoothness / 2;
                o.Specular = _Smoothness + 0.001;
                _SpecColor = float4(0.1, 0.1, 0.1, 0.1);
            #else
                o.Gloss = _Smoothness;
                o.Specular = _Smoothness + 0.001;
                _SpecColor = saturate(c.rgba * 5);//float4(1, 1, 1, 1);
            #endif
        #endif
#endif
    }
    ENDCG
}
    Fallback "Transparent/Cutout/VertexLit"
}