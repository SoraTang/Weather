
Shader "Rain/RainScroller"
{
    Properties
    {
        _DropletMask("Droplet Mask", 2D) = "white" {}
        _Distortion("Distortion", Float) = 0.01
        _Tiling("Tiling", Vector) = (1,1,0,0)
        _Tint("Tint", Color) = (0.8809185,0.9188843,0.9245283,1)
        _Droplets_Strength("Droplets_Strength", Range( 0 , 1)) = 1
        _DropletThreshold("Droplet Threshold", Range(0,1)) = 0.65
        _DropletSoftness("Droplet Threshold Softness", Range(0,0.5)) = 0.04
        _RivuletMask("Rivulet Mask", 2D) = "white" {}
        _GlobalRotation("Global Rotation", Range( -180 , 180)) = 0
        _RivuletRotation("Rivulet Rotation", Range( -180 , 180)) = 0
        _RivuletSpeed("Rivulet Speed", Range( 0 , 2)) = 0.2
        _RivuletsStrength("Rivulets Strength", Range( 0 , 3)) = 1
        _DropletsGravity("Droplets Gravity", Range( 0 , 1)) = 0
        _DropletsStrikeSpeed("Droplets Strike Speed", Range( 0 , 2)) = 0.3
        [HideInInspector] _texcoord( "", 2D ) = "white" {}
        [HideInInspector] __dirty( "", Int ) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "RainScrollerURP"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            TEXTURE2D(_DropletMask);
            SAMPLER(sampler_DropletMask);
            TEXTURE2D(_RivuletMask);
            SAMPLER(sampler_RivuletMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _DropletMask_ST;
                float4 _RivuletMask_ST;
                float _Distortion;
                float2 _Tiling;
                float4 _Tint;
                float _Droplets_Strength;
                float _DropletThreshold;
                float _DropletSoftness;
                float _GlobalRotation;
                float _RivuletRotation;
                float _RivuletSpeed;
                float _RivuletsStrength;
                float _DropletsGravity;
                float _DropletsStrikeSpeed;
            CBUFFER_END

            float2 RotateUV(float2 uv, float degrees)
            {
                float r = radians(degrees);
                float c = cos(r);
                float s = sin(r);
                return mul(uv, float2x2(c, -s, s, c));
            }

            float4 SampleGradient_Original(float timeValue)
            {
                // Original gradient in the ASE shader:
                // color: white at 0.8500038 -> black at 1
                // alpha: 1 throughout. Only the red channel is used.
                float t = saturate((timeValue - 0.8500038) / max(0.00001, 1.0 - 0.8500038));
                return float4(lerp(float3(1,1,1), float3(0,0,0), t), 1.0);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                // ----- Droplets -----
                float2 gravityOffset = float2(0.0, _DropletsGravity);
                float2 baseUV = IN.uv * _Tiling;
                float2 dropletUV = baseUV + time * gravityOffset;
                dropletUV = RotateUV(dropletUV, _GlobalRotation);

                float4 dropletSample = SAMPLE_TEXTURE2D(_DropletMask, sampler_DropletMask, dropletUV);

                // Threshold the droplet texture so gray/noisy background does not become active rain.
                // Black/no-drops should become 0; bright droplet areas remain active.
                float dropletLuma = dot(dropletSample.rgb, float3(0.299, 0.587, 0.114));
                float dropletMask = smoothstep(
                    _DropletThreshold,
                    min(_DropletThreshold + max(_DropletSoftness, 0.0001), 1.0),
                    dropletLuma
                );
                dropletSample *= dropletMask;
                float4 dropletVector = dropletSample * 2.0 - 1.0;
                dropletVector = float4(dropletVector.r, dropletVector.g, 0.0, 0.0);

                float strikeTime = time * _DropletsStrikeSpeed;
                float remappedBlue = -1.0 + dropletSample.b * 2.0;
                float activationRaw = dropletSample.a - frac(remappedBlue + strikeTime);
                float threshold = 1.0 - _Droplets_Strength;
                float activation = saturate(ceil((activationRaw - threshold) / max(0.00001, 1.0 - threshold)));
                float4 activeDroplets = dropletVector * activation;

                // ----- Rivulets -----
                float2 rivuletUV = RotateUV(dropletUV, _RivuletRotation);
                float4 rivuletBase = SAMPLE_TEXTURE2D(_RivuletMask, sampler_RivuletMask, rivuletUV);
                float2 rivuletBA = float2(rivuletBase.b, rivuletBase.a);
                float2 rivuletOffset = float2(-0.1, 0.0) + rivuletBA * (float2(0.1, 3.0) - float2(-0.1, 0.0));

                float timing = time * 0.23;
                float rest1 = ((timing - floor(timing + 0.5)) * 2.0 + 1.0) * 0.5;
                float timing2 = timing + 0.5;
                float rest2 = ((timing2 - floor(timing2 + 0.5)) * 2.0 + 1.0) * 0.5;

                float tri = abs((timing - floor(timing + 0.5)) * 2.0) * 2.0 - 1.0;
                float bias = pow(saturate((tri + 1.0) * 0.5), 2.0);

                float2 scroll = float2(0.0, time * _RivuletSpeed);
                float4 rivulet1 = SAMPLE_TEXTURE2D(_RivuletMask, sampler_RivuletMask, rivuletUV + scroll + rivuletOffset * rest1);
                float4 rivulet2 = SAMPLE_TEXTURE2D(_RivuletMask, sampler_RivuletMask, rivuletUV + scroll + rivuletOffset * rest2);
                float4 rivulets = lerp(rivulet1, rivulet2, bias);
                float4 rivuletVector = (rivulets * 2.0 - 1.0) * float4(1, 1, 0, 0);

                float normalDotUp = dot(normalize(IN.normalWS), float3(0, 1, 0));
                float gradientMask = SampleGradient_Original(abs(normalDotUp)).r;

                float4 distortionVector = activeDroplets + rivuletVector * _RivuletsStrength * gradientMask;

                // URP replacement for Built-in GrabPass:
                // sample the camera opaque texture, distorted by the rain masks.
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                screenUV += distortionVector.xy * _Distortion;

                half3 sceneColor = SampleSceneColor(screenUV);
                half3 finalColor = sceneColor * _Tint.rgb;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
