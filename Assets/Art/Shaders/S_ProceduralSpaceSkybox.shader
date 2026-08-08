Shader "SpaceGame/Procedural Space Skybox"
{
    Properties
    {
        _SpaceColorA ("Deep Space", Color) = (0.001, 0.002, 0.008, 1)
        _SpaceColorB ("Space Horizon", Color) = (0.006, 0.012, 0.035, 1)

        _NebulaTex ("Cosmic Fog", 2D) = "black" {}
        [HDR] _NebulaTint ("Cosmic Fog Tint", Color) = (0.8, 0.95, 1.15, 1)
        _NebulaIntensity ("Cosmic Fog Intensity", Range(0, 2)) = 0.85
        _NebulaDriftSpeed ("Cosmic Fog Drift", Range(-0.01, 0.01)) = 0.0006
        _NebulaStarOcclusion ("Cosmic Fog Star Occlusion", Range(0, 1)) = 0.55

        [HDR] _StarColor ("Star Color", Color) = (1.2, 1.35, 1.6, 1)
        _StarDensity ("Star Density", Range(0.97, 0.999)) = 0.988
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.4
        _StarWarmColor ("Warm Star Tint", Color) = (1.2, 0.84, 0.62, 1)
        _StarCoolColor ("Cool Star Tint", Color) = (0.62, 0.86, 1.25, 1)
        _StarTwinkleStrength ("Star Twinkle Strength", Range(0, 0.35)) = 0.12
        _StarTwinkleSpeed ("Star Twinkle Speed", Range(0, 1)) = 0.14

        _SunDirection ("Sun Direction", Vector) = (0.42, 0.36, 0.83, 0)
        [HDR] _SunColor ("Sun Color", Color) = (8, 4.8, 2.2, 1)
        [HDR] _SunCoreColor ("Sun Core Color", Color) = (12, 9, 5.5, 1)
        [HDR] _SunCoronaColor ("Sun Corona Color", Color) = (3.2, 1.7, 0.7, 1)
        _SunAngularRadius ("Sun Angular Radius", Range(0.005, 0.08)) = 0.025
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.65
        _SunCoronaStrength ("Sun Corona Strength", Range(0, 2)) = 0.8
        _SunRayStrength ("Sun Ray Strength", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            float4 _SpaceColorA;
            float4 _SpaceColorB;

            sampler2D _NebulaTex;
            float4 _NebulaTint;
            float _NebulaIntensity;
            float _NebulaDriftSpeed;
            float _NebulaStarOcclusion;

            float4 _StarColor;
            float _StarDensity;
            float _StarBrightness;
            float4 _StarWarmColor;
            float4 _StarCoolColor;
            float _StarTwinkleStrength;
            float _StarTwinkleSpeed;

            float4 _SunDirection;
            float4 _SunColor;
            float4 _SunCoreColor;
            float4 _SunCoronaColor;
            float _SunAngularRadius;
            float _SunGlow;
            float _SunCoronaStrength;
            float _SunRayStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirection : TEXCOORD0;
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float3 RotateAroundY(float3 direction, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    cosine * direction.x - sine * direction.z,
                    direction.y,
                    sine * direction.x + cosine * direction.z);
            }

            float3 SampleCosmicFog(float3 direction)
            {
                float3 driftedDirection = RotateAroundY(direction, _Time.y * _NebulaDriftSpeed);

                // Triplanar direction sampling avoids an equirectangular seam and keeps the
                // texture look-up count fixed on WebGL2.
                float3 weights = abs(driftedDirection);
                weights *= weights;
                weights *= weights;
                weights /= max(weights.x + weights.y + weights.z, 0.0001);

                float2 uvX = driftedDirection.zy * 0.38 + float2(0.48, 0.50);
                float2 uvY = driftedDirection.xz * 0.38 + float2(0.53, 0.47);
                float2 uvZ = driftedDirection.xy * 0.38 + float2(0.46, 0.54);

                float3 fogX = tex2D(_NebulaTex, uvX).rgb;
                float3 fogY = tex2D(_NebulaTex, uvY).rgb;
                float3 fogZ = tex2D(_NebulaTex, uvZ).rgb;
                return fogX * weights.x + fogY * weights.y + fogZ * weights.z;
            }

            float3 StarLayer(float3 direction, float scale, float seed, float layerBrightness)
            {
                float3 position = direction * scale;
                float3 cell = floor(position);
                float3 localPosition = frac(position) - 0.5;

                float existence = Hash31(cell + seed);
                float3 offset = float3(
                    Hash31(cell + seed + 11.0),
                    Hash31(cell + seed + 23.0),
                    Hash31(cell + seed + 37.0)) - 0.5;
                offset *= 0.65;

                float distanceToStar = length(localPosition - offset);
                float sizeVariation = Hash31(cell + seed + 51.0);
                float starRadius = lerp(0.018, 0.06, sizeVariation * sizeVariation);
                float edge = max(fwidth(distanceToStar) * 1.5, 0.002);
                float star = 1.0 - smoothstep(starRadius, starRadius + edge, distanceToStar);
                star *= step(_StarDensity, existence) * lerp(0.45, 1.35, sizeVariation);

                float temperature = Hash31(cell + seed + 89.0);
                float3 tint = lerp(_StarWarmColor.rgb, _StarCoolColor.rgb, temperature);

                // A triangle wave gives each star a gentle independent pulse without adding
                // another transcendental instruction to every background pixel.
                float twinklePhase = Hash31(cell + seed + 113.0);
                float twinkleRate = lerp(0.7, 1.3, sizeVariation);
                float twinkleWave = abs(frac(
                    _Time.y * _StarTwinkleSpeed * twinkleRate + twinklePhase) * 2.0 - 1.0);
                float twinkle = lerp(
                    1.0 - _StarTwinkleStrength,
                    1.0 + _StarTwinkleStrength,
                    twinkleWave);

                return star * tint * twinkle * layerBrightness;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.viewDirection = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.viewDirection);

                float horizonBlend = saturate(direction.y * 0.5 + 0.5);
                float3 color = lerp(_SpaceColorB.rgb, _SpaceColorA.rgb, horizonBlend);

                float3 galacticNormal = normalize(float3(0.28, 0.82, -0.5));
                float galacticBand = pow(saturate(1.0 - abs(dot(direction, galacticNormal))), 9.0);
                color += galacticBand * float3(0.012, 0.016, 0.035);

                float3 cosmicFog = SampleCosmicFog(direction);
                color += cosmicFog * _NebulaTint.rgb * _NebulaIntensity;

                float fogLuminance = dot(cosmicFog, float3(0.2126, 0.7152, 0.0722));
                float starVisibility = 1.0 - saturate(
                    fogLuminance * 8.0 * _NebulaStarOcclusion);

                float3 stars = StarLayer(direction, 92.0, 7.0, 1.0);
                stars += StarLayer(direction, 181.0, 73.0, 0.65);
                color += stars * _StarColor.rgb * _StarBrightness * starVisibility;

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunDot = dot(direction, sunDirection);
                float sunDistance = sqrt(max(2.0 - 2.0 * sunDot, 0.0));
                float sunRadius = max(_SunAngularRadius, 0.0001);

                float sunDisc = 1.0 - smoothstep(
                    sunRadius * 0.82,
                    sunRadius * 1.08,
                    sunDistance);
                float sunCore = 1.0 - smoothstep(
                    sunRadius * 0.08,
                    sunRadius * 0.62,
                    sunDistance);
                float sunRim = smoothstep(
                    sunRadius * 0.32,
                    sunRadius * 0.88,
                    sunDistance) * sunDisc;

                float corona = saturate(1.0 - sunDistance / (sunRadius * 6.0));
                corona = corona * corona * corona;
                float outerGlow = pow(saturate(sunDot), 180.0) * _SunGlow;

                float3 sunTangent = normalize(cross(float3(0.0, 1.0, 0.0), sunDirection));
                float3 sunBitangent = cross(sunDirection, sunTangent);
                float tangentDistance = abs(dot(direction, sunTangent));
                float bitangentDistance = abs(dot(direction, sunBitangent));
                float horizontalRay = saturate(
                    1.0 - bitangentDistance / (sunRadius * 0.16)) *
                    saturate(1.0 - tangentDistance / (sunRadius * 8.0));
                float verticalRay = saturate(
                    1.0 - tangentDistance / (sunRadius * 0.16)) *
                    saturate(1.0 - bitangentDistance / (sunRadius * 8.0));
                float rayFade = saturate(1.0 - sunDistance / (sunRadius * 8.0));
                float sunRays = (horizontalRay * horizontalRay + verticalRay * verticalRay) *
                    rayFade * _SunRayStrength;

                float3 discColor = lerp(_SunColor.rgb, _SunCoreColor.rgb, sunCore);
                color += discColor * sunDisc;
                color += _SunColor.rgb * sunRim * 0.18;
                color += _SunCoronaColor.rgb * (
                    corona * _SunCoronaStrength + outerGlow * 0.35 + sunRays);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
