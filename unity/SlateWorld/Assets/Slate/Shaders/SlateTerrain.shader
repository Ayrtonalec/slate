// SLATE terrain v2 — per-pixel ground detail for URP.
// The mesh's vertex color carries the macro biome tint (rgb) and the snow
// mask (a). The fragment stage layers seamless procedural detail textures on
// top: grass grain on the flats, ridged rock on steep slopes, snow drifts
// where the mask says winter or altitude. This is what turns vertex-color
// blur into ground that reads as real at every zoom.
Shader "Slate/Terrain"
{
    Properties
    {
        _GrassTex ("Grass detail", 2D) = "gray" {}
        _RockTex ("Rock detail", 2D) = "gray" {}
        _SnowTex ("Snow detail", 2D) = "gray" {}
        _DetailScale ("Detail scale", Range(0.05, 2)) = 0.34
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_RockTex);  SAMPLER(sampler_RockTex);
            TEXTURE2D(_SnowTex);  SAMPLER(sampler_SnowTex);

            CBUFFER_START(UnityPerMaterial)
            float _DetailScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float2 uv = IN.positionWS.xz * _DetailScale;

                // Two scales of every material so the tiling never reads.
                half grass = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv).r * 0.62h
                           + SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv * 0.23h + 0.37h).r * 0.38h;
                half rock  = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uv * 0.71h).r * 0.7h
                           + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uv * 0.19h + 0.11h).r * 0.3h;
                half snowD = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, uv * 0.9h).r;

                // Steep ground turns to rock, per-pixel.
                half slope = saturate(1.0h - n.y);
                half rockW = smoothstep(0.24h, 0.55h, slope);
                half detail = lerp(grass, rock, rockW);

                // Macro tint from the mesh; desaturate + cool it where rock takes over.
                half3 tint = IN.color.rgb;
                half lum = dot(tint, half3(0.30h, 0.59h, 0.11h));
                half3 rockTint = lerp(tint, half3(lum, lum, lum) * half3(1.04h, 1.0h, 0.96h), 0.6h);
                tint = lerp(tint, rockTint, rockW);

                half3 albedo = tint * detail;

                // Snow overlay from the vertex mask (seasonal paint + mountain caps).
                half snowA = IN.color.a;
                half3 snowCol = half3(0.87h, 0.90h, 0.95h) * snowD;
                albedo = lerp(albedo, snowCol, snowA);

                // Lighting: half-lambert sun + shadows + sky ambient + point lights (fires).
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, mainLight.direction));
                half diffuse = ndl * 0.85h + 0.15h;
                half3 lit = albedo * (SampleSH(n) * 0.9h + mainLight.color * diffuse * mainLight.shadowAttenuation);

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, IN.positionWS);
                    lit += albedo * l.color * (saturate(dot(n, l.direction)) * l.distanceAttenuation);
                }
                #endif

                lit = MixFog(lit, IN.fogFactor);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
