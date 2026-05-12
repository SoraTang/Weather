Shader "Custom/WetRoadPuddles_URP_Lit"
{
    Properties
    {
        [Header(Base Lit Material)]
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Base Smoothness", Range(0,1)) = 0.35

        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 1

        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1

        [Header(Overall Wetness)]
        _Wetness("Overall Wetness", Range(0,1)) = 0.45
        _WetDarken("Wet Darken", Range(0,1)) = 0.22
        _WetSmoothnessBoost("Wet Smoothness Boost", Range(0,1)) = 0.35

        [Header(Puddles)]
        _PuddleMap("Puddle Mask", 2D) = "white" {}
        _PuddleStrength("Puddle Strength", Range(0,1)) = 0.75
        _PuddleThreshold("Puddle Threshold", Range(0,1)) = 0.45
        _PuddleContrast("Puddle Contrast", Range(0.1,8)) = 2.0
        _PuddleTint("Puddle Tint", Color) = (0.65,0.75,0.85,1)
        _PuddleDarken("Puddle Darken", Range(0,1)) = 0.35
        _PuddleSmoothness("Puddle Smoothness", Range(0,1)) = 0.96
        _PuddleNormalFlatten("Puddle Normal Flatten", Range(0,1)) = 0.85

        [Header(Animated Rain Ripples)]
        [Normal] _RippleNormal("Ripple Normal", 2D) = "bump" {}
        _RippleStrength("Ripple Strength", Range(0,1)) = 0.12
        _RippleTiling("Ripple Tiling", Vector) = (4,4,0,0)
        _RippleSpeed("Ripple Speed XY", Vector) = (0.05,0.12,0,0)

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float fogCoord    : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_PuddleMap);      SAMPLER(sampler_PuddleMap);
            TEXTURE2D(_RippleNormal);   SAMPLER(sampler_RippleNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _OcclusionMap_ST;
                float4 _PuddleMap_ST;
                float4 _RippleNormal_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _BumpScale;
                half _OcclusionStrength;
                half _Wetness;
                half _WetDarken;
                half _WetSmoothnessBoost;
                half _PuddleStrength;
                half _PuddleThreshold;
                half _PuddleContrast;
                half4 _PuddleTint;
                half _PuddleDarken;
                half _PuddleSmoothness;
                half _PuddleNormalFlatten;
                half _RippleStrength;
                float4 _RippleTiling;
                float4 _RippleSpeed;
                half _Cull;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInput = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInput.positionCS;
                OUT.positionWS = posInput.positionWS;
                OUT.uv = IN.uv;
                OUT.normalWS = normInput.normalWS;
                OUT.tangentWS = float4(normInput.tangentWS, IN.tangentOS.w);
                OUT.fogCoord = ComputeFogFactor(posInput.positionCS.z);
                OUTPUT_LIGHTMAP_UV(IN.staticLightmapUV, unity_LightmapST, OUT.staticLightmapUV);
                OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);
                return OUT;
            }

            half3 BlendNormals(half3 n1, half3 n2)
            {
                n1 = normalize(n1);
                n2 = normalize(n2);
                return normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 baseUV = TRANSFORM_TEX(IN.uv, _BaseMap);
                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _BaseColor;

                float2 puddleUV = TRANSFORM_TEX(IN.uv, _PuddleMap);
                half rawPuddle = SAMPLE_TEXTURE2D(_PuddleMap, sampler_PuddleMap, puddleUV).r;
                half puddleMask = saturate((rawPuddle - _PuddleThreshold) * _PuddleContrast + 0.5h);
                puddleMask = saturate(puddleMask * _PuddleStrength);

                half wetMask = saturate(_Wetness);
                half totalWet = saturate(max(wetMask * 0.55h, puddleMask));

                half3 baseColor = albedoSample.rgb;
                half3 wetColor = baseColor * (1.0h - _WetDarken * wetMask);
                half3 puddleColor = baseColor * (1.0h - _PuddleDarken) * _PuddleTint.rgb;
                half3 finalColor = lerp(baseColor, wetColor, wetMask);
                finalColor = lerp(finalColor, puddleColor, puddleMask);

                half3 baseNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TRANSFORM_TEX(IN.uv, _BumpMap)), _BumpScale);
                half3 flatNormalTS = half3(0, 0, 1);
                half3 normalTS = lerp(baseNormalTS, flatNormalTS, puddleMask * _PuddleNormalFlatten);

                float2 rippleUV1 = IN.uv * _RippleTiling.xy + _Time.y * _RippleSpeed.xy;
                float2 rippleUV2 = IN.uv * (_RippleTiling.xy * 1.37) - _Time.y * (_RippleSpeed.xy * 0.73);
                half3 ripple1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, rippleUV1), _RippleStrength * puddleMask);
                half3 ripple2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_RippleNormal, sampler_RippleNormal, rippleUV2), _RippleStrength * 0.5h * puddleMask);
                normalTS = BlendNormals(normalTS, ripple1);
                normalTS = BlendNormals(normalTS, ripple2);

                half occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, TRANSFORM_TEX(IN.uv, _OcclusionMap)).g;
                half occlusion = lerp(1.0h, occlusionSample, _OcclusionStrength);

                half finalSmoothness = saturate(_Smoothness + _WetSmoothnessBoost * wetMask);
                finalSmoothness = lerp(finalSmoothness, _PuddleSmoothness, puddleMask);

                float sgn = IN.tangentWS.w * GetOddNegativeScale();
                float3 bitangent = sgn * cross(IN.normalWS.xyz, IN.tangentWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS.xyz));
                normalWS = NormalizeNormalPerPixel(normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord = IN.fogCoord;
                inputData.vertexLighting = VertexLighting(IN.positionWS, normalWS);
                inputData.bakedGI = SAMPLE_GI(IN.staticLightmapUV, IN.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(IN.staticLightmapUV);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.alpha = albedoSample.a;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0,0,0);
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = half3(0,0,0);
                surfaceData.occlusion = occlusion;
                surfaceData.clearCoatMask = puddleMask * 0.25h;
                surfaceData.clearCoatSmoothness = _PuddleSmoothness;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings vert(Attributes IN) { Varyings OUT; UNITY_SETUP_INSTANCE_ID(IN); UNITY_TRANSFER_INSTANCE_ID(IN, OUT); OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half4 frag(Varyings IN) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
