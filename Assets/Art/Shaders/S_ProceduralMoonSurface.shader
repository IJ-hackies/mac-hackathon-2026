Shader "SpaceGame/Procedural Moon Surface"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.772549, 0.458824, 0.243137, 1)
        _MacroScale ("Large Feature Scale", Range(0.5, 12)) = 3.2
        _MacroStrength ("Large Feature Strength", Range(0, 0.4)) = 0.16
        _DetailScale ("Dust Detail Scale", Range(4, 96)) = 38
        _DetailStrength ("Dust Detail Strength", Range(0, 0.2)) = 0.055
        _NormalStrength ("Surface Bump", Range(0, 2)) = 0.85
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
        _RoughnessVariation ("Roughness Variation", Range(0, 0.15)) = 0.035
        [Toggle(_CAST_SHADOW_ONLY)] _CastShadowOnly ("Flat Surface With Cast Shadows", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _MacroScale;
            half _MacroStrength;
            float _DetailScale;
            half _DetailStrength;
            half _NormalStrength;
            half _Smoothness;
            half _RoughnessVariation;
            half _CastShadowOnly;
        CBUFFER_END

        float Hash31(float3 value)
        {
            value = frac(value * 0.1031);
            value += dot(value, value.yzx + 33.33);
            return frac((value.x + value.y) * value.z);
        }

        float4 ValueNoiseWithGradient(float3 position)
        {
            float3 cell = floor(position);
            float3 localPosition = frac(position);
            float3 blend = localPosition * localPosition * (3.0 - 2.0 * localPosition);
            float3 blendDerivative = 6.0 * localPosition * (1.0 - localPosition);

            float n000 = Hash31(cell + float3(0, 0, 0));
            float n100 = Hash31(cell + float3(1, 0, 0));
            float n010 = Hash31(cell + float3(0, 1, 0));
            float n110 = Hash31(cell + float3(1, 1, 0));
            float n001 = Hash31(cell + float3(0, 0, 1));
            float n101 = Hash31(cell + float3(1, 0, 1));
            float n011 = Hash31(cell + float3(0, 1, 1));
            float n111 = Hash31(cell + float3(1, 1, 1));

            float lower = lerp(lerp(n000, n100, blend.x), lerp(n010, n110, blend.x), blend.y);
            float upper = lerp(lerp(n001, n101, blend.x), lerp(n011, n111, blend.x), blend.y);

            float3 gradient;
            gradient.x = blendDerivative.x * lerp(
                lerp(n100 - n000, n110 - n010, blend.y),
                lerp(n101 - n001, n111 - n011, blend.y),
                blend.z);
            gradient.y = blendDerivative.y * lerp(
                lerp(n010 - n000, n110 - n100, blend.x),
                lerp(n011 - n001, n111 - n101, blend.x),
                blend.z);
            gradient.z = blendDerivative.z * (upper - lower);

            return float4(lerp(lower, upper, blend.z), gradient);
        }

        float4 MoonMacroPattern(float3 surfaceDirection)
        {
            float frequency = _MacroScale;
            float3 position = surfaceDirection * frequency + float3(11.7, -4.3, 8.1);
            float4 octave = ValueNoiseWithGradient(position);
            float4 pattern = float4(octave.x * 0.56, octave.yzw * (0.56 * frequency));

            position = position * 2.07 + float3(3.4, 13.1, -7.6);
            frequency *= 2.07;
            octave = ValueNoiseWithGradient(position);
            pattern += float4(octave.x * 0.28, octave.yzw * (0.28 * frequency));

            position = position * 2.11 + float3(-9.2, 5.7, 2.8);
            frequency *= 2.11;
            octave = ValueNoiseWithGradient(position);
            pattern += float4(octave.x * 0.16, octave.yzw * (0.16 * frequency));
            return pattern;
        }

        float4 MoonDetailPattern(float3 surfaceDirection)
        {
            float frequency = _DetailScale;
            float3 position = surfaceDirection * frequency + float3(-17.2, 6.8, 23.5);
            float4 octave = ValueNoiseWithGradient(position);
            float4 detail = float4(octave.x * 0.68, octave.yzw * (0.68 * frequency));

            position = position * 2.03 + float3(7.1, -12.6, 4.9);
            frequency *= 2.03;
            octave = ValueNoiseWithGradient(position);
            detail += float4(octave.x * 0.32, octave.yzw * (0.32 * frequency));
            return detail;
        }

        half3 ApplyMoonBump(
            half3 geometricNormalWS,
            float3 surfaceDirection,
            float3 patternGradientOS)
        {
            float3 surfaceGradientOS = patternGradientOS
                - dot(patternGradientOS, surfaceDirection) * surfaceDirection;
            float gradientLength = length(surfaceGradientOS);
            float3 gradientDirectionOS = surfaceGradientOS / max(gradientLength, 0.0001);
            float3 gradientDirectionWS = TransformObjectToWorldNormal(gradientDirectionOS);
            float bumpAmount = min(gradientLength, 1.25) * _NormalStrength;
            return SafeNormalize(geometricNormalWS - gradientDirectionWS * bumpAmount);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex MoonVertex
            #pragma fragment MoonFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _CAST_SHADOW_ONLY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half4 fogFactorAndVertexLight : TEXCOORD3;
            #else
                half fogFactor : TEXCOORD3;
            #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings MoonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.positionCS = positionInputs.positionCS;

                half fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight = VertexLighting(positionInputs.positionWS, output.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
            #else
                output.fogFactor = fogFactor;
            #endif

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 MoonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 geometricNormalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 surfaceDirection = normalize(input.positionOS);
                float4 macroPattern = MoonMacroPattern(surfaceDirection);
                float4 detailPattern = MoonDetailPattern(surfaceDirection);

                half tonalOffset = (macroPattern.x - 0.5) * (2.0h * _MacroStrength);
                tonalOffset += (detailPattern.x - 0.5) * (2.0h * _DetailStrength);
                half tonalMultiplier = clamp(1.0h + tonalOffset, 0.58h, 1.35h);
                half3 albedo = _BaseColor.rgb * tonalMultiplier;

            #if defined(_CAST_SHADOW_ONLY)
                half fogCoord;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                fogCoord = input.fogFactorAndVertexLight.x;
            #else
                fogCoord = input.fogFactor;
            #endif

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half4 shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                half castShadow = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half4 color = half4(albedo * castShadow, _BaseColor.a);
                color.rgb = MixFog(color.rgb, fogCoord);
                return color;
            #else

                float3 bumpGradientOS = macroPattern.yzw / max(_MacroScale, 0.001) * 0.3;
                bumpGradientOS += detailPattern.yzw / max(_DetailScale, 0.001) * 0.7;
                half3 normalWS = ApplyMoonBump(
                    geometricNormalWS,
                    surfaceDirection,
                    bumpGradientOS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0.0h;
                surfaceData.specular = half3(0.04h, 0.04h, 0.04h);
                surfaceData.smoothness = saturate(
                    _Smoothness + (macroPattern.x - 0.5) * _RoughnessVariation);
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = 0.0h;
                surfaceData.alpha = _BaseColor.a;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.fogCoord = input.fogFactorAndVertexLight.x;
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
            #else
                inputData.fogCoord = input.fogFactor;
            #endif
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            #endif
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0.0h;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormal = PackNormalOctQuadEncode(normalize(input.normalWS));
                half3 packedNormal = PackFloat2To888(saturate(octNormal * 0.5 + 0.5));
                return half4(packedNormal, 0.0h);
            #else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
            #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
