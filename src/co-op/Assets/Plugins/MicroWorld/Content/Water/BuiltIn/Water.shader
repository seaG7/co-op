Shader "MicroWorld/Water" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_ColorIntencity("Color Intencity", Range(0, 1)) = 1
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 1
		_Metallic ("Metallic", Range(-1,1)) = 1

		_BumpMap("Normal Map", 2D) = "bump" {}
		_BumpScale("Normal Map Scale", Range(-1,2)) = 1
		_Refraction("Refraction", Range(0, 100)) = 20
		_Lightening("Lightening", range(0, 2)) = 1.25

		_RimEffect("Rim Effect", Range(0, 2)) = 1
		_Speed("Waves Speed", range(0, 1)) = 1
	}

	SubShader {
		//Tags{ "RenderType" = "Opaque" "Queue" = "Overlay" "LightMode" = "Always" }
		Tags{ "RenderType" = "Opaque" "Queue" = "Overlay" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
		
		LOD 200
		Cull Off
		ZWrite On

		GrabPass
		{
			Tags{ "LightMode" = "Always" }
		}

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows
		//#pragma surface surf Standard NoLighting

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		#include "UnityCG.cginc"

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float4 screenPos;
			float3 viewDir;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
		sampler2D _GrabTexture;
		float4 _GrabTexture_TexelSize;
		sampler2D _BumpMap;
		half _Refraction;
		half _BumpScale;
		half _Lightening;
		half _ColorIntencity;
		half _InverseSurface;
		half _RimEffect;
		float4 _BumpMap_ST;
		half _Speed;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) 
		{
			half2 uv = IN.uv_MainTex;
			float time = _Speed * _Time.x;

			half3 bump = UnpackScaleNormal(tex2D(_BumpMap, (uv + fixed2(time * 0.62, time * 1.1)) * _BumpMap_ST.x * 1), _BumpScale);
			bump += UnpackScaleNormal(tex2D(_BumpMap, (uv * 1.3 + fixed2(time * -0.62, time * -1.1)) * _BumpMap_ST.x * 1), _BumpScale);
			bump += UnpackScaleNormal(tex2D(_BumpMap, (uv * 1.3 + fixed2(time * 0.62, time * -1.1)) * _BumpMap_ST.x * 1), _BumpScale);
			bump += UnpackScaleNormal(tex2D(_BumpMap, (uv * 1.3 + fixed2(time * -0.62, time * 1.1)) * _BumpMap_ST.x * 1), _BumpScale);
			o.Normal = normalize(bump);

			//refraction
			#ifdef UNITY_Z_0_FAR_FROM_CLIPSPACE //to handle recent standard asset package on older version of unity (before 5.5)
				float z = UNITY_Z_0_FAR_FROM_CLIPSPACE(IN.screenPos.z);
			#else
				float z = IN.screenPos.z;
			#endif

			IN.screenPos.xy += (_Refraction * z / _BumpScale) * o.Normal.xy * _GrabTexture_TexelSize.xy;
			float4 refrColor = tex2Dproj(_GrabTexture, IN.screenPos);
			
			fixed4 tint = tex2D (_MainTex, IN.uv_MainTex);
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;


			fixed4 c = lerp((fixed4)1, tint * _Color, _ColorIntencity);

			//Reflection
			float d = dot(IN.viewDir, o.Normal);
			float border = (1 - abs(d)) * _RimEffect;
			float alpha = lerp(1, border, _Color.a);
			//
			
			o.Albedo = lerp(refrColor  * c * _Lightening, c, alpha);
			o.Alpha = 1;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
