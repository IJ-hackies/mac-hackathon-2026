Shader "Custom/WaveEnergyBarrier"
{
    Properties
    {
        [HDR] _BarrierColor ("Barrier Color", Color) = (0.52, 0.85, 1, 1)
        _Pulse ("Pulse", Range(0, 1)) = 0.8
        _GridScale ("Grid Scale", Float) = 0.72
        _LineWidth ("Line Width", Range(0.01, 0.15)) = 0.055
        _FillAlpha ("Fill Alpha", Range(0, 0.2)) = 0.018
        _LineAlpha ("Line Alpha", Range(0, 1)) = 0.48
        _ScanSpeed ("Scan Speed", Float) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "WaveEnergyBarrier"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 fieldUV : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BarrierColor;
            float _Pulse;
            float _GridScale;
            float _LineWidth;
            float _FillAlpha;
            float _LineAlpha;
            float _ScanSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);

                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float scaleX = distance(TransformObjectToWorld(float3(1, 0, 0)), originWS);
                float scaleY = distance(TransformObjectToWorld(float3(0, 1, 0)), originWS);
                output.fieldUV = input.positionOS.xy * float2(scaleX, scaleY);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.fieldUV * _GridScale;
                float2 cell = abs(frac(uv) - 0.5);

                float borderDistance = 0.5 - max(cell.x, cell.y);
                float borderAA = max(fwidth(borderDistance), 0.004);
                float border = 1.0 - smoothstep(_LineWidth, _LineWidth + borderAA, borderDistance);

                float diagonalDistance = min(abs(cell.x - cell.y), abs((cell.x + cell.y) - 0.5));
                float diagonalAA = max(fwidth(diagonalDistance), 0.004);
                float diagonal = 1.0 - smoothstep(
                    _LineWidth * 0.45,
                    _LineWidth * 0.45 + diagonalAA,
                    diagonalDistance);

                float nodeDistance = length(cell);
                float node = 1.0 - smoothstep(0.075, 0.14, nodeDistance);
                float lattice = saturate(max(border, diagonal * 0.42) + node * 0.7);

                float scanPosition = frac(input.fieldUV.y * 0.11 - _Time.y * _ScanSpeed);
                float scan = pow(saturate(1.0 - abs(scanPosition - 0.5) * 2.0), 14.0);
                float shimmer = 0.86 + 0.14 * sin(_Time.y * 4.2 + input.fieldUV.x * 1.7);
                float energy = saturate(lattice * shimmer + scan * 0.7);

                float pulse = lerp(0.82, 1.12, _Pulse);
                float alpha = saturate(_FillAlpha + energy * _LineAlpha + scan * 0.08);
                half3 color = _BarrierColor.rgb * (0.16 + energy * 2.8 + scan * 1.35) * pulse;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
