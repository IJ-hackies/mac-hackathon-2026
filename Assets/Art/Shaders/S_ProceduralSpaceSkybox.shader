Shader "SpaceGame/Procedural Space Skybox"
{
    Properties
    {
        _SpaceColorA ("Deep Space", Color) = (0.001, 0.002, 0.008, 1)
        _SpaceColorB ("Space Horizon", Color) = (0.006, 0.012, 0.035, 1)
        [HDR] _StarColor ("Star Color", Color) = (1.2, 1.35, 1.6, 1)
        _StarDensity ("Star Density", Range(0.97, 0.999)) = 0.988
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.4
        _SunDirection ("Sun Direction", Vector) = (0.42, 0.36, 0.83, 0)
        [HDR] _SunColor ("Sun Color", Color) = (8, 4.8, 2.2, 1)
        _SunAngularRadius ("Sun Angular Radius", Range(0.005, 0.08)) = 0.025
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.65
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
            float4 _StarColor;
            float _StarDensity;
            float _StarBrightness;
            float4 _SunDirection;
            float4 _SunColor;
            float _SunAngularRadius;
            float _SunGlow;

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

            float StarLayer(float3 direction, float scale, float seed)
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

                return star * step(_StarDensity, existence) * lerp(0.45, 1.35, sizeVariation);
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
                color += galacticBand * float3(0.018, 0.022, 0.045);

                float stars = StarLayer(direction, 92.0, 7.0);
                stars += StarLayer(direction, 181.0, 73.0) * 0.65;
                color += stars * _StarColor.rgb * _StarBrightness;

                float sunDot = dot(direction, normalize(_SunDirection.xyz));
                float sunDisc = smoothstep(
                    cos(_SunAngularRadius * 1.15),
                    cos(_SunAngularRadius * 0.85),
                    sunDot);
                float sunGlow = pow(saturate(sunDot), 320.0) * _SunGlow;
                color += _SunColor.rgb * (sunDisc + sunGlow);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
