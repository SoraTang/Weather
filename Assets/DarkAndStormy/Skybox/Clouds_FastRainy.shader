Shader "Skybox/Clouds_FastRainy"
{
    Properties
    {
        [NoScaleOffset] _CloudTex1 ("Clouds 1", 2D) = "white" {}
        [NoScaleOffset] _CloudTex2 ("Clouds 2", 2D) = "grey" {}

        _Tiling1 ("Tiling 1", Vector) = (1,1,0.02,0)
        _Tiling2 ("Tiling 2", Vector) = (2,2,-0.01,0.01)

        _CloudDensity ("Cloud Density", Range(0, 5)) = 1.5
        _CloudContrast ("Cloud Contrast", Range(0.1, 5)) = 1.5
        _CloudSoftness ("Cloud Softness", Range(0.01, 1)) = 0.25

        _SkyColorTop ("Sky Top Color", Color) = (0.25,0.35,0.38,1)
        _SkyColorHorizon ("Sky Horizon Color", Color) = (0.55,0.65,0.66,1)
        _CloudColor ("Cloud Color", Color) = (0.55,0.62,0.62,1)

        _HorizonPower ("Horizon Power", Range(0.1, 8)) = 2
        _Speed ("Speed", Range(0, 2)) = 0.1
        _Darken ("Rainy Darken", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _CloudTex1;
            sampler2D _CloudTex2;

            float4 _Tiling1;
            float4 _Tiling2;

            float _CloudDensity;
            float _CloudContrast;
            float _CloudSoftness;

            float4 _SkyColorTop;
            float4 _SkyColorHorizon;
            float4 _CloudColor;

            float _HorizonPower;
            float _Speed;
            float _Darken;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                // 0 = horizon, 1 = top
                float vertical = saturate(dir.y * 0.5 + 0.5);
                float horizon = pow(1.0 - vertical, _HorizonPower);

                float2 skyUV = dir.xz / max(abs(dir.y) + 0.2, 0.2);

                float2 uv1 = skyUV * _Tiling1.xy + _Tiling1.zw * _Time.y * _Speed;
                float2 uv2 = skyUV * _Tiling2.xy + _Tiling2.zw * _Time.y * _Speed;

                float c1 = tex2D(_CloudTex1, uv1).r;
                float c2 = tex2D(_CloudTex2, uv2).r;

                float cloud = lerp(c1, c2, 0.45);
                cloud = saturate((cloud - _CloudSoftness) * _CloudContrast);
                cloud = saturate(cloud * _CloudDensity);

                float3 skyCol = lerp(_SkyColorHorizon.rgb, _SkyColorTop.rgb, vertical);
                float3 cloudCol = _CloudColor.rgb;

                float3 finalCol = lerp(skyCol, cloudCol, cloud * 0.75);

                // rainy horizon haze
                finalCol = lerp(finalCol, _SkyColorHorizon.rgb, horizon * 0.35);

                // rainy darken
                finalCol *= (1.0 - _Darken);

                return float4(finalCol, 1);
            }
            ENDCG
        }
    }
}