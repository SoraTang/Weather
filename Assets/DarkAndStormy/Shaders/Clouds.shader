Shader "Skybox/Clouds"
{
	Properties
	{
		[NoScaleOffset] _CloudTex1 ("Clouds 1", 2D) = "white" {}
		[NoScaleOffset] _FlowTex1 ("Flow Tex 1", 2D) = "grey" {}
		_Tiling1("Tiling 1", Vector) = (1,1,0,0)

		[NoScaleOffset] _CloudTex2 ("Clouds 2", 2D) = "white" {}
		[NoScaleOffset] _Tiling2("Tiling 2", Vector) = (1,1,0,0)
		_Cloud2Amount ("Cloud 2 Amount", float) = 0.5
		_FlowSpeed ("Flow Speed", float) = 1
		_FlowAmount ("Flow Amount", float) = 1

		[NoScaleOffset] _WaveTex ("Wave", 2D) = "white" {}
		_TilingWave("Tiling Wave", Vector) = (1,1,0,0)
		_WaveAmount ("Wave Amount", float) = 0.5
		_WaveDistort ("Wave Distort", float) = 0.05

		_CloudScale ("Clouds Scale", float) = 1.0
		_CloudBias ("Clouds Bias", float) = 0.0

		[NoScaleOffset] _ColorTex ("Color Tex", 2D) = "white" {}
		_TilingColor("Tiling Color", Vector) = (1,1,0,0)
		_ColPow ("Color Power", float) = 1
		_ColFactor ("Color Factor", float) = 1

		_Color ("Color", Color) = (1.0,1.0,1.0,1)
		_Color2 ("Color2", Color) = (1.0,1.0,1.0,1)

		_CloudDensity ("Cloud Density", float) = 5.0

		_BumpOffset ("BumpOffset", float) = 0.1
		_Steps ("Steps", float) = 10

		_CloudHeight ("Cloud Height", float) = 100
		_Scale ("Scale", float) = 10

		_Speed ("Speed", float) = 1

		_LightSpread ("Light Spread PFPF", Vector) = (2.0,1.0,50.0,3.0)
	}

	SubShader
	{
		Tags
		{
			"Queue"="Background"
			"RenderType"="Opaque"
			"PreviewType"="Skybox"
		}

		LOD 100
		Cull Off
		ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
			#define SKYBOX
			#include "FogInclude.cginc"

			#define MAX_SKY_STEPS 4

			sampler2D _CloudTex1;
			sampler2D _FlowTex1;
			sampler2D _CloudTex2;
			sampler2D _WaveTex;

			float4 _Tiling1;
			float4 _Tiling2;
			float4 _TilingWave;

			float _CloudScale;
			float _CloudBias;

			float _Cloud2Amount;
			float _WaveAmount;
			float _WaveDistort;
			float _FlowSpeed;
			float _FlowAmount;

			sampler2D _ColorTex;
			float4 _TilingColor;

			float4 _Color;
			float4 _Color2;

			float _CloudDensity;

			float _BumpOffset;
			float _Steps;

			float _CloudHeight;
			float _Scale;
			float _Speed;

			float4 _LightSpread;

			float _ColPow;
			float _ColFactor;

			struct appdata
			{
				float4 vertex : POSITION;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 worldPos : TEXCOORD0;

				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(appdata v)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

				return o;
			}

			float3 GetStereoCameraWorldPosition()
			{
				#if defined(USING_STEREO_MATRICES)
					return unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex];
				#else
					return _WorldSpaceCameraPos;
				#endif
			}

			half4 SampleClouds(float3 uv, half3 sunTrans, half densityAdd)
			{
				half3 wave = half3(0.5, 0.5, 0.5);

				// Wave distortion is skipped when both values are almost zero.
				// This avoids one texture sample when wave is visually disabled.
				if (_WaveAmount > 0.001 || _WaveDistort > 0.001)
				{
					float3 coordsWave = float3(
						uv.xy * _TilingWave.xy + (_TilingWave.zw * _Speed * _Time.y),
						0.0
					);

					wave = tex2Dlod(_WaveTex, float4(coordsWave.xy, 0, 0)).xyz;
				}

				// First cloud layer
				float2 coords1 =
					uv.xy * _Tiling1.xy
					+ (_Tiling1.zw * _Speed * _Time.y)
					+ (wave.xy - 0.5) * _WaveDistort;

				half4 clouds = tex2Dlod(_CloudTex1, float4(coords1.xy, 0, 0));

				// Second cloud layer is skipped when _Cloud2Amount is effectively zero.
				// This avoids FlowTex + CloudTex2 + CloudTex2b samples when layer 2 is disabled.
				if (_Cloud2Amount > 0.001)
				{
					half3 cloudsFlow = tex2Dlod(_FlowTex1, float4(coords1.xy, 0, 0)).xyz;

					float speed = _FlowSpeed * _Speed * 10;
					float timeFrac1 = frac(_Time.y * speed);
					float timeFrac2 = frac(_Time.y * speed + 0.5);
					float timeLerp  = abs(timeFrac1 * 2.0 - 1.0);

					timeFrac1 = (timeFrac1 - 0.5) * _FlowAmount;
					timeFrac2 = (timeFrac2 - 0.5) * _FlowAmount;

					float2 coords2 =
						coords1 * _Tiling2.xy
						+ (_Tiling2.zw * _Speed * _Time.y);

					half4 clouds2 = tex2Dlod(
						_CloudTex2,
						float4(coords2.xy + (cloudsFlow.xy - 0.5) * timeFrac1, 0, 0)
					);

					half4 clouds2b = tex2Dlod(
						_CloudTex2,
						float4(coords2.xy + (cloudsFlow.xy - 0.5) * timeFrac2 + 0.5, 0, 0)
					);

					clouds2 = lerp(clouds2, clouds2b, timeLerp);
					clouds += (clouds2 - 0.5) * _Cloud2Amount * cloudsFlow.z;
				}

				// Add wave to cloud height
				clouds.w += (wave.z - 0.5) * _WaveAmount;

				// Scale and bias clouds
				clouds.w = clouds.w * _CloudScale + _CloudBias;

				// Overhead light color
				float3 coords4 = float3(
					uv.xy * _TilingColor.xy + (_TilingColor.zw * _Speed * _Time.y),
					0.0
				);

				half4 cloudColor = tex2Dlod(_ColorTex, float4(coords4.xy, 0, 0));

				// Cloud color based on density
				half cloudHightMask = 1.0 - saturate(clouds.w);
				cloudHightMask = pow(cloudHightMask, _ColPow);

				clouds.xyz *= lerp(
					_Color2.xyz,
					_Color.xyz * cloudColor.xyz * _ColFactor,
					cloudHightMask
				);

				// Subtract alpha based on height
				half cloudSub = 1.0 - uv.z;
				clouds.w = clouds.w - cloudSub * cloudSub;

				// Multiply density
				clouds.w = saturate(clouds.w * _CloudDensity);

				// Add extra density
				clouds.w = saturate(clouds.w + densityAdd);

				// Add sunlight
				clouds.xyz += sunTrans * cloudHightMask;

				// Premultiply alpha
				clouds.xyz *= clouds.w;

				return clouds;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				float3 cameraWorldPos = GetStereoCameraWorldPosition();

				// Generate a view direction from the world position of the skybox mesh.
				// In XR Single Pass Instanced, use the per-eye camera position.
				float3 viewDir = normalize(IN.worldPos - cameraWorldPos);

				// Get the falloff to the horizon
				float viewFalloff = 1.0 - saturate(dot(viewDir, float3(0, 1, 0)));

				// Add some up vector to the horizon to pull the clouds down
				float3 traceDir = normalize(viewDir + float3(0, viewFalloff * 0.1, 0));

				// Generate UVs from the world position of the sky
				float3 worldPos =
					cameraWorldPos
					+ traceDir * ((_CloudHeight - cameraWorldPos.y) / max(traceDir.y, 0.00001));

				float3 uv = float3(worldPos.xz * 0.01 * _Scale, 0);

				// Make a spot for the sun, make it brighter at the horizon
				float lightDot = saturate(dot(_WorldSpaceLightPos0, viewDir) * 0.5 + 0.5);

				half3 lightTrans =
					_LightColor0.xyz
					* (
						pow(lightDot, _LightSpread.x) * _LightSpread.y
						+ pow(lightDot, _LightSpread.z) * _LightSpread.w
					);

				half3 lightTransTotal = lightTrans * pow(viewFalloff, 5) * 5.0 + 1.0;

				// Clamp effective step count to avoid accidentally expensive material settings.
				float effectiveSteps = clamp(_Steps, 1.0, (float)MAX_SKY_STEPS);

				// Figure out how far to move through the UVs for each parallax step
				half3 uvStep =
					half3(traceDir.xz * _BumpOffset * (1.0 / max(traceDir.y, 0.00001)), 1.0)
					* (1.0 / effectiveSteps);

				// Replaces per-pixel sin random with a fixed offset.
				// This removes the rand3() sin/dot cost while keeping a small non-zero offset.
				uv += uvStep * 0.37;

				// Initialize accumulated color with fog
				half4 accColor = FogColorDensitySky(viewDir);
				half4 clouds = 0;

				[loop]
				for (int j = 0; j < MAX_SKY_STEPS; j++)
				{
					if (j >= effectiveSteps) { break; }

					// If alpha is filled, stop early
					if (accColor.w >= 1.0) { break; }

					uv += uvStep;

					clouds = SampleClouds(uv, lightTransTotal, 0.0);

					// Front-to-back blending
					accColor += clouds * (1.0 - accColor.w);
				}

				// One last sample to fill gaps
				uv += uvStep;
				clouds = SampleClouds(uv, lightTransTotal, 1.0);
				accColor += clouds * (1.0 - accColor.w);

				return accColor;
			}
			ENDCG
		}
	}
}