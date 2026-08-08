Shader "SpaceGame/Shooting Star"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1.4, 1.7, 2.4, 1)
        _Intensity ("Intensity", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One
        ColorMask RGB

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            float4 _Tint;
            float _Intensity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float centerDistance = abs(input.uv.y * 2.0 - 1.0);
                float softWidth = saturate(1.0 - centerDistance);
                float coreWidth = softWidth * softWidth;
                coreWidth *= coreWidth;

                float tailFade = smoothstep(0.0, 0.15, input.uv.x) *
                    (1.0 - smoothstep(0.76, 1.0, input.uv.x));
                float head = 1.0 - smoothstep(0.0, 0.18, abs(input.uv.x - 0.82));

                float glow = (softWidth * 0.18 + coreWidth * 1.15) * tailFade;
                glow += head * (softWidth * 0.28 + coreWidth * 1.35);
                float3 color = _Tint.rgb * glow * _Intensity;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
