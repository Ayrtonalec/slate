// SLATE zone marker — soft unlit disc or ring, for divine weather, blessings,
// dragon menace rings and event pings. uv.x encodes distance from center (0..1).
Shader "Slate/Zone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.4)
        _Ring ("Ring (0 = disc, 1 = ring)", Range(0, 1)) = 0
        _RingWidth ("Ring width", Range(0.01, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Ring, _RingWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half r = IN.uv.x;
                half discA = 1.0h - smoothstep(0.55h, 1.0h, r);
                half ringA = smoothstep(1.0h - _RingWidth * 2.0h, 1.0h - _RingWidth, r)
                           * (1.0h - smoothstep(1.0h - _RingWidth * 0.5h, 1.0h, r));
                half a = lerp(discA, ringA, _Ring) * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
