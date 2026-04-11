Shader "Custom/RainyWindow_URP"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.75, 0.85, 0.95, 0.18)

        _RainTex("Rain Mask (grayscale)", 2D) = "white" {}
        _RainColor("Rain Color", Color) = (0.9, 0.95, 1.0, 1.0)

        _Tiling1("Drop Tiling", Vector) = (2, 2, 0, 0)
        _Tiling2("Streak Tiling", Vector) = (3, 5, 0, 0)

        _Speed1("Drop Speed", Float) = 0.08
        _Speed2("Streak Speed", Float) = 0.18

        _Strength1("Drop Strength", Range(0, 2)) = 0.7
        _Strength2("Streak Strength", Range(0, 2)) = 0.45

        _Alpha("Glass Alpha", Range(0, 1)) = 0.2
        _RainAlpha("Rain Alpha Boost", Range(0, 1)) = 0.35

        _EdgeFade("Edge Fade", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_RainTex);
            SAMPLER(sampler_RainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _RainTex_ST;
                float4 _BaseColor;
                float4 _RainColor;
                float4 _Tiling1;
                float4 _Tiling2;
                float _Speed1;
                float _Speed2;
                float _Strength1;
                float _Strength2;
                float _Alpha;
                float _RainAlpha;
                float _EdgeFade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float t = _Time.y;

                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                float2 uv1 = uv * _Tiling1.xy + float2(0, -t * _Speed1);
                float2 uv2 = uv * _Tiling2.xy + float2(0, -t * _Speed2);

                // 第一层：较大的水滴
                float rain1 = SAMPLE_TEXTURE2D(_RainTex, sampler_RainTex, uv1).r;

                // 第二层：更细更快的雨痕
                float rain2 = SAMPLE_TEXTURE2D(_RainTex, sampler_RainTex, uv2 + float2(0.37, 0.11)).r;

                // 稍微让第二层更像竖向 streak
                rain2 = smoothstep(0.35, 0.9, rain2);

                float rainMask = saturate(rain1 * _Strength1 + rain2 * _Strength2);

                // 让边缘透明一点，中心更明显，避免整面板太糊
                float2 centered = uv * 2.0 - 1.0;
                float edge = 1.0 - saturate(length(centered) * 0.7);
                edge = pow(edge, _EdgeFade);

                float finalRain = rainMask * edge;

                half3 finalRgb = lerp(baseCol.rgb, _RainColor.rgb, finalRain);
                half finalA = saturate(_Alpha + finalRain * _RainAlpha);

                return half4(finalRgb, finalA);
            }
            ENDHLSL
        }
    }
}
