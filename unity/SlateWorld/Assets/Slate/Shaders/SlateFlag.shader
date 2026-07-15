// SLATE flag — banner cloth that actually waves. The mesh is a subdivided
// quad with uv.x = 0 at the pole; the wave grows toward the free edge and
// fake self-shading follows the wave slope so the cloth reads as cloth.
Shader "Slate/Flag"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.6, 0.3, 0.3, 1)
        _WaveAmp ("Wave amplitude", Range(0, 0.5)) = 0.16
        _WaveFreq ("Wave frequency", Range(0, 20)) = 7.0
        _WaveSpeed ("Wave speed", Range(0, 20)) = 5.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _WaveAmp, _WaveFreq, _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float shade : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float phase = IN.uv.x * _WaveFreq - _Time.y * _WaveSpeed;
                float pin = IN.uv.x; // pinned at the pole, free at the fly end
                float3 pos = IN.positionOS.xyz;
                pos.z += sin(phase) * _WaveAmp * pin;
                pos.y += cos(phase * 0.7) * _WaveAmp * 0.25 * pin;

                OUT.positionCS = TransformObjectToHClip(pos);
                // Fake cloth shading from the wave slope.
                OUT.shade = 0.78 + 0.22 * cos(phase);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                half3 col = _BaseColor.rgb * IN.shade * (0.35h + 0.75h * mainLight.color);
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
