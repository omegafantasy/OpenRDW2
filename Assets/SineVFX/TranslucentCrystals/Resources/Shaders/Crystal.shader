// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SineVFX/TranslucentCrystals/Crystal"
{
	Properties
	{
		[Header(Translucency)]
		_Translucency("Strength", Range( 0 , 50)) = 1
		_TransNormalDistortion("Normal Distortion", Range( 0 , 1)) = 0.1
		_TransScattering("Scaterring Falloff", Range( 1 , 50)) = 2
		_TransDirect("Direct", Range( 0 , 1)) = 1
		_TransAmbient("Ambient", Range( 0 , 1)) = 0.2
		_TranslucencyMask("Translucency Mask", 2D) = "white" {}
		_TransShadow("Shadow", Range( 0 , 1)) = 0.9
		_TranslucencyMaskPower("Translucency Mask Power", Range( 0 , 1)) = 1
		_AlbedoMask("Albedo Mask", 2D) = "white" {}
		_ColorTint1("Color Tint 1", Color) = (1,1,1,1)
		_ColorTint2("Color Tint 2", Color) = (1,1,1,1)
		_Normal("Normal", 2D) = "bump" {}
		_Emission("Emission", 2D) = "white" {}
		_EmissionColor("Emission Color", Color) = (1,1,1,1)
		_EmissionPower("Emission Power", Range( 0 , 10)) = 2
		[Toggle]_RampEnabled("Ramp Enabled", Int) = 0
		[Toggle]_RampInverted("Ramp Inverted", Int) = 0
		_Ramp("Ramp", 2D) = "white" {}
		_RampMask("Ramp Mask", 2D) = "white" {}
		_MetallicSmoothness("MetallicSmoothness", 2D) = "white" {}
		_Metallic("Metallic", Range( 0 , 1)) = 1
		_Smoothness("Smoothness", Range( 0 , 1)) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#include "UnityPBSLighting.cginc"
		#pragma target 3.0
		#pragma shader_feature _RAMPINVERTED_ON
		#pragma shader_feature _RAMPENABLED_ON
		#pragma surface surf StandardCustom keepalpha addshadow fullforwardshadows exclude_path:deferred 
		struct Input
		{
			float2 uv_texcoord;
		};

		struct SurfaceOutputStandardCustom
		{
			fixed3 Albedo;
			fixed3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			fixed Alpha;
			fixed3 Translucency;
		};

		uniform sampler2D _Normal;
		uniform float4 _Normal_ST;
		uniform float4 _ColorTint1;
		uniform float4 _ColorTint2;
		uniform sampler2D _AlbedoMask;
		uniform float4 _AlbedoMask_ST;
		uniform sampler2D _Emission;
		uniform float4 _Emission_ST;
		uniform sampler2D _Ramp;
		uniform sampler2D _RampMask;
		uniform float4 _RampMask_ST;
		uniform float _EmissionPower;
		uniform float4 _EmissionColor;
		uniform sampler2D _MetallicSmoothness;
		uniform float4 _MetallicSmoothness_ST;
		uniform float _Metallic;
		uniform float _Smoothness;
		uniform half _Translucency;
		uniform half _TransNormalDistortion;
		uniform half _TransScattering;
		uniform half _TransDirect;
		uniform half _TransAmbient;
		uniform half _TransShadow;
		uniform sampler2D _TranslucencyMask;
		uniform float4 _TranslucencyMask_ST;
		uniform float _TranslucencyMaskPower;

		inline half4 LightingStandardCustom(SurfaceOutputStandardCustom s, half3 viewDir, UnityGI gi )
		{
			#if !DIRECTIONAL
			float3 lightAtten = gi.light.color;
			#else
			float3 lightAtten = lerp( _LightColor0.rgb, gi.light.color, _TransShadow );
			#endif
			half3 lightDir = gi.light.dir + s.Normal * _TransNormalDistortion;
			half transVdotL = pow( saturate( dot( viewDir, -lightDir ) ), _TransScattering );
			half3 translucency = lightAtten * (transVdotL * _TransDirect + gi.indirect.diffuse * _TransAmbient) * s.Translucency;
			half4 c = half4( s.Albedo * translucency * _Translucency, 0 );

			SurfaceOutputStandard r;
			r.Albedo = s.Albedo;
			r.Normal = s.Normal;
			r.Emission = s.Emission;
			r.Metallic = s.Metallic;
			r.Smoothness = s.Smoothness;
			r.Occlusion = s.Occlusion;
			r.Alpha = s.Alpha;
			return LightingStandard (r, viewDir, gi) + c;
		}

		inline void LightingStandardCustom_GI(SurfaceOutputStandardCustom s, UnityGIInput data, inout UnityGI gi )
		{
			UNITY_GI(gi, s, data);
		}

		void surf( Input i , inout SurfaceOutputStandardCustom o )
		{
			float2 uv_Normal = i.uv_texcoord * _Normal_ST.xy + _Normal_ST.zw;
			o.Normal = UnpackNormal( tex2D( _Normal, uv_Normal ) );
			float2 uv_AlbedoMask = i.uv_texcoord * _AlbedoMask_ST.xy + _AlbedoMask_ST.zw;
			float4 lerpResult17 = lerp( _ColorTint1 , _ColorTint2 , tex2D( _AlbedoMask, uv_AlbedoMask ).r);
			o.Albedo = lerpResult17.rgb;
			float2 uv_Emission = i.uv_texcoord * _Emission_ST.xy + _Emission_ST.zw;
			float4 tex2DNode5 = tex2D( _Emission, uv_Emission );
			float2 uv_RampMask = i.uv_texcoord * _RampMask_ST.xy + _RampMask_ST.zw;
			float4 tex2DNode22 = tex2D( _RampMask, uv_RampMask );
			#ifdef _RAMPINVERTED_ON
				float staticSwitch26 = ( 1.0 - tex2DNode22.r );
			#else
				float staticSwitch26 = tex2DNode22.r;
			#endif
			float2 appendResult23 = (float2(staticSwitch26 , 0.0));
			#ifdef _RAMPENABLED_ON
				float4 staticSwitch20 = ( tex2DNode5 * tex2D( _Ramp, appendResult23 ) * _EmissionPower );
			#else
				float4 staticSwitch20 = ( tex2DNode5 * _EmissionColor * _EmissionPower );
			#endif
			o.Emission = staticSwitch20.rgb;
			float2 uv_MetallicSmoothness = i.uv_texcoord * _MetallicSmoothness_ST.xy + _MetallicSmoothness_ST.zw;
			float4 tex2DNode12 = tex2D( _MetallicSmoothness, uv_MetallicSmoothness );
			o.Metallic = ( tex2DNode12.r * _Metallic );
			o.Smoothness = ( tex2DNode12.a * _Smoothness );
			float2 uv_TranslucencyMask = i.uv_texcoord * _TranslucencyMask_ST.xy + _TranslucencyMask_ST.zw;
			float3 temp_cast_2 = (( tex2D( _TranslucencyMask, uv_TranslucencyMask ).r * _TranslucencyMaskPower )).xxx;
			o.Translucency = temp_cast_2;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=13901
7;29;1906;1004;2693.809;689.402;1;True;False
Node;AmplifyShaderEditor.SamplerNode;22;-2179.814,-214.3205;Float;True;Property;_RampMask;Ramp Mask;18;0;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.OneMinusNode;27;-1799.809,-273.402;Float;False;1;0;FLOAT;0.0;False;1;FLOAT
Node;AmplifyShaderEditor.StaticSwitch;26;-1541.809,-220.402;Float;False;Property;_RampInverted;Ramp Inverted;16;0;0;False;True;;2;0;FLOAT;0.0;False;1;FLOAT;0.0;False;1;FLOAT
Node;AmplifyShaderEditor.RangedFloatNode;24;-1426.426,-113.8341;Float;False;Constant;_Float0;Float 0;16;0;0;0;0;0;1;FLOAT
Node;AmplifyShaderEditor.DynamicAppendNode;23;-1173.693,-216.2024;Float;False;FLOAT2;4;0;FLOAT;0.0;False;1;FLOAT;0.0;False;2;FLOAT;0.0;False;3;FLOAT;0.0;False;1;FLOAT2
Node;AmplifyShaderEditor.ColorNode;7;-889.7004,136.3475;Float;False;Property;_EmissionColor;Emission Color;13;0;1,1,1,1;0;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.SamplerNode;21;-990.6935,-243.1058;Float;True;Property;_Ramp;Ramp;17;0;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.RangedFloatNode;8;-946.7004,304.3475;Float;False;Property;_EmissionPower;Emission Power;14;0;2;0;10;0;1;FLOAT
Node;AmplifyShaderEditor.SamplerNode;5;-979.7004,-52.65241;Float;True;Property;_Emission;Emission;12;0;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.RangedFloatNode;11;-581.059,1448.186;Float;False;Property;_TranslucencyMaskPower;Translucency Mask Power;7;0;1;0;1;0;1;FLOAT
Node;AmplifyShaderEditor.SamplerNode;9;-597.8098,1245.612;Float;True;Property;_TranslucencyMask;Translucency Mask;6;0;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.RangedFloatNode;16;-506.0231,881.6746;Float;False;Property;_Smoothness;Smoothness;21;0;1;0;1;0;1;FLOAT
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;25;-582.6212,-169.435;Float;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0.0,0,0,0;False;2;FLOAT;0,0,0,0;False;1;COLOR
Node;AmplifyShaderEditor.SamplerNode;12;-609.6003,599.6158;Float;True;Property;_MetallicSmoothness;MetallicSmoothness;19;0;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;6;-590.6995,43.34745;Float;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0,0,0,0;False;1;COLOR
Node;AmplifyShaderEditor.RangedFloatNode;14;-505.0231,797.6746;Float;False;Property;_Metallic;Metallic;20;0;1;0;1;0;1;FLOAT
Node;AmplifyShaderEditor.ColorNode;18;-575.705,-1119.544;Float;False;Property;_ColorTint2;Color Tint 2;10;0;1,1,1,1;0;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.ColorNode;3;-577.106,-1296.171;Float;False;Property;_ColorTint1;Color Tint 1;9;0;1,1,1,1;0;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.SamplerNode;1;-639.106,-941.1714;Float;True;Property;_AlbedoMask;Albedo Mask;8;0;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;COLOR;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-217.0591,1350.186;Float;False;2;2;0;FLOAT;0.0;False;1;FLOAT;0.0;False;1;FLOAT
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;13;-123.0231,692.6746;Float;False;2;2;0;FLOAT;0.0;False;1;FLOAT;0.0;False;1;FLOAT
Node;AmplifyShaderEditor.LerpOp;17;-200.2047,-1089.245;Float;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0.0,0,0,0;False;2;FLOAT;0.0,0,0,0;False;1;COLOR
Node;AmplifyShaderEditor.StaticSwitch;20;-321.9658,-72.04562;Float;False;Property;_RampEnabled;Ramp Enabled;15;0;0;False;True;;2;0;COLOR;0.0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR
Node;AmplifyShaderEditor.SamplerNode;4;-637.4258,-737.2113;Float;True;Property;_Normal;Normal;11;0;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0.0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1.0;False;5;FLOAT3;FLOAT;FLOAT;FLOAT;FLOAT
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;15;-122.0231,798.6746;Float;False;2;2;0;FLOAT;0.0;False;1;FLOAT;0.0;False;1;FLOAT
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;470.7228,78.47247;Float;False;True;2;Float;ASEMaterialInspector;0;0;Standard;SineVFX/TranslucentCrystals/Crystal;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;0;False;0;0;Translucent;0.5;True;True;0;False;Opaque;Transparent;ForwardOnly;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;False;0;255;255;0;0;0;0;0;0;0;0;False;2;15;10;25;False;0.5;True;0;Zero;Zero;0;Zero;Zero;OFF;OFF;0;False;0;0,0,0,0;VertexOffset;False;Cylindrical;False;Relative;0;;-1;0;-1;-1;0;0;0;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0.0;False;4;FLOAT;0.0;False;5;FLOAT;0.0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0.0;False;9;FLOAT;0.0;False;10;FLOAT;0.0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;27;0;22;1
WireConnection;26;0;27;0
WireConnection;26;1;22;1
WireConnection;23;0;26;0
WireConnection;23;1;24;0
WireConnection;21;1;23;0
WireConnection;25;0;5;0
WireConnection;25;1;21;0
WireConnection;25;2;8;0
WireConnection;6;0;5;0
WireConnection;6;1;7;0
WireConnection;6;2;8;0
WireConnection;10;0;9;1
WireConnection;10;1;11;0
WireConnection;13;0;12;1
WireConnection;13;1;14;0
WireConnection;17;0;3;0
WireConnection;17;1;18;0
WireConnection;17;2;1;1
WireConnection;20;0;25;0
WireConnection;20;1;6;0
WireConnection;15;0;12;4
WireConnection;15;1;16;0
WireConnection;0;0;17;0
WireConnection;0;1;4;0
WireConnection;0;2;20;0
WireConnection;0;3;13;0
WireConnection;0;4;15;0
WireConnection;0;7;10;0
ASEEND*/
//CHKSM=90A0784883DCC12283A7AF077ECB0B91A5C6700C