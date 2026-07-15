// SLATE water — depth-tinted sea for URP: celadon shallows fading to deep
// water, a soft foam line along every coast, moving procedural ripples and a
// sun glint. Reads the camera depth texture (enabled on the pipeline asset).
Shader "Slate/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.38, 0.56, 0.53, 1)
        _DeepColor ("Deep", Color) = (0.10, 0.22, 0.26, 1)
        _FoamColor ("Foam", Color) = (0.92, 0.95, 0.92, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor, _DeepColor, _FoamColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            float hashn(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hashn(i), b = hashn(i + float2(1, 0));
                float c = hashn(i + float2(0, 1)), d = hashn(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                // Gentle swell.
                ws.y += sin(ws.x * 0.045 + _Time.y * 0.9) * 0.10
                      + cos(ws.z * 0.038 + _Time.y * 1.15) * 0.10;
                OUT.positionWS = ws;
                OUT.positionCS = TransformWorldToHClip(ws);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenPos.xy / IN.screenPos.w;
                float sceneRaw = SampleSceneDepth(uv);
                float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                float thisEye = IN.screenPos.w;
                float depthBelow = max(0.0, sceneEye - thisEye);

                // Color by how much water sits below the surface.
                half deepMix = saturate(depthBelow / 26.0);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, deepMix);

                // Moving ripple mottling.
                float2 p = IN.positionWS.xz;
                float ripple = vnoise(p * 0.06 + _Time.y * 0.22) * 0.6
                             + vnoise(p * 0.15 - _Time.y * 0.13) * 0.4;
                col *= 0.92 + ripple * 0.16;

                // Foam along the shore, broken up by drifting noise.
                float foamBand = 1.0 - saturate(depthBelow / 2.6);
                float foamNoise = vnoise(p * 0.7 + _Time.y * 0.35);
                half foam = saturate(foamBand * (0.55 + 0.45 * foamNoise)) * step(0.02, depthBelow);
                col = lerp(col, _FoamColor.rgb, foam * 0.8);

                // Sun glint from perturbed normal.
                Light mainLight = GetMainLight();
                float3 n = normalize(float3(
                    vnoise(p * 0.11 + _Time.y * 0.3) - 0.5,
                    2.6,
                    vnoise(p * 0.11 - _Time.y * 0.27) - 0.5));
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 h = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), 90.0) * 0.7;
                col += mainLight.color * spec;

                half alpha = lerp(0.62h, 0.90h, deepMix);
                alpha = max(alpha, foam * 0.9);
                col = MixFog(col, IN.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
